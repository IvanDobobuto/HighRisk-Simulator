using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.DataStructures;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Persistence;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Motor principal del simulador.
/// 
/// Esta versión refuerza el realismo de la jornada con:
/// - delta time escalable 1x-50x;
/// - colas realistas centradas en la estación base y en transferencias reales;
/// - árbol causal interno para memoria contextual del día;
/// - heap manual para acciones futuras y contingencias;
/// - pila manual para historial reciente de eventos;
/// - panel maestro de riesgo con sintonía fina de probabilidades;
/// - métricas consolidadas para exportación de reportes.
/// </summary>
public sealed class SimulationEngine
{
    private const double StationDockingToleranceMeters = 14.0;
    private const double StationDockingVelocityThreshold = 0.55;

    private readonly SimulationModel _model;
    private readonly SimulationOptions _options;
    private readonly ScenarioDefinition _scenario;
    private readonly ISimulationSnapshotRepository _repository;
    private readonly Random _macroRandom;
    private readonly Random _microRandom;
    private readonly RollingMetricSeries _riskSeries;
    private readonly RollingMetricSeries _occupancySeries;
    private readonly RollingMetricSeries _weatherSeries;
    private readonly List<SimulationEvent> _eventTimeline = new();
    private readonly LinkedStack<SimulationEvent> _recentEventStack = new();
    private readonly Dictionary<string, TimeSpan> _cooldowns = new();
    private readonly BinaryMinHeap<PendingSimulationAction> _pendingActions = new();
    private readonly EventualityTree _eventualityTree;
    private readonly Dictionary<int, StationOperationalStats> _stationStats;
    private readonly Dictionary<int, CabinOperationalStats> _cabinStats;

    private int _tickIndex;
    private TimeSpan _elapsed;
    private double _currentRiskScore;
    private double _accumulatedRiskScore;
    private int _riskSamples;
    private double _accumulatedOccupancyPercent;
    private int _occupancySamples;
    private double _accumulatedVisibilityPercent;
    private int _visibilitySamples;
    private double _peakRiskScore;
    private int _processedPassengers;
    private int _rejectedPassengers;
    private int _activeCriticalIssues;
    private double _severityBudget;
    private long _eventSequence;
    private long _pendingSequence;
    private bool _systemWidePowerOutage;
    private TimeSpan _powerOutageRemaining;
    private bool _accidentTriggered;
    private double _currentTimeScale = 1.0;
    private TimeSpan _nextWeatherUpdateAt;
    private TimeSpan _nextBaseDemandWaveAt;
    private double _peakWindSpeed;
    private double _peakIcingRisk;
    private double _lowestTemperatureCelsius;
    private double _baseDemandArrivalCarryover;

    public SimulationEngine(
        SimulationModel model,
        SimulationOptions options,
        ScenarioDefinition scenario,
        ISimulationSnapshotRepository repository)
    {
        _model = model;
        _options = options;
        _scenario = scenario;
        _repository = repository;
        _macroRandom = new Random(options.RandomSeed);
        _microRandom = new Random(options.RandomSeed ^ options.OperationalVarianceSeed);
        _riskSeries = new RollingMetricSeries(options.TelemetryCapacity);
        _occupancySeries = new RollingMetricSeries(options.TelemetryCapacity);
        _weatherSeries = new RollingMetricSeries(options.TelemetryCapacity);
        _eventualityTree = new EventualityTree(options.RandomSeed ^ options.OperationalVarianceSeed);
        _stationStats = _model.Stations.ToDictionary(station => station.Id, station => new StationOperationalStats
        {
            PeakQueue = station.WaitingAscendingPassengers + station.WaitingDescendingPassengers,
            GeneratedAscendingQueue = station.WaitingAscendingPassengers,
            GeneratedDescendingQueue = station.WaitingDescendingPassengers
        });
        _cabinStats = _model.Cabins.ToDictionary(cabin => cabin.Id, cabin => new CabinOperationalStats
        {
            PeakOccupancyPercent = cabin.OccupancyRatio * 100.0,
            PeakAlertLevel = cabin.AlertLevel
        });

        _nextWeatherUpdateAt = TimeSpan.FromSeconds(45);
        _nextBaseDemandWaveAt = TimeSpan.FromSeconds(8);
        _peakWindSpeed = _model.WeatherState.WindSpeedMetersPerSecond;
        _peakIcingRisk = _model.WeatherState.IcingRiskIndex;
        _lowestTemperatureCelsius = _model.Stations.Count == 0
            ? _model.WeatherState.BaseTemperatureCelsius
            : _model.Stations.Min(station => _model.WeatherState.EstimateTemperatureAtAltitude(station.AltitudeMeters));

        _eventualityTree.SeedDailyContext(_model.SeasonalityProfile, _model.DayProfile, _model.WeatherState, _options.PressureMode);
        ScheduleScenarioIncidents();
        RefreshCabinAlertLevels();
        RecalculateRiskAndTelemetry();

        OperationalState = SystemOperationalState.Ready;
        CurrentSnapshot = CreateSnapshot("Sistema inicializado y listo para ejecutar.");
    }

    public SimulationModel Model => _model;

    public SimulationOptions Options => _options;

    public ScenarioDefinition Scenario => _scenario;

    public SystemOperationalState OperationalState { get; private set; }

    public SimulationSnapshot CurrentSnapshot { get; private set; }

    public double CurrentTimeScale => _currentTimeScale;

    public void SetTimeScale(double timeScale)
    {
        _currentTimeScale = Math.Clamp(timeScale, 1.0, 50.0);
    }

    public void ApplyRiskTuning(SimulationRiskTuningProfile tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _options.RiskTuning = tuning.Clone();
        _options.RiskTuning.Normalize();
        RefreshAfterManualIntervention("Se actualizó la calibración dinámica de probabilidades.");
    }

    /// <summary>
    /// Coloca el motor en estado de ejecución.
    /// </summary>
    public SimulationSnapshot Start()
    {
        if (OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed))
        {
            OperationalState = SystemOperationalState.Running;
            CurrentSnapshot = CreateSnapshot("Simulación en ejecución.");
        }

        return CurrentSnapshot;
    }

    /// <summary>
    /// Coloca el motor en pausa sin destruir el estado.
    /// </summary>
    public SimulationSnapshot Pause()
    {
        if (OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed))
        {
            OperationalState = SystemOperationalState.Paused;
            CurrentSnapshot = CreateSnapshot("Simulación en pausa.");
        }

        return CurrentSnapshot;
    }

    /// <summary>
    /// Avanza exactamente un tick lógico usando el escalamiento actual de tiempo.
    /// </summary>
    public SimulationSnapshot Step()
    {
        if (OperationalState is SystemOperationalState.EmergencyStop or SystemOperationalState.Completed)
        {
            return CurrentSnapshot;
        }

        if (OperationalState == SystemOperationalState.Ready)
        {
            OperationalState = SystemOperationalState.Running;
        }

        ExecuteTickCore();
        return CurrentSnapshot;
    }

    /// <summary>
    /// Ejecuta la jornada hasta el final del servicio o hasta una parada de emergencia.
    /// </summary>
    public SimulationSnapshot RunToEndOfService()
    {
        if (OperationalState == SystemOperationalState.Ready)
        {
            OperationalState = SystemOperationalState.Running;
        }

        if (OperationalState is SystemOperationalState.EmergencyStop or SystemOperationalState.Completed)
        {
            return CurrentSnapshot;
        }

        while (OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed))
        {
            ExecuteTickCore(publishSnapshot: false);
        }

        PublishSnapshot(ComposeNarrative());
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectMechanicalFailure(int cabinId)
    {
        var cabin = _model.GetCabin(cabinId);
        ApplyMechanicalFailure(cabin, forcedSevere: true, sourceTag: "Inyección manual");
        RefreshAfterManualIntervention("Se inyectó una falla mecánica manualmente.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectElectricalFailure(int? cabinId = null)
    {
        if (cabinId is null)
        {
            ApplySystemWideElectricalFailure(sourceTag: "Inyección manual", severity: EventSeverity.Critical, duration: TimeSpan.FromMinutes(6));
        }
        else
        {
            var cabin = _model.GetCabin(cabinId.Value);
            cabin.ApplyElectricalDamage(0.30 + (_microRandom.NextDouble() * 0.15), true, TimeSpan.FromMinutes(4));
            cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
            EmitEvent(
                SimulationEventType.ElectricalFailure,
                EventSeverity.Critical,
                $"Falla eléctrica manual en {cabin.Code}",
                $"Se inyectó manualmente una falla eléctrica sobre la cabina {cabin.Code}.",
                22,
                "Inyección manual",
                cabin: cabin,
                segment: _model.GetSegment(cabin.AssignedSegmentId));
        }

        RefreshAfterManualIntervention("Se inyectó una falla eléctrica.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectStorm()
    {
        ApplyWeatherCondition(WeatherCondition.Storm);
        _eventualityTree.RegisterWeatherContext(_model.WeatherState, _elapsed);
        EmitEvent(
            SimulationEventType.ExtremeWeather,
            EventSeverity.Critical,
            "Tormenta inyectada",
            "La interfaz activó manualmente una tormenta de altura para evaluar resiliencia del sistema.",
            18,
            "Inyección manual");

        RefreshAfterManualIntervention("Se forzó una tormenta de altura.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectStrongWind()
    {
        ApplyWeatherCondition(WeatherCondition.Windy);
        _eventualityTree.RegisterWeatherContext(_model.WeatherState, _elapsed);
        EmitEvent(
            SimulationEventType.ExtremeWeather,
            EventSeverity.Warning,
            "Viento fuerte inyectado",
            "La interfaz activó una ráfaga intensa para validar estabilidad, separación y protocolos de desaceleración.",
            11,
            "Inyección manual");

        RefreshAfterManualIntervention("Se forzó viento fuerte sobre la línea.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectFog()
    {
        ApplyWeatherCondition(WeatherCondition.Fog);
        _eventualityTree.RegisterWeatherContext(_model.WeatherState, _elapsed);
        EmitEvent(
            SimulationEventType.ExtremeWeather,
            EventSeverity.Warning,
            "Neblina densa inyectada",
            "La interfaz degradó la visibilidad para comprobar lectura operacional y reacción del operador.",
            9,
            "Inyección manual");

        RefreshAfterManualIntervention("Se forzó una capa de neblina densa.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectPulleyWear(int cabinId)
    {
        var cabin = _model.GetCabin(cabinId);
        var segment = _model.GetSegment(cabin.AssignedSegmentId);
        cabin.ApplyMechanicalDamage(0.05 + (_microRandom.NextDouble() * 0.05), false, TimeSpan.Zero);
        cabin.ApplyBrakeDamage(0.02 + (_microRandom.NextDouble() * 0.03));
        EmitEvent(
            SimulationEventType.MechanicalWear,
            EventSeverity.Warning,
            $"Desgaste acelerado en {cabin.Code}",
            $"Se inyectó manualmente una condición de desgaste de poleas o rodadura sobre {cabin.Code} para validar diagnóstico temprano.",
            8,
            "Inyección manual",
            cabin: cabin,
            segment: segment);

        RefreshAfterManualIntervention("Se inyectó desgaste mecánico puntual.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectVoltageSpike(int? cabinId = null)
    {
        if (cabinId is null)
        {
            foreach (var cabin in _model.Cabins.Where(item => !item.IsOutOfService))
            {
                cabin.ApplyElectricalDamage(0.05 + (_microRandom.NextDouble() * 0.04), false, TimeSpan.Zero);
                if (_microRandom.NextDouble() < 0.28)
                {
                    cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(1));
                }
            }

            EmitEvent(
                SimulationEventType.VoltageSpike,
                EventSeverity.Warning,
                "Pico de tensión general",
                "Se provocó un transitorio eléctrico a escala de sistema para probar resiliencia de protección y recuperación.",
                13,
                "Inyección manual");
        }
        else
        {
            var cabin = _model.GetCabin(cabinId.Value);
            var segment = _model.GetSegment(cabin.AssignedSegmentId);
            cabin.ApplyElectricalDamage(0.14 + (_microRandom.NextDouble() * 0.10), false, TimeSpan.Zero);
            cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
            EmitEvent(
                SimulationEventType.VoltageSpike,
                EventSeverity.Warning,
                $"Pico de tensión en {cabin.Code}",
                $"La cabina {cabin.Code} recibió una sobretensión controlada para validar telemetría y reacción del sistema.",
                10,
                "Inyección manual",
                cabin: cabin,
                segment: segment);
        }

        RefreshAfterManualIntervention("Se inyectó un pico de tensión.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectOverload(int cabinId)
    {
        var cabin = _model.GetCabin(cabinId);
        cabin.SetPassengers(cabin.Capacity + _microRandom.Next(3, 11));
        EmitEvent(
            SimulationEventType.Overload,
            EventSeverity.Warning,
            $"Sobrecarga manual en {cabin.Code}",
            $"La cabina {cabin.Code} quedó por encima de su capacidad nominal para validación del protocolo de control.",
            15,
            "Inyección manual",
            cabin: cabin,
            segment: _model.GetSegment(cabin.AssignedSegmentId));

        RefreshAfterManualIntervention("Se inyectó una sobrecarga manual.");
        return CurrentSnapshot;
    }

    public SimulationSnapshot InjectEmergencyStop()
    {
        foreach (var cabin in _model.Cabins)
        {
            cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(3));
        }

        EmitEvent(
            SimulationEventType.EmergencyBrake,
            EventSeverity.Critical,
            "Parada de emergencia manual",
            "La interfaz activó manualmente el protocolo de parada de emergencia del sistema.",
            20,
            "Inyección manual",
            requiresEmergencyStop: true);

        OperationalState = SystemOperationalState.EmergencyStop;
        RefreshAfterManualIntervention("El sistema entró en parada de emergencia manual.");
        return CurrentSnapshot;
    }

    public SimulationRunReport CreateRunReport()
    {
        var averageRisk = _riskSamples == 0 ? _currentRiskScore : _accumulatedRiskScore / _riskSamples;
        var averageOccupancy = _occupancySamples == 0 ? 0 : _accumulatedOccupancyPercent / _occupancySamples;
        var averageVisibility = _visibilitySamples == 0 ? _model.WeatherState.VisibilityFactor * 100.0 : _accumulatedVisibilityPercent / _visibilitySamples;
        var warningEvents = _eventTimeline.Count(item => item.Severity == EventSeverity.Warning);
        var criticalEvents = _eventTimeline.Count(item => item.Severity == EventSeverity.Critical);
        var catastrophicEvents = _eventTimeline.Count(item => item.Severity == EventSeverity.Catastrophic);

        var tuning = _options.RiskTuning ?? new SimulationRiskTuningProfile();

        return new SimulationRunReport
        {
            SystemName = _model.SystemName,
            ScenarioName = _scenario.Name,
            DayProfileName = _model.DayProfile.Name,
            SeasonalityLabel = _model.SeasonalityProfile.FullDisplayName,
            PressureModeLabel = _options.PressureMode.ToDisplayText(),
            FinalStateLabel = OperationalState.ToDisplayText(),
            ExecutiveSummary = BuildExecutiveSummary(averageRisk, averageOccupancy),
            Conclusions = BuildConclusions(averageRisk),
            EventualityFingerprint = _eventualityTree.Fingerprint,
            RiskCalibrationSummary = tuning.ToSummaryText(),
            SimulationDate = _model.SimulationDate,
            GeneratedAtUtc = DateTime.UtcNow,
            BaseSeed = _options.RandomSeed,
            OperationalVarianceSeed = _options.OperationalVarianceSeed,
            SimulatedElapsed = _elapsed,
            MaxRiskScore = _peakRiskScore,
            AverageRiskScore = averageRisk,
            AverageOccupancyPercent = averageOccupancy,
            AverageVisibilityPercent = averageVisibility,
            PeakIcingRiskPercent = _peakIcingRisk * 100.0,
            PeakWindSpeedMetersPerSecond = _peakWindSpeed,
            LowestTemperatureCelsius = _lowestTemperatureCelsius,
            TotalProcessedPassengers = _processedPassengers,
            TotalRejectedPassengers = _rejectedPassengers,
            TotalEvents = _eventTimeline.Count,
            WarningEvents = warningEvents,
            CriticalEvents = criticalEvents,
            CatastrophicEvents = catastrophicEvents,
            EndedByEmergencyStop = OperationalState == SystemOperationalState.EmergencyStop,
            GlobalRiskMultiplier = tuning.GlobalRiskMultiplier,
            StormProbabilityMultiplier = tuning.StormProbabilityMultiplier,
            WindProbabilityMultiplier = tuning.WindProbabilityMultiplier,
            FogProbabilityMultiplier = tuning.FogProbabilityMultiplier,
            MechanicalWearProbabilityMultiplier = tuning.MechanicalWearProbabilityMultiplier,
            CabinMechanicalFailureProbabilityMultiplier = tuning.CabinMechanicalFailureProbabilityMultiplier,
            PowerOutageProbabilityMultiplier = tuning.PowerOutageProbabilityMultiplier,
            VoltageSpikeProbabilityMultiplier = tuning.VoltageSpikeProbabilityMultiplier,
            Timeline = _eventTimeline.ToList(),
            Stations = BuildStationReportEntries(),
            Cabins = BuildCabinReportEntries(),
            RiskSeries = _riskSeries.Snapshot(),
            OccupancySeries = _occupancySeries.Snapshot(),
            WeatherSeries = _weatherSeries.Snapshot()
        };
    }

    private void ExecuteTickCore(bool publishSnapshot = true)
    {
        if (OperationalState is SystemOperationalState.EmergencyStop or SystemOperationalState.Completed)
        {
            return;
        }

        var delta = GetEffectiveDelta();
        _tickIndex++;
        _elapsed += delta;

        UpdatePowerGridState(delta);
        UpdateWeather();
        UpdateExternalDemand();
        ProcessPendingActions();
        ProcessCabinMotion(delta);
        ProcessRandomIncidents(delta);
        RefreshCabinAlertLevels();
        RecalculateRiskAndTelemetry();
        EvaluateSafetyRules(delta);
        UpdateOperationalState();

        if (!publishSnapshot)
        {
            return;
        }

        PublishSnapshot(ComposeNarrative());
    }

    private TimeSpan GetEffectiveDelta()
    {
        return TimeSpan.FromTicks((long)(_options.FixedTimeStep.Ticks * _currentTimeScale));
    }

    private void RefreshAfterManualIntervention(string narrative)
    {
        RefreshCabinAlertLevels();
        RecalculateRiskAndTelemetry();
        UpdateOperationalState();
        PublishSnapshot(narrative);
    }

    private void PublishSnapshot(string narrative)
    {
        CurrentSnapshot = CreateSnapshot(narrative);
        _repository.Save(CurrentSnapshot);
    }

    private void ScheduleScenarioIncidents()
    {
        if (_options.Mode != SimulationMode.ScriptedScenario || _scenario.ScheduledIncidents.Count == 0)
        {
            return;
        }

        foreach (var incident in _scenario.ScheduledIncidents)
        {
            EnqueueAction(
                incident.TriggerAt,
                ResolvePriorityFromSeverity(incident.Severity),
                $"Escenario: {incident.Title}",
                () => ApplyScheduledIncident(incident));
        }
    }

    private void ProcessPendingActions()
    {
        while (_pendingActions.TryPeek(out var pending) && pending!.DueAt <= _elapsed)
        {
            var dueAction = _pendingActions.Dequeue();
            dueAction.Execute();
        }
    }

    private void UpdatePowerGridState(TimeSpan delta)
    {
        if (!_systemWidePowerOutage)
        {
            return;
        }

        _powerOutageRemaining -= delta;
        if (_powerOutageRemaining > TimeSpan.Zero)
        {
            return;
        }

        _systemWidePowerOutage = false;
        _powerOutageRemaining = TimeSpan.Zero;

        foreach (var cabin in _model.Cabins)
        {
            if (!cabin.HasElectricalFailure && !cabin.HasMechanicalFailure && !cabin.IsOutOfService)
            {
                cabin.ClearEmergencyBrake();
            }
        }

        EmitEvent(
            SimulationEventType.ElectricalFailure,
            EventSeverity.Info,
            "Recuperación del suministro eléctrico",
            "La red principal se estabilizó y el sistema puede abandonar la protección eléctrica progresivamente.",
            -8,
            "Infraestructura");
    }

    private void UpdateWeather()
    {
        if (!_options.EnableWeatherSystem || _elapsed < _nextWeatherUpdateAt)
        {
            return;
        }

        var tuning = _options.RiskTuning ?? new SimulationRiskTuningProfile();
        var baseIntervalSeconds = 45 - ((_model.SeasonalityProfile.WeatherVolatilityMultiplier - 1.0) * 12.0);
        var tunedInterval = baseIntervalSeconds / Math.Clamp(0.75 + ((tuning.GlobalRiskMultiplier - 1.0) * 0.18), 0.65, 1.35);
        _nextWeatherUpdateAt = _elapsed + TimeSpan.FromSeconds(Math.Clamp(tunedInterval + (_microRandom.NextDouble() * 18.0), 18.0, 65.0));

        var currentCondition = _model.WeatherState.Condition;
        var nextCondition = ResolveNextCondition(currentCondition);
        ApplyWeatherCondition(nextCondition);

        _peakWindSpeed = Math.Max(_peakWindSpeed, _model.WeatherState.WindSpeedMetersPerSecond);
        _peakIcingRisk = Math.Max(_peakIcingRisk, _model.WeatherState.IcingRiskIndex);
        _lowestTemperatureCelsius = Math.Min(
            _lowestTemperatureCelsius,
            _model.Stations.Min(station => _model.WeatherState.EstimateTemperatureAtAltitude(station.AltitudeMeters)));

        if (nextCondition != currentCondition || _model.WeatherState.IcingRiskIndex >= 0.40 || _model.WeatherState.VisibilityFactor <= 0.72)
        {
            _eventualityTree.RegisterWeatherContext(_model.WeatherState, _elapsed);
        }

        if (nextCondition == WeatherCondition.Storm && TryAcquireCooldown("weather-storm", TimeSpan.FromMinutes(18)))
        {
            EmitEvent(
                SimulationEventType.ExtremeWeather,
                EventSeverity.Critical,
                "Tormenta en altura",
                "Las condiciones de viento y visibilidad degradan de forma notable la operación de los tramos altos.",
                16,
                "Clima");
        }
        else if (nextCondition == WeatherCondition.Snow && TryAcquireCooldown("weather-snow", TimeSpan.FromMinutes(18)))
        {
            EmitEvent(
                SimulationEventType.ExtremeWeather,
                EventSeverity.Warning,
                "Nevada con riesgo de engelamiento",
                "La temperatura y la humedad elevan el riesgo de hielo y reducen la velocidad operativa.",
                10,
                "Clima");
        }
        else if (nextCondition == WeatherCondition.Windy && TryAcquireCooldown("weather-windy", TimeSpan.FromMinutes(16)))
        {
            EmitEvent(
                SimulationEventType.ExtremeWeather,
                EventSeverity.Warning,
                "Vientos fuertes de ladera",
                "Las ráfagas exigen mayor margen de separación, lectura de balanceo y control de velocidad.",
                8,
                "Clima");
        }
        else if (nextCondition == WeatherCondition.Fog && TryAcquireCooldown("weather-fog", TimeSpan.FromMinutes(16)))
        {
            EmitEvent(
                SimulationEventType.ExtremeWeather,
                EventSeverity.Warning,
                "Neblina densa",
                "La reducción de visibilidad exige operación conservadora, vigilancia continua y confirmación redundante de estado.",
                7,
                "Clima");
        }
    }

    private WeatherCondition ResolveNextCondition(WeatherCondition currentCondition)
    {
        var tuning = _options.RiskTuning ?? new SimulationRiskTuningProfile();
        var volatility = _model.DayProfile.WeatherVolatility * _model.SeasonalityProfile.WeatherVolatilityMultiplier;
        var transitionChance = Math.Clamp(0.14 * volatility * Math.Max(0.78, tuning.GlobalRiskMultiplier), 0.06, 0.44);

        if (_microRandom.NextDouble() >= transitionChance)
        {
            return currentCondition;
        }

        return currentCondition switch
        {
            WeatherCondition.Clear => SelectWeightedWeather(
                (WeatherCondition.Clear, 0.18),
                (WeatherCondition.Cold, 0.30),
                (WeatherCondition.Windy, 0.18 * tuning.WindProbabilityMultiplier),
                (WeatherCondition.Fog, 0.16 * tuning.FogProbabilityMultiplier),
                (WeatherCondition.Snow, 0.10),
                (WeatherCondition.Storm, 0.08 * tuning.StormProbabilityMultiplier)),

            WeatherCondition.Cold => SelectWeightedWeather(
                (WeatherCondition.Clear, 0.20),
                (WeatherCondition.Cold, 0.18),
                (WeatherCondition.Windy, 0.20 * tuning.WindProbabilityMultiplier),
                (WeatherCondition.Fog, 0.14 * tuning.FogProbabilityMultiplier),
                (WeatherCondition.Snow, 0.16),
                (WeatherCondition.Storm, 0.12 * tuning.StormProbabilityMultiplier)),

            WeatherCondition.Windy => SelectWeightedWeather(
                (WeatherCondition.Clear, 0.16),
                (WeatherCondition.Cold, 0.18),
                (WeatherCondition.Windy, 0.18 * tuning.WindProbabilityMultiplier),
                (WeatherCondition.Fog, 0.10 * tuning.FogProbabilityMultiplier),
                (WeatherCondition.Snow, 0.16),
                (WeatherCondition.Storm, 0.22 * tuning.StormProbabilityMultiplier)),

            WeatherCondition.Fog => SelectWeightedWeather(
                (WeatherCondition.Clear, 0.16),
                (WeatherCondition.Cold, 0.18),
                (WeatherCondition.Windy, 0.18 * tuning.WindProbabilityMultiplier),
                (WeatherCondition.Fog, 0.20 * tuning.FogProbabilityMultiplier),
                (WeatherCondition.Snow, 0.12),
                (WeatherCondition.Storm, 0.16 * tuning.StormProbabilityMultiplier)),

            WeatherCondition.Snow => SelectWeightedWeather(
                (WeatherCondition.Cold, 0.22),
                (WeatherCondition.Windy, 0.18 * tuning.WindProbabilityMultiplier),
                (WeatherCondition.Fog, 0.08 * tuning.FogProbabilityMultiplier),
                (WeatherCondition.Snow, 0.24),
                (WeatherCondition.Storm, 0.18 * tuning.StormProbabilityMultiplier)),

            WeatherCondition.Storm => SelectWeightedWeather(
                (WeatherCondition.Windy, 0.24 * tuning.WindProbabilityMultiplier),
                (WeatherCondition.Fog, 0.14 * tuning.FogProbabilityMultiplier),
                (WeatherCondition.Cold, 0.20),
                (WeatherCondition.Snow, 0.18),
                (WeatherCondition.Storm, 0.16 * tuning.StormProbabilityMultiplier)),

            _ => currentCondition,
        };
    }

    private WeatherCondition SelectWeightedWeather(params (WeatherCondition Condition, double Weight)[] candidates)
    {
        if (candidates.Length == 0)
        {
            return _model.WeatherState.Condition;
        }

        var totalWeight = candidates.Sum(candidate => Math.Max(0.0, candidate.Weight));
        if (totalWeight <= 0)
        {
            return candidates[0].Condition;
        }

        var roll = _microRandom.NextDouble() * totalWeight;
        var cumulative = 0.0;

        foreach (var candidate in candidates)
        {
            cumulative += Math.Max(0.0, candidate.Weight);
            if (roll <= cumulative)
            {
                return candidate.Condition;
            }
        }

        return candidates[^1].Condition;
    }

    private void ApplyWeatherCondition(WeatherCondition condition)
    {
        var seasonalBaseTemperature = _model.SeasonalityProfile.Band switch
        {
            SeasonDemandBand.Low => 14.5,
            SeasonDemandBand.Regular => 13.0,
            SeasonDemandBand.High => 12.0,
            SeasonDemandBand.Peak => 11.8,
            _ => 13.0
        };

        switch (condition)
        {
            case WeatherCondition.Clear:
                _model.WeatherState.Apply(condition, 3.5 + (_microRandom.NextDouble() * 2.5), seasonalBaseTemperature + 2.0, 1.00, 1.00, 1.00, 0.45, 0.05, 0.00);
                break;
            case WeatherCondition.Cold:
                _model.WeatherState.Apply(condition, 5.5 + (_microRandom.NextDouble() * 2.5), seasonalBaseTemperature - 1.5, 0.96, 0.94, 1.08, 0.62, 0.28, 0.08);
                break;
            case WeatherCondition.Windy:
                _model.WeatherState.Apply(condition, 9.5 + (_microRandom.NextDouble() * 5.5), seasonalBaseTemperature - 2.0, 0.88, 0.84, 1.22, 0.58, 0.20, 0.10);
                break;
            case WeatherCondition.Fog:
                _model.WeatherState.Apply(condition, 4.8 + (_microRandom.NextDouble() * 2.8), seasonalBaseTemperature - 1.8, 0.80, 0.76, 1.18, 0.30, 0.14, 0.06);
                break;
            case WeatherCondition.Snow:
                _model.WeatherState.Apply(condition, 8.0 + (_microRandom.NextDouble() * 5.5), seasonalBaseTemperature - 5.0, 0.72, 0.66, 1.42, 0.84, 0.72, 0.65);
                break;
            case WeatherCondition.Storm:
                _model.WeatherState.Apply(condition, 14.0 + (_microRandom.NextDouble() * 9.0), seasonalBaseTemperature - 4.0, 0.54, 0.50, 1.72, 0.90, 0.46, 0.88);
                break;
        }
    }

    private void UpdateExternalDemand()
    {
        if (!_options.EnableRandomDemand || _elapsed < _nextBaseDemandWaveAt)
        {
            return;
        }

        var intervalSeconds = 18 + (_microRandom.NextDouble() * 18);
        _nextBaseDemandWaveAt = _elapsed + TimeSpan.FromSeconds(intervalSeconds);

        var baseStation = _model.Stations.First(station => station.IsLowerTerminal);
        var baseSegment = _model.Segments.OrderBy(segment => segment.VisualOrder).First();
        var serviceClock = _options.ServiceStartTime + _elapsed;
        var hour = serviceClock.TotalHours;

        var capacityPerHour = EstimateBaseSegmentBoardingCapacityPerHour(baseSegment);
        var hourlyDemandFactor = ResolveDemandWaveFactor(hour);
        var pressureDemandFactor = _options.PressureMode == SimulationPressureMode.IntensifiedTraining ? 1.10 : 1.00;
        var demandProfileFactor = Math.Clamp(
            0.60 +
            ((_model.DayProfile.DemandMultiplier - 1.0) * 0.45) +
            ((_model.SeasonalityProfile.DemandMultiplier - 1.0) * 0.32) +
            ((_options.DemandMultiplier - 1.0) * 0.35),
            0.46,
            _options.PressureMode == SimulationPressureMode.Realistic ? 0.94 : 1.08);
        var stochasticFactor = 0.90 + (_microRandom.NextDouble() * 0.22);

        var expectedArrivals = capacityPerHour
            * hourlyDemandFactor
            * pressureDemandFactor
            * demandProfileFactor
            * stochasticFactor
            * (intervalSeconds / 3600.0);

        _baseDemandArrivalCarryover += expectedArrivals;
        var arrivals = (int)Math.Floor(_baseDemandArrivalCarryover);
        _baseDemandArrivalCarryover -= arrivals;

        if (arrivals == 0 && baseStation.WaitingAscendingPassengers == 0 && _microRandom.NextDouble() < 0.22)
        {
            arrivals = 1;
        }

        arrivals = Math.Max(0, arrivals);
        if (arrivals == 0)
        {
            return;
        }

        baseStation.EnqueuePassengers(arrivals, 0);
        _stationStats[baseStation.Id].GeneratedAscendingQueue += arrivals;
        UpdateStationQueuePeak(baseStation);

        if (arrivals >= 4 && TryAcquireCooldown("demand-wave", TimeSpan.FromMinutes(12)))
        {
            EmitEvent(
                SimulationEventType.PassengerDemand,
                EventSeverity.Info,
                "Nueva ola de demanda en Barinitas",
                $"Ingresan {arrivals} pasajeros a la cola ascendente de la estación base con una cadencia ajustada a la capacidad operativa del primer tramo.",
                1.8,
                "Demanda",
                station: baseStation);
        }
    }

    private double EstimateBaseSegmentBoardingCapacityPerHour(TrackSegment baseSegment)
    {
        var fleet = _model.GetSegmentFleet(baseSegment.Id);
        var totalCabinsServingBase = Math.Max(1, fleet.Cabins.Count);
        var nominalCruiseSpeed = Math.Max(2.8, baseSegment.MaxOperationalSpeedMetersPerSecond * 0.82);
        var oneWayMinutes = Math.Max(9.5, (baseSegment.LengthMeters / nominalCruiseSpeed) / 60.0);
        var stationCycleMinutes = _model.GetStation(baseSegment.StartStationId).DefaultDwellTime.TotalMinutes
            + _model.GetStation(baseSegment.EndStationId).DefaultDwellTime.TotalMinutes
            + 0.8;
        var roundTripMinutes = Math.Max(18.0, (oneWayMinutes * 2.0) + stationCycleMinutes);
        var tripsPerHour = totalCabinsServingBase * (60.0 / roundTripMinutes);
        return tripsPerHour * 60.0;
    }

    private static double ResolveDemandWaveFactor(double hour)
    {
        return hour switch
        {
            >= 9 and < 10.5 => 0.88,
            >= 10.5 and < 12.5 => 1.00,
            >= 12.5 and < 14.0 => 0.84,
            >= 14.0 and < 16.5 => 0.74,
            >= 16.5 and < 18.0 => 0.62,
            _ => 0.42
        };
    }

    private void ApplyScheduledIncident(ScheduledIncident incident)
    {
        switch (incident.EventType)
        {
            case SimulationEventType.Overload:
            {
                var cabin = ResolveIncidentCabin(incident);
                cabin.SetPassengers(cabin.Capacity + _microRandom.Next(6, 18));
                EmitEvent(
                    incident.EventType,
                    incident.Severity,
                    incident.Title,
                    incident.Description,
                    incident.RiskDelta,
                    incident.SourceTag,
                    cabin: cabin,
                    segment: _model.GetSegment(cabin.AssignedSegmentId));
                break;
            }

            case SimulationEventType.MechanicalFailure:
            {
                var cabin = ResolveIncidentCabin(incident);
                cabin.ApplyMechanicalDamage(0.28 + (_microRandom.NextDouble() * 0.18), true, TimeSpan.FromMinutes(4 + _microRandom.Next(0, 4)));
                cabin.ApplyBrakeDamage(0.06 + (_microRandom.NextDouble() * 0.08));
                cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
                EmitEvent(
                    incident.EventType,
                    incident.Severity,
                    incident.Title,
                    incident.Description,
                    incident.RiskDelta,
                    incident.SourceTag,
                    cabin: cabin,
                    segment: _model.GetSegment(cabin.AssignedSegmentId));

                if (incident.Severity >= EventSeverity.Critical)
                {
                    cabin.MarkOutOfService(TimeSpan.FromMinutes(8));
                    EmitEvent(
                        SimulationEventType.CabinOutOfService,
                        EventSeverity.Critical,
                        $"Cabina fuera de servicio: {cabin.Code}",
                        $"La cabina {cabin.Code} queda fuera de servicio por contención del evento programado.",
                        14,
                        incident.SourceTag,
                        cabin: cabin,
                        segment: _model.GetSegment(cabin.AssignedSegmentId));
                }

                break;
            }

            case SimulationEventType.ElectricalFailure:
            {
                if (incident.CabinId is null && incident.SegmentId is null)
                {
                    ApplySystemWideElectricalFailure(incident.SourceTag, incident.Severity, TimeSpan.FromMinutes(6));
                    EmitEvent(
                        incident.EventType,
                        incident.Severity,
                        incident.Title,
                        incident.Description,
                        incident.RiskDelta,
                        incident.SourceTag,
                        requiresEmergencyStop: incident.RequiresEmergencyStop);
                }
                else
                {
                    var cabin = ResolveIncidentCabin(incident);
                    cabin.ApplyElectricalDamage(0.30 + (_microRandom.NextDouble() * 0.14), true, TimeSpan.FromMinutes(5));
                    cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
                    EmitEvent(
                        incident.EventType,
                        incident.Severity,
                        incident.Title,
                        incident.Description,
                        incident.RiskDelta,
                        incident.SourceTag,
                        cabin: cabin,
                        segment: _model.GetSegment(cabin.AssignedSegmentId));
                }

                break;
            }

            case SimulationEventType.ExtremeWeather:
            {
                ApplyWeatherCondition(incident.ForcedWeather ?? WeatherCondition.Storm);
                _eventualityTree.RegisterWeatherContext(_model.WeatherState, _elapsed);
                EmitEvent(
                    incident.EventType,
                    incident.Severity,
                    incident.Title,
                    incident.Description,
                    incident.RiskDelta,
                    incident.SourceTag);
                break;
            }

            case SimulationEventType.EmergencyBrake:
            {
                if (incident.CabinId is not null || incident.SegmentId is not null)
                {
                    var cabin = ResolveIncidentCabin(incident);
                    cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
                    EmitEvent(
                        incident.EventType,
                        incident.Severity,
                        incident.Title,
                        incident.Description,
                        incident.RiskDelta,
                        incident.SourceTag,
                        cabin: cabin,
                        segment: _model.GetSegment(cabin.AssignedSegmentId));
                }
                else
                {
                    foreach (var cabin in _model.Cabins)
                    {
                        cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
                    }

                    EmitEvent(
                        incident.EventType,
                        incident.Severity,
                        incident.Title,
                        incident.Description,
                        incident.RiskDelta,
                        incident.SourceTag);
                }

                break;
            }

            case SimulationEventType.CabinOutOfService:
            {
                var cabin = ResolveIncidentCabin(incident);
                cabin.MarkOutOfService(TimeSpan.FromMinutes(8));
                EmitEvent(
                    incident.EventType,
                    incident.Severity,
                    incident.Title,
                    incident.Description,
                    incident.RiskDelta,
                    incident.SourceTag,
                    cabin: cabin,
                    segment: _model.GetSegment(cabin.AssignedSegmentId));
                break;
            }

            case SimulationEventType.Accident:
            {
                TriggerAccident(incident.Title, incident.Description, incident.SourceTag);
                break;
            }
        }
    }

    private Cabin ResolveIncidentCabin(ScheduledIncident incident)
    {
        if (incident.CabinId is int cabinId)
        {
            return _model.GetCabin(cabinId);
        }

        if (incident.SegmentId is int segmentId)
        {
            var fleet = _model.GetSegmentFleet(segmentId);
            var preferredCabin = fleet.Cabins
                .Where(cabin => !cabin.IsOutOfService)
                .OrderByDescending(cabin => cabin.PassengerCount)
                .ThenBy(cabin => cabin.Id)
                .FirstOrDefault();

            if (preferredCabin is not null)
            {
                return preferredCabin;
            }
        }

        return _model.Cabins.First();
    }

    private void ProcessRandomIncidents(TimeSpan delta)
    {
        var dtSeconds = delta.TotalSeconds;
        var tuning = _options.RiskTuning ?? new SimulationRiskTuningProfile();
        var globalRisk = Math.Clamp(tuning.GlobalRiskMultiplier, 0.25, 4.00);
        var weatherFactor = _model.WeatherState.RiskMultiplier * _model.SeasonalityProfile.IncidentPressureMultiplier;
        var pressureModeFactor = _options.PressureMode == SimulationPressureMode.Realistic ? 0.72 : 1.55;
        var pressure = _model.DayProfile.IncidentPressure * _options.RandomIncidentMultiplier * pressureModeFactor * globalRisk;

        var blackoutCascade = 1.0 + _eventualityTree.EvaluateCascadePressure(
            "electrica",
            _elapsed,
            new[] { "grid", _model.WeatherState.Condition.ToString() });

        var blackoutRate = 0.00000012 * pressure * weatherFactor * blackoutCascade * tuning.PowerOutageProbabilityMultiplier;
        if (!_systemWidePowerOutage && !_accidentTriggered && ShouldOccur(blackoutRate, dtSeconds) && TryAcquireCooldown("grid-blackout", TimeSpan.FromMinutes(20)))
        {
            ApplySystemWideElectricalFailure("Aleatorio", EventSeverity.Critical, TimeSpan.FromMinutes(5 + _microRandom.Next(0, 4)));
            EmitEvent(
                SimulationEventType.ElectricalFailure,
                EventSeverity.Critical,
                "Falla eléctrica general",
                "La red principal entra en falla y el sistema ejecuta el protocolo de protección global.",
                24,
                "Aleatorio");
        }

        foreach (var cabin in _model.Cabins)
        {
            var segment = _model.GetSegment(cabin.AssignedSegmentId);
            var segmentAltitude = GetAverageSegmentAltitude(segment);
            var segmentRiskMultiplier = _model.WeatherState.EstimateSegmentRiskMultiplier(segment, segmentAltitude);
            var tags = new[]
            {
                segment.Name,
                cabin.Code,
                _model.WeatherState.Condition.ToString()
            };

            var preEmergency = cabin.IsEmergencyBrakeActive;

            if (cabin.IsOutOfService)
            {
                _cabinStats[cabin.Id].OutOfServiceDuration += delta;
                continue;
            }

            if (!cabin.IsOverloaded &&
                cabin.RemainingDwellTime > TimeSpan.Zero &&
                segment.VisualOrder == 1 &&
                cabin.Direction == TravelDirection.Ascending)
            {
                var baseStation = _model.GetStation(segment.StartStationId);
                var queuePressure = baseStation.WaitingAscendingPassengers;
                if (queuePressure >= 45 &&
                    ShouldOccur(0.00000022 * pressure * (1.0 + (queuePressure / 80.0)), dtSeconds) &&
                    TryAcquireCooldown($"overload-random-{cabin.Id}", TimeSpan.FromMinutes(14)))
                {
                    cabin.SetPassengers(cabin.Capacity + _microRandom.Next(2, 8));
                    EmitEvent(
                        SimulationEventType.Overload,
                        EventSeverity.Warning,
                        $"Sobrecarga puntual en {cabin.Code}",
                        $"La presión de cola en estación base derivó en un abordaje por encima de la capacidad nominal de {cabin.Code}.",
                        12,
                        "Aleatorio",
                        cabin: cabin,
                        segment: segment,
                        station: baseStation);
                }
            }

            var loadFactor = 0.70 + cabin.OccupancyRatio;
            var mechanicalCascade = 1.0 + _eventualityTree.EvaluateCascadePressure("mecanica", _elapsed, tags);
            var electricalCascade = 1.0 + _eventualityTree.EvaluateCascadePressure("electrica", _elapsed, tags);

            var wearRate = 0.00000105 * pressure * segmentRiskMultiplier * loadFactor * tuning.MechanicalWearProbabilityMultiplier * (0.85 + ((1.0 - cabin.BrakeHealth) * 0.60));
            if (ShouldOccur(wearRate, dtSeconds) && TryAcquireCooldown($"wear-{cabin.Id}", TimeSpan.FromMinutes(12)))
            {
                var wearAmount = 0.02 + (_microRandom.NextDouble() * 0.03);
                cabin.ApplyMechanicalDamage(wearAmount, false, TimeSpan.Zero);
                cabin.ApplyBrakeDamage(0.01 + (_microRandom.NextDouble() * 0.025));
                EmitEvent(
                    SimulationEventType.MechanicalWear,
                    cabin.MechanicalHealth < 0.58 || cabin.BrakeHealth < 0.58 ? EventSeverity.Warning : EventSeverity.Info,
                    $"Desgaste progresivo detectado en {cabin.Code}",
                    $"La telemetría de rodadura y frenado revela desgaste acumulado en {cabin.Code}; se recomienda inspección preventiva.",
                    cabin.MechanicalHealth < 0.58 || cabin.BrakeHealth < 0.58 ? 6 : 3,
                    "Aleatorio",
                    cabin: cabin,
                    segment: segment);
            }

            var mechanicalRate = 0.00000085 * pressure * segmentRiskMultiplier * loadFactor * mechanicalCascade * tuning.CabinMechanicalFailureProbabilityMultiplier * (1.0 + ((1.0 - cabin.MechanicalHealth) * 2.2));
            if (ShouldOccur(mechanicalRate, dtSeconds) && TryAcquireCooldown($"mechanical-{cabin.Id}", TimeSpan.FromMinutes(16)))
            {
                ApplyMechanicalFailure(cabin, forcedSevere: false, sourceTag: "Aleatorio");
            }

            var voltageSpikeRate = 0.00000072 * pressure * segmentRiskMultiplier * electricalCascade * tuning.VoltageSpikeProbabilityMultiplier * (0.90 + ((1.0 - cabin.ElectricalHealth) * 1.20));
            if (ShouldOccur(voltageSpikeRate, dtSeconds) && TryAcquireCooldown($"voltage-spike-{cabin.Id}", TimeSpan.FromMinutes(14)))
            {
                var spikeDamage = 0.05 + (_microRandom.NextDouble() * 0.06);
                var escalate = cabin.ElectricalHealth < 0.58 || _microRandom.NextDouble() < 0.24;
                cabin.ApplyElectricalDamage(spikeDamage, escalate, TimeSpan.FromMinutes(3 + _microRandom.Next(0, 2)));
                cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(escalate ? 2 : 1));
                EmitEvent(
                    SimulationEventType.VoltageSpike,
                    escalate ? EventSeverity.Warning : EventSeverity.Info,
                    $"Pico de tensión en {cabin.Code}",
                    escalate
                        ? $"{cabin.Code} recibió una sobretensión que obligó a freno protector y verificación del subsistema eléctrico."
                        : $"{cabin.Code} registró una sobretensión breve contenida por protecciones internas.",
                    escalate ? 8 : 4,
                    "Aleatorio",
                    cabin: cabin,
                    segment: segment);
            }

            var electricalRate = 0.00000055 * pressure * segmentRiskMultiplier * electricalCascade * tuning.PowerOutageProbabilityMultiplier * (0.85 + ((1.0 - cabin.ElectricalHealth) * 2.0));
            if (ShouldOccur(electricalRate, dtSeconds) && TryAcquireCooldown($"electrical-{cabin.Id}", TimeSpan.FromMinutes(18)))
            {
                cabin.ApplyElectricalDamage(0.10 + (_microRandom.NextDouble() * 0.12), true, TimeSpan.FromMinutes(4 + _microRandom.Next(0, 3)));
                cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
                EmitEvent(
                    SimulationEventType.ElectricalFailure,
                    EventSeverity.Warning,
                    $"Anomalía eléctrica en {cabin.Code}",
                    $"{cabin.Code} presenta una inestabilidad eléctrica local y activa desaceleración protectiva.",
                    11,
                    "Aleatorio",
                    cabin: cabin,
                    segment: segment);
            }

            if (!preEmergency && cabin.IsEmergencyBrakeActive)
            {
                _cabinStats[cabin.Id].EmergencyBrakeActivations++;
            }
        }
    }

    private void ApplySystemWideElectricalFailure(string sourceTag, EventSeverity severity, TimeSpan duration)
    {
        _systemWidePowerOutage = true;
        _powerOutageRemaining = duration;

        foreach (var cabin in _model.Cabins)
        {
            cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
        }

        EmitEvent(
            SimulationEventType.EmergencyBrake,
            severity >= EventSeverity.Critical ? EventSeverity.Warning : severity,
            "Frenado preventivo del sistema",
            "Todas las cabinas activan frenado preventivo mientras se estabiliza el suministro energético.",
            10,
            sourceTag);
    }

    private void ApplyMechanicalFailure(Cabin cabin, bool forcedSevere, string sourceTag)
    {
        var segment = _model.GetSegment(cabin.AssignedSegmentId);
        var severe = forcedSevere || _microRandom.NextDouble() < (0.22 + ((1.0 - cabin.MechanicalHealth) * 0.38) + (_model.WeatherState.IcingRiskIndex * 0.16));

        cabin.ApplyMechanicalDamage(
            severe ? 0.20 + (_microRandom.NextDouble() * 0.16) : 0.07 + (_microRandom.NextDouble() * 0.10),
            severe,
            TimeSpan.FromMinutes(severe ? 5 + _microRandom.Next(0, 4) : 3 + _microRandom.Next(0, 2)));

        cabin.ApplyBrakeDamage(0.04 + (_microRandom.NextDouble() * 0.06));
        cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));

        EmitEvent(
            SimulationEventType.MechanicalFailure,
            severe ? EventSeverity.Critical : EventSeverity.Warning,
            $"Falla mecánica en {cabin.Code}",
            severe
                ? $"{cabin.Code} presenta una degradación mecánica relevante y obliga a un protocolo inmediato de protección."
                : $"{cabin.Code} presenta una anomalía mecánica contenida.",
            severe ? 18 : 9,
            sourceTag,
            cabin: cabin,
            segment: segment);

        if (severe && _microRandom.NextDouble() < 0.32)
        {
            cabin.MarkOutOfService(TimeSpan.FromMinutes(7 + _microRandom.Next(0, 4)));
            EmitEvent(
                SimulationEventType.CabinOutOfService,
                EventSeverity.Critical,
                $"Retiro temporal de {cabin.Code}",
                $"La cabina {cabin.Code} queda fuera de servicio mientras se contiene la desviación mecánica.",
                12,
                "Seguridad",
                cabin: cabin,
                segment: segment);
        }
    }

    private void ProcessCabinMotion(TimeSpan delta)
    {
        foreach (var fleet in _model.SegmentFleets.Values.OrderBy(item => item.Segment.VisualOrder))
        {
            fleet.RebuildDispatchRing();
            var leaders = BuildLeaderMap(fleet);

            foreach (var cabin in fleet.DispatchRing.EnumerateDispatchOrder())
            {
                UpdateCabinMotion(fleet, cabin, leaders.TryGetValue(cabin.Id, out var leader) ? leader : null, delta);
            }
        }
    }

    private Dictionary<int, Cabin?> BuildLeaderMap(SegmentFleet fleet)
    {
        var leaders = new Dictionary<int, Cabin?>();

        foreach (var direction in new[] { TravelDirection.Ascending, TravelDirection.Descending })
        {
            var ordered = direction == TravelDirection.Ascending
                ? fleet.Cabins.Where(cabin => cabin.Direction == direction).OrderBy(cabin => cabin.SegmentPositionMeters).ToList()
                : fleet.Cabins.Where(cabin => cabin.Direction == direction).OrderByDescending(cabin => cabin.SegmentPositionMeters).ToList();

            for (var index = 0; index < ordered.Count; index++)
            {
                leaders[ordered[index].Id] = index < ordered.Count - 1 ? ordered[index + 1] : null;
            }
        }

        return leaders;
    }

    private void UpdateCabinMotion(SegmentFleet fleet, Cabin cabin, Cabin? leaderCabin, TimeSpan delta)
    {
        var segment = fleet.Segment;
        var dt = delta.TotalSeconds;
        var previousPosition = cabin.SegmentPositionMeters;
        var preEmergency = cabin.IsEmergencyBrakeActive;

        cabin.AdvanceFaultTimers(delta);

        if (cabin.IsOutOfService)
        {
            _cabinStats[cabin.Id].OutOfServiceDuration += delta;
        }

        if (cabin.RemainingDwellTime > TimeSpan.Zero)
        {
            cabin.ReduceDwell(delta);
            cabin.UpdateMotion(cabin.SegmentPositionMeters, 0, 0, cabin.IsOutOfService ? CabinOperationalState.OutOfService : CabinOperationalState.IdleAtStation);
            return;
        }

        if (cabin.IsOutOfService)
        {
            cabin.UpdateMotion(cabin.SegmentPositionMeters, 0, 0, CabinOperationalState.OutOfService);
            return;
        }

        if (_systemWidePowerOutage || cabin.HasElectricalFailure || cabin.HasMechanicalFailure || cabin.IsEmergencyBrakeActive)
        {
            ApplyEmergencyBraking(cabin, segment, dt);
            if (!preEmergency && cabin.IsEmergencyBrakeActive)
            {
                _cabinStats[cabin.Id].EmergencyBrakeActivations++;
            }
            return;
        }

        var targetSpeed = CalculateTargetSpeed(cabin, segment, leaderCabin);
        var distanceToArrival = cabin.Direction == TravelDirection.Ascending
            ? segment.LengthMeters - cabin.SegmentPositionMeters
            : cabin.SegmentPositionMeters;

        if (TryResolveStationDocking(cabin, segment, previousPosition, distanceToArrival, cabin.VelocityMetersPerSecond))
        {
            return;
        }

        var brakingDistance = cabin.VelocityMetersPerSecond * cabin.VelocityMetersPerSecond /
            (2 * Math.Max(0.05, segment.ServiceDecelerationMetersPerSecondSquared));

        double acceleration;
        CabinOperationalState newState;

        if (distanceToArrival <= Math.Max(18, brakingDistance + 8))
        {
            acceleration = -segment.ServiceDecelerationMetersPerSecondSquared;
            newState = CabinOperationalState.Braking;
        }
        else if (cabin.VelocityMetersPerSecond < targetSpeed - 0.12)
        {
            acceleration = segment.ServiceAccelerationMetersPerSecondSquared;
            newState = CabinOperationalState.Accelerating;
        }
        else if (cabin.VelocityMetersPerSecond > targetSpeed + 0.12)
        {
            acceleration = -segment.ServiceDecelerationMetersPerSecondSquared;
            newState = CabinOperationalState.Braking;
        }
        else
        {
            acceleration = 0;
            newState = CabinOperationalState.Cruising;
        }

        var nextVelocity = Math.Clamp(cabin.VelocityMetersPerSecond + (acceleration * dt), 0, targetSpeed);
        var travelDistance = Math.Max(0, (cabin.VelocityMetersPerSecond + nextVelocity) * 0.5 * dt);
        var nextPosition = cabin.Direction == TravelDirection.Ascending
            ? cabin.SegmentPositionMeters + travelDistance
            : cabin.SegmentPositionMeters - travelDistance;

        if (cabin.Direction == TravelDirection.Ascending && nextPosition >= segment.LengthMeters)
        {
            cabin.UpdateMotion(segment.LengthMeters, 0, 0, CabinOperationalState.IdleAtStation);
            _cabinStats[cabin.Id].DistanceTravelledMeters += Math.Abs(segment.LengthMeters - previousPosition);
            ResolveStationArrival(cabin, segment, _model.GetStation(segment.EndStationId), TravelDirection.Ascending);
            return;
        }

        if (cabin.Direction == TravelDirection.Descending && nextPosition <= 0)
        {
            cabin.UpdateMotion(0, 0, 0, CabinOperationalState.IdleAtStation);
            _cabinStats[cabin.Id].DistanceTravelledMeters += Math.Abs(previousPosition);
            ResolveStationArrival(cabin, segment, _model.GetStation(segment.StartStationId), TravelDirection.Descending);
            return;
        }

        var distanceAfterMove = cabin.Direction == TravelDirection.Ascending
            ? Math.Max(0, segment.LengthMeters - nextPosition)
            : Math.Max(0, nextPosition);

        if (TryResolveStationDocking(cabin, segment, previousPosition, distanceAfterMove, nextVelocity))
        {
            return;
        }

        cabin.UpdateMotion(nextPosition, nextVelocity, acceleration, newState);
        _cabinStats[cabin.Id].DistanceTravelledMeters += Math.Abs(nextPosition - previousPosition);
    }

    private bool TryResolveStationDocking(Cabin cabin, TrackSegment segment, double previousPosition, double distanceToArrival, double velocityMetersPerSecond)
    {
        if (distanceToArrival > StationDockingToleranceMeters || velocityMetersPerSecond > StationDockingVelocityThreshold)
        {
            return false;
        }

        var isAscendingArrival = cabin.Direction == TravelDirection.Ascending;
        var finalPosition = isAscendingArrival ? segment.LengthMeters : 0.0;
        var arrivalStation = _model.GetStation(isAscendingArrival ? segment.EndStationId : segment.StartStationId);

        cabin.UpdateMotion(finalPosition, 0, 0, CabinOperationalState.IdleAtStation);
        _cabinStats[cabin.Id].DistanceTravelledMeters += Math.Abs(finalPosition - previousPosition);
        ResolveStationArrival(cabin, segment, arrivalStation, isAscendingArrival ? TravelDirection.Ascending : TravelDirection.Descending);
        return true;
    }

    private void ApplyEmergencyBraking(Cabin cabin, TrackSegment segment, double dt)
    {
        if (!cabin.IsEmergencyBrakeActive)
        {
            cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
        }

        var emergencyDeceleration = segment.EmergencyDecelerationMetersPerSecondSquared * Math.Max(0.45, cabin.BrakeHealth);
        var nextVelocity = Math.Max(0, cabin.VelocityMetersPerSecond - (emergencyDeceleration * dt));
        var travelDistance = Math.Max(0, (cabin.VelocityMetersPerSecond + nextVelocity) * 0.5 * dt);

        var nextPosition = cabin.Direction == TravelDirection.Ascending
            ? Math.Min(segment.LengthMeters, cabin.SegmentPositionMeters + travelDistance)
            : Math.Max(0, cabin.SegmentPositionMeters - travelDistance);

        cabin.UpdateMotion(
            nextPosition,
            nextVelocity,
            -emergencyDeceleration,
            cabin.IsOutOfService ? CabinOperationalState.OutOfService : CabinOperationalState.EmergencyBraking);
    }

    private double CalculateTargetSpeed(Cabin cabin, TrackSegment segment, Cabin? leaderCabin)
    {
        var averageAltitude = GetAverageSegmentAltitude(segment);
        var weatherPenalty = _model.WeatherState.EstimateSegmentSpeedMultiplier(segment, averageAltitude);
        var healthPenalty = Math.Clamp((cabin.MechanicalHealth + cabin.ElectricalHealth + cabin.BrakeHealth) / 3.0, 0.45, 1.0);
        var loadPenalty = cabin.IsOverloaded ? 0.76 : (cabin.OccupancyRatio > 0.85 ? 0.90 : 1.0);
        var leaderPenalty = 1.0;

        if (leaderCabin is not null)
        {
            var gap = cabin.Direction == TravelDirection.Ascending
                ? leaderCabin.SegmentPositionMeters - cabin.SegmentPositionMeters
                : cabin.SegmentPositionMeters - leaderCabin.SegmentPositionMeters;

            if (gap < segment.MinimumSeparationMeters * 1.15)
            {
                leaderPenalty = 0.35;
            }
            else if (gap < segment.MinimumSeparationMeters * 1.60)
            {
                leaderPenalty = 0.60;
            }
            else if (gap < segment.MinimumSeparationMeters * 2.20)
            {
                leaderPenalty = 0.82;
            }
        }

        return segment.MaxOperationalSpeedMetersPerSecond * weatherPenalty * healthPenalty * loadPenalty * leaderPenalty;
    }

    private void ResolveStationArrival(Cabin cabin, TrackSegment segment, Station arrivalStation, TravelDirection arrivalDirection)
    {
        var unloadedPassengers = cabin.UnloadAllPassengers();
        _processedPassengers += unloadedPassengers;
        _stationStats[arrivalStation.Id].UnloadedPassengers += unloadedPassengers;
        _cabinStats[cabin.Id].UnloadedPassengers += unloadedPassengers;
        _cabinStats[cabin.Id].CompletedTrips++;

        SchedulePassengerDecisions(arrivalStation, arrivalDirection, unloadedPassengers);

        cabin.ReverseDirection();

        if (cabin.IsOutOfService || cabin.HasElectricalFailure || cabin.HasMechanicalFailure)
        {
            cabin.StartStationStop(CalculateDynamicDwell(arrivalStation, unloadedPassengers, 0));
            return;
        }

        var queueBeforeBoarding = cabin.Direction == TravelDirection.Ascending
            ? arrivalStation.WaitingAscendingPassengers
            : arrivalStation.WaitingDescendingPassengers;

        var availableCapacity = cabin.Capacity - cabin.PassengerCount;
        var boardedPassengers = cabin.Direction == TravelDirection.Ascending
            ? arrivalStation.DequeueAscendingPassengers(availableCapacity)
            : arrivalStation.DequeueDescendingPassengers(availableCapacity);

        cabin.BoardPassengers(boardedPassengers);
        cabin.StartStationStop(CalculateDynamicDwell(arrivalStation, unloadedPassengers, boardedPassengers));

        var leftWaiting = Math.Max(0, queueBeforeBoarding - boardedPassengers);
        _rejectedPassengers += leftWaiting;
        _stationStats[arrivalStation.Id].LeftWaitingPassengers += leftWaiting;

        if (cabin.Direction == TravelDirection.Ascending)
        {
            _stationStats[arrivalStation.Id].BoardedAscending += boardedPassengers;
        }
        else
        {
            _stationStats[arrivalStation.Id].BoardedDescending += boardedPassengers;
        }

        _cabinStats[cabin.Id].BoardedPassengers += boardedPassengers;
        _cabinStats[cabin.Id].PeakOccupancyPercent = Math.Max(_cabinStats[cabin.Id].PeakOccupancyPercent, cabin.OccupancyRatio * 100.0);
        UpdateStationQueuePeak(arrivalStation);
    }

    private TimeSpan CalculateDynamicDwell(Station station, int unloadedPassengers, int boardedPassengers)
    {
        var passengerHandlingSeconds = (unloadedPassengers * 0.14) + (boardedPassengers * 0.11);
        var weatherPenaltySeconds = (_model.WeatherState.VisibilityFactor < 0.75 ? 1.8 : 0.0) + (_model.WeatherState.IcingRiskIndex * 2.2);
        var queuePenaltySeconds = Math.Min(4.0, (station.TotalWaitingPassengers / 25.0));
        var totalSeconds = station.DefaultDwellTime.TotalSeconds + passengerHandlingSeconds + weatherPenaltySeconds + queuePenaltySeconds;
        return TimeSpan.FromSeconds(Math.Clamp(totalSeconds, station.DefaultDwellTime.TotalSeconds, station.DefaultDwellTime.TotalSeconds + 9.0));
    }

    private void SchedulePassengerDecisions(Station station, TravelDirection arrivalDirection, int unloadedPassengers)
    {
        if (unloadedPassengers <= 0)
        {
            return;
        }

        if (station.IsLowerTerminal)
        {
            return;
        }

        var continuationCount = 0;
        var returnCount = 0;

        if (station.IsUpperTerminal)
        {
            var returnRatio = Math.Clamp(0.72 + (station.AttractionFactor * 0.06) + ((_model.DayProfile.ReturnBias - 1.0) * 0.08), 0.55, 0.92);
            returnCount = (int)Math.Round(unloadedPassengers * ApplyRatioJitter(returnRatio));
        }
        else
        {
            var continuationRatio = Math.Clamp(0.18 + (station.AttractionFactor * 0.09) + ((_model.DayProfile.TransferContinuationBias - 1.0) * 0.09), 0.10, 0.42);
            var returnRatio = Math.Clamp(0.16 + (station.AttractionFactor * 0.10) + ((_model.DayProfile.ReturnBias - 1.0) * 0.10), 0.10, 0.40);

            continuationCount = (int)Math.Round(unloadedPassengers * ApplyRatioJitter(continuationRatio));
            returnCount = (int)Math.Round(unloadedPassengers * ApplyRatioJitter(returnRatio));

            if (continuationCount + returnCount > unloadedPassengers)
            {
                var overflow = (continuationCount + returnCount) - unloadedPassengers;
                returnCount = Math.Max(0, returnCount - overflow);
            }
        }

        if (continuationCount > 0)
        {
            var delay = TimeSpan.FromMinutes(1.2 + (_microRandom.NextDouble() * 2.2));
            EnqueueAction(
                _elapsed + delay,
                priority: 3,
                description: $"Transferencia inmediata en {station.Name}",
                action: () =>
                {
                    if (arrivalDirection == TravelDirection.Ascending)
                    {
                        station.EnqueuePassengers(continuationCount, 0);
                        _stationStats[station.Id].GeneratedAscendingQueue += continuationCount;
                    }
                    else
                    {
                        station.EnqueuePassengers(0, continuationCount);
                        _stationStats[station.Id].GeneratedDescendingQueue += continuationCount;
                    }

                    UpdateStationQueuePeak(station);
                });
        }

        if (returnCount > 0)
        {
            var attractionMinutes = station.IsUpperTerminal
                ? 12.0 + (station.AttractionFactor * 10.0)
                : 6.0 + (station.AttractionFactor * 7.5);

            var delay = TimeSpan.FromMinutes(attractionMinutes + (_microRandom.NextDouble() * 9.0));
            EnqueueAction(
                _elapsed + delay,
                priority: 4,
                description: $"Retorno diferido en {station.Name}",
                action: () =>
                {
                    if (arrivalDirection == TravelDirection.Ascending)
                    {
                        station.EnqueuePassengers(0, returnCount);
                        _stationStats[station.Id].GeneratedDescendingQueue += returnCount;
                    }
                    else
                    {
                        station.EnqueuePassengers(returnCount, 0);
                        _stationStats[station.Id].GeneratedAscendingQueue += returnCount;
                    }

                    UpdateStationQueuePeak(station);
                });
        }
    }

    private void EvaluateSafetyRules(TimeSpan delta)
    {
        var separationViolation = false;

        foreach (var fleet in _model.SegmentFleets.Values)
        {
            foreach (var direction in new[] { TravelDirection.Ascending, TravelDirection.Descending })
            {
                var orderedCabins = direction == TravelDirection.Ascending
                    ? fleet.Cabins.Where(cabin => cabin.Direction == direction).OrderBy(cabin => cabin.SegmentPositionMeters).ToList()
                    : fleet.Cabins.Where(cabin => cabin.Direction == direction).OrderByDescending(cabin => cabin.SegmentPositionMeters).ToList();

                for (var index = 0; index < orderedCabins.Count - 1; index++)
                {
                    var first = orderedCabins[index];
                    var second = orderedCabins[index + 1];
                    var gap = direction == TravelDirection.Ascending
                        ? second.SegmentPositionMeters - first.SegmentPositionMeters
                        : first.SegmentPositionMeters - second.SegmentPositionMeters;

                    if (gap < fleet.Segment.MinimumSeparationMeters && TryAcquireCooldown($"separation-{fleet.Segment.Id}-{direction}", TimeSpan.FromMinutes(6)))
                    {
                        separationViolation = true;
                        first.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));
                        second.ActivateEmergencyBrake(TimeSpan.FromMinutes(2));

                        EmitEvent(
                            SimulationEventType.SeparationLoss,
                            gap < fleet.Segment.MinimumSeparationMeters * 0.60 ? EventSeverity.Critical : EventSeverity.Warning,
                            "Pérdida de separación segura",
                            $"El tramo {fleet.Segment.Name} redujo su distancia operativa segura entre {first.Code} y {second.Code}.",
                            gap < fleet.Segment.MinimumSeparationMeters * 0.60 ? 18 : 10,
                            "Seguridad",
                            cabin: first,
                            segment: fleet.Segment);
                    }
                }
            }
        }

        var criticalRuleBreach = separationViolation ||
            (_systemWidePowerOutage && _model.WeatherState.Condition == WeatherCondition.Storm) ||
            _model.Cabins.Any(cabin => cabin.IsOverloaded && (cabin.HasMechanicalFailure || cabin.HasElectricalFailure)) ||
            _model.Cabins.Count(cabin => cabin.IsOutOfService) >= 3;

        if (criticalRuleBreach)
        {
            _severityBudget += delta.TotalSeconds * (1.10 + (_activeCriticalIssues * 0.18));
        }
        else
        {
            _severityBudget = Math.Max(0, _severityBudget - (delta.TotalSeconds * 0.90));
        }

        if (!_options.EnableSafetyEscalation || _accidentTriggered)
        {
            return;
        }

        var thresholdRisk = _options.PressureMode == SimulationPressureMode.Realistic ? 94.0 : 82.0;
        var thresholdBudget = _options.PressureMode == SimulationPressureMode.Realistic ? 14.0 : 8.0;

        if (criticalRuleBreach && _currentRiskScore >= thresholdRisk && _severityBudget >= thresholdBudget)
        {
            var accidentRate = (_options.PressureMode == SimulationPressureMode.Realistic ? 0.00008 : 0.00022) * (1.0 + (_severityBudget / 18.0));
            if (ShouldOccur(accidentRate, delta.TotalSeconds))
            {
                TriggerAccident(
                    "Accidente operacional simulado",
                    "Se encadenaron varias condiciones críticas y el sistema produjo una consecuencia operacional mayor.",
                    "Seguridad");
            }
        }
    }

    private void TriggerAccident(string title, string description, string sourceTag)
    {
        _accidentTriggered = true;
        _currentRiskScore = 100;

        foreach (var cabin in _model.Cabins)
        {
            cabin.ActivateEmergencyBrake(TimeSpan.FromMinutes(4));
            if (_microRandom.NextDouble() < 0.22)
            {
                cabin.MarkOutOfService(TimeSpan.FromMinutes(10));
            }
        }

        EmitEvent(
            SimulationEventType.Accident,
            EventSeverity.Catastrophic,
            title,
            description,
            35,
            sourceTag,
            requiresEmergencyStop: true);

        OperationalState = SystemOperationalState.EmergencyStop;
    }

    private void RecalculateRiskAndTelemetry()
    {
        double risk = 0;
        var criticalIssues = 0;

        foreach (var cabin in _model.Cabins)
        {
            if (cabin.OccupancyRatio > 0.85)
            {
                risk += 3 + ((cabin.OccupancyRatio - 0.85) * 10.0);
            }

            if (cabin.IsOverloaded)
            {
                risk += 16 + Math.Max(0, cabin.OccupancyRatio - 1.0) * 18;
                criticalIssues++;
            }

            if (cabin.HasMechanicalFailure)
            {
                risk += 18 * (1.0 + (1.0 - cabin.MechanicalHealth));
                criticalIssues++;
            }
            else if (cabin.MechanicalHealth < 0.65 || cabin.BrakeHealth < 0.65)
            {
                risk += 5.5 * (1.0 + ((1.0 - Math.Min(cabin.MechanicalHealth, cabin.BrakeHealth)) * 1.4));
            }

            if (cabin.HasElectricalFailure)
            {
                risk += 15 * (1.0 + (1.0 - cabin.ElectricalHealth));
                criticalIssues++;
            }
            else if (cabin.ElectricalHealth < 0.65)
            {
                risk += 4.5 * (1.0 + ((1.0 - cabin.ElectricalHealth) * 1.3));
            }

            if (cabin.IsEmergencyBrakeActive)
            {
                risk += 5.5;
            }

            if (cabin.IsOutOfService)
            {
                risk += 12;
                criticalIssues++;
            }
        }

        if (_systemWidePowerOutage)
        {
            risk += 14;
            criticalIssues++;
        }

        risk += (_model.WeatherState.RiskMultiplier - 1.0) * 14.0;
        risk += _model.WeatherState.IcingRiskIndex * 12.0;

        if (_model.WeatherState.Condition == WeatherCondition.Fog)
        {
            risk += 5.0;
        }

        var averageQueue = _model.Stations.Count == 0
            ? 0
            : _model.Stations.Average(station => station.WaitingAscendingPassengers + station.WaitingDescendingPassengers);
        risk += Math.Min(10, averageQueue / 11.5);

        var safetyCascade = _eventualityTree.EvaluateCascadePressure(
            "seguridad",
            _elapsed,
            new[] { _model.WeatherState.Condition.ToString(), _options.PressureMode.ToString() });
        risk += safetyCascade * 6.0;

        _currentRiskScore = Math.Clamp(risk, 0, 100);
        _peakRiskScore = Math.Max(_peakRiskScore, _currentRiskScore);
        _activeCriticalIssues = criticalIssues;

        var averageOccupancyPercent = _model.Cabins.Count == 0
            ? 0
            : _model.Cabins.Average(cabin => cabin.OccupancyRatio) * 100.0;

        var visibilityPercent = _model.WeatherState.VisibilityFactor * 100.0;

        _accumulatedRiskScore += _currentRiskScore;
        _riskSamples++;
        _accumulatedOccupancyPercent += averageOccupancyPercent;
        _occupancySamples++;
        _accumulatedVisibilityPercent += visibilityPercent;
        _visibilitySamples++;
        _peakWindSpeed = Math.Max(_peakWindSpeed, _model.WeatherState.WindSpeedMetersPerSecond);
        _peakIcingRisk = Math.Max(_peakIcingRisk, _model.WeatherState.IcingRiskIndex);

        _riskSeries.Add(_elapsed, _currentRiskScore);
        _occupancySeries.Add(_elapsed, averageOccupancyPercent);
        _weatherSeries.Add(_elapsed, ((_model.WeatherState.RiskMultiplier - 1.0) * 100.0) + (_model.WeatherState.IcingRiskIndex * 25.0));
    }

    private void RefreshCabinAlertLevels()
    {
        foreach (var cabin in _model.Cabins)
        {
            var alertLevel = ResolveAlertLevel(cabin);
            cabin.SetAlertLevel(alertLevel);
            _cabinStats[cabin.Id].PeakAlertLevel = alertLevel > _cabinStats[cabin.Id].PeakAlertLevel
                ? alertLevel
                : _cabinStats[cabin.Id].PeakAlertLevel;
        }
    }

    private CabinAlertLevel ResolveAlertLevel(Cabin cabin)
    {
        if (cabin.IsOutOfService || cabin.HasMechanicalFailure || cabin.HasElectricalFailure || cabin.IsOverloaded)
        {
            return CabinAlertLevel.Critical;
        }

        if (cabin.IsEmergencyBrakeActive ||
            cabin.OperationalState is CabinOperationalState.Braking or CabinOperationalState.EmergencyBraking ||
            cabin.OccupancyRatio > 0.85 ||
            cabin.MechanicalHealth < 0.72 ||
            cabin.ElectricalHealth < 0.72 ||
            cabin.BrakeHealth < 0.72 ||
            _model.WeatherState.Condition is WeatherCondition.Windy or WeatherCondition.Snow or WeatherCondition.Storm)
        {
            return CabinAlertLevel.Alert;
        }

        return CabinAlertLevel.Normal;
    }

    private void UpdateOperationalState()
    {
        if (OperationalState == SystemOperationalState.EmergencyStop)
        {
            return;
        }

        if (_elapsed >= _options.ServiceDuration)
        {
            OperationalState = SystemOperationalState.Completed;
            return;
        }

        if (_activeCriticalIssues > 0 || _systemWidePowerOutage || _model.Cabins.Any(cabin => cabin.AlertLevel == CabinAlertLevel.Critical))
        {
            OperationalState = SystemOperationalState.Degraded;
            return;
        }

        OperationalState = SystemOperationalState.Running;
    }

    private string ComposeNarrative()
    {
        if (OperationalState == SystemOperationalState.EmergencyStop)
        {
            return "La jornada terminó en protocolo de emergencia por escalamiento de riesgo operacional.";
        }

        if (OperationalState == SystemOperationalState.Completed)
        {
            return _eventTimeline.Any(item => item.Severity >= EventSeverity.Critical)
                ? "La jornada finalizó con incidentes relevantes, pero el sistema logró cerrar la operación simulada."
                : "La jornada finalizó de forma controlada y sin incidentes mayores.";
        }

        if (_systemWidePowerOutage)
        {
            return "Operación degradada: corte eléctrico activo y frenado de protección en progreso.";
        }

        if (_model.WeatherState.Condition == WeatherCondition.Storm)
        {
            return "Operación restringida por tormenta en cotas altas; el sistema prioriza visibilidad y seguridad.";
        }

        if (_model.WeatherState.Condition == WeatherCondition.Fog)
        {
            return "Operación conservadora por neblina densa; la visibilidad cayó y la interfaz resalta telemetría crítica.";
        }

        if (_model.WeatherState.Condition == WeatherCondition.Windy)
        {
            return "Operación vigilada por ráfagas de viento; el despacho se ajusta para preservar estabilidad y separación.";
        }

        if (_model.Cabins.Any(cabin => cabin.IsOutOfService))
        {
            return "Operación degradada: una o más cabinas están temporalmente fuera de servicio.";
        }

        if (_model.Stations.Any(station => station.WaitingAscendingPassengers + station.WaitingDescendingPassengers > 45))
        {
            return "Alta afluencia: la presión de colas crece y el sistema compensa con control de dwell y despacho.";
        }

        return "Operación estable. Movimiento, transferencias y monitoreo continuo dentro de parámetros normales.";
    }

    private bool TryAcquireCooldown(string key, TimeSpan duration)
    {
        if (!_cooldowns.TryGetValue(key, out var blockedUntil) || _elapsed >= blockedUntil)
        {
            _cooldowns[key] = _elapsed + duration;
            return true;
        }

        return false;
    }

    private bool ShouldOccur(double ratePerSecond, double deltaSeconds)
    {
        if (ratePerSecond <= 0 || deltaSeconds <= 0)
        {
            return false;
        }

        var chance = 1.0 - Math.Exp(-(ratePerSecond * deltaSeconds));
        return _microRandom.NextDouble() < Math.Clamp(chance, 0.0, 1.0);
    }

    private double ApplyRatioJitter(double ratio)
    {
        var jitter = 0.92 + (_microRandom.NextDouble() * 0.16);
        return Math.Clamp(ratio * jitter, 0.0, 0.98);
    }

    private double GetAverageSegmentAltitude(TrackSegment segment)
    {
        var startStation = _model.GetStation(segment.StartStationId);
        var endStation = _model.GetStation(segment.EndStationId);
        return (startStation.AltitudeMeters + endStation.AltitudeMeters) * 0.5;
    }

    private void UpdateStationQueuePeak(Station station)
    {
        var currentQueue = station.WaitingAscendingPassengers + station.WaitingDescendingPassengers;
        if (_stationStats.TryGetValue(station.Id, out var stats))
        {
            stats.PeakQueue = Math.Max(stats.PeakQueue, currentQueue);
        }
    }

    private void EnqueueAction(TimeSpan dueAt, int priority, string description, Action action)
    {
        _pendingActions.Enqueue(new PendingSimulationAction(++_pendingSequence, dueAt, priority, description, action));
    }

    private int ResolvePriorityFromSeverity(EventSeverity severity)
    {
        return severity switch
        {
            EventSeverity.Catastrophic => 0,
            EventSeverity.Critical => 1,
            EventSeverity.Warning => 2,
            _ => 3,
        };
    }

    private SimulationEvent EmitEvent(
        SimulationEventType type,
        EventSeverity severity,
        string title,
        string description,
        double riskDelta,
        string sourceTag,
        Cabin? cabin = null,
        TrackSegment? segment = null,
        Station? station = null,
        bool requiresEmergencyStop = false)
    {
        var simulationEvent = new SimulationEvent(
            ++_eventSequence,
            _elapsed,
            type,
            severity,
            title,
            description,
            riskDelta,
            sourceTag,
            cabin?.Code,
            segment?.Name,
            station?.Name,
            requiresEmergencyStop);

        _eventTimeline.Add(simulationEvent);
        _recentEventStack.Push(simulationEvent);
        _eventualityTree.RegisterEvent(simulationEvent);

        return simulationEvent;
    }

    private SimulationSnapshot CreateSnapshot(string narrative)
    {
        var cabinSnapshots = _model.Cabins
            .OrderBy(cabin => cabin.AssignedSegmentId)
            .ThenBy(cabin => cabin.Id)
            .Select(CreateCabinSnapshot)
            .ToList();

        var stationSnapshots = _model.Stations
            .OrderBy(station => station.RoutePositionMeters)
            .Select(CreateStationSnapshot)
            .ToList();

        var telemetry = new TelemetrySnapshot(
            _riskSeries.Snapshot(),
            _occupancySeries.Snapshot(),
            _weatherSeries.Snapshot());

        return new SimulationSnapshot(
            _tickIndex,
            _elapsed,
            _model.SimulationDate,
            _options.Mode,
            _scenario.Name,
            _model.DayProfile.Name,
            _model.SeasonalityProfile.FullDisplayName,
            _options.PressureMode,
            _options.RandomSeed,
            _options.OperationalVarianceSeed,
            OperationalState,
            narrative,
            _model.WeatherState.ToDisplayText(),
            _model.WeatherState.Condition,
            _currentRiskScore,
            cabinSnapshots.Count == 0 ? 0 : cabinSnapshots.Average(snapshot => snapshot.OccupancyPercent),
            _model.WeatherState.VisibilityFactor * 100.0,
            _model.WeatherState.IcingRiskIndex * 100.0,
            _processedPassengers,
            _rejectedPassengers,
            _activeCriticalIssues,
            _eventTimeline.Count,
            cabinSnapshots,
            stationSnapshots,
            _recentEventStack.Take(60).ToList(),
            telemetry);
    }

    private CabinSnapshot CreateCabinSnapshot(Cabin cabin)
    {
        var segment = _model.GetSegment(cabin.AssignedSegmentId);
        var startStation = _model.GetStation(segment.StartStationId);
        var endStation = _model.GetStation(segment.EndStationId);

        var ratio = segment.LengthMeters <= 0 ? 0 : Math.Clamp(cabin.SegmentPositionMeters / segment.LengthMeters, 0, 1);
        var globalRoutePosition = startStation.RoutePositionMeters + cabin.SegmentPositionMeters;
        var altitude = startStation.AltitudeMeters + ((endStation.AltitudeMeters - startStation.AltitudeMeters) * ratio);

        return new CabinSnapshot(
            cabin.Id,
            cabin.Code,
            segment.Name,
            cabin.Direction,
            cabin.OperationalState,
            cabin.AlertLevel,
            cabin.PassengerCount,
            cabin.Capacity,
            cabin.SegmentPositionMeters,
            globalRoutePosition,
            altitude,
            cabin.VelocityMetersPerSecond,
            cabin.IsEmergencyBrakeActive,
            cabin.HasMechanicalFailure,
            cabin.HasElectricalFailure,
            cabin.IsOutOfService,
            cabin.MechanicalHealth * 100.0,
            cabin.ElectricalHealth * 100.0,
            cabin.BrakeHealth * 100.0);
    }

    private static StationSnapshot CreateStationSnapshot(Station station)
    {
        return new StationSnapshot(
            station.Id,
            station.Code,
            station.Name,
            station.AltitudeMeters,
            station.RoutePositionMeters,
            station.WaitingAscendingPassengers,
            station.WaitingDescendingPassengers,
            station.AllowsAscendingBoarding,
            station.AllowsDescendingBoarding);
    }

    private IReadOnlyList<StationReportEntry> BuildStationReportEntries()
    {
        return _model.Stations
            .OrderBy(station => station.RoutePositionMeters)
            .Select(station =>
            {
                var stats = _stationStats[station.Id];
                return new StationReportEntry
                {
                    Name = station.Name,
                    BoardingRules = station.IsLowerTerminal
                        ? "Solo ascenso"
                        : station.IsUpperTerminal
                            ? "Solo descenso"
                            : "Ascenso / descenso",
                    AltitudeMeters = station.AltitudeMeters,
                    BoardedAscending = stats.BoardedAscending,
                    BoardedDescending = stats.BoardedDescending,
                    UnloadedPassengers = stats.UnloadedPassengers,
                    GeneratedAscendingQueue = stats.GeneratedAscendingQueue,
                    GeneratedDescendingQueue = stats.GeneratedDescendingQueue,
                    PeakQueue = stats.PeakQueue,
                    FinalQueue = station.WaitingAscendingPassengers + station.WaitingDescendingPassengers,
                    LeftWaitingPassengers = stats.LeftWaitingPassengers
                };
            })
            .ToList();
    }

    private IReadOnlyList<CabinReportEntry> BuildCabinReportEntries()
    {
        return _model.Cabins
            .OrderBy(cabin => cabin.Id)
            .Select(cabin =>
            {
                var stats = _cabinStats[cabin.Id];
                return new CabinReportEntry
                {
                    Code = cabin.Code,
                    SegmentName = _model.GetSegment(cabin.AssignedSegmentId).Name,
                    PeakAlertLevel = stats.PeakAlertLevel.ToDisplayText(),
                    CompletedTrips = stats.CompletedTrips,
                    DistanceTravelledMeters = stats.DistanceTravelledMeters,
                    BoardedPassengers = stats.BoardedPassengers,
                    UnloadedPassengers = stats.UnloadedPassengers,
                    PeakOccupancyPercent = stats.PeakOccupancyPercent,
                    EmergencyBrakeActivations = stats.EmergencyBrakeActivations,
                    OutOfServiceMinutes = stats.OutOfServiceDuration.TotalMinutes,
                    MechanicalHealthPercent = cabin.MechanicalHealth * 100.0,
                    ElectricalHealthPercent = cabin.ElectricalHealth * 100.0,
                    BrakeHealthPercent = cabin.BrakeHealth * 100.0
                };
            })
            .ToList();
    }

    private string BuildExecutiveSummary(double averageRisk, double averageOccupancy)
    {
        if (OperationalState == SystemOperationalState.EmergencyStop)
        {
            return $"La corrida terminó en parada de emergencia tras alcanzar un riesgo pico de {_peakRiskScore:F1}/100. Se procesaron {_processedPassengers} pasajeros y la ocupación media fue de {averageOccupancy:F1}%.";
        }

        return $"La jornada {(_elapsed >= _options.ServiceDuration ? "cerró" : "avanzó")} con estado final '{OperationalState.ToDisplayText()}'. El riesgo promedio fue de {averageRisk:F1}/100, el riesgo máximo alcanzó {_peakRiskScore:F1}/100 y se procesaron {_processedPassengers} pasajeros.";
    }

    private string BuildConclusions(double averageRisk)
    {
        if (OperationalState == SystemOperationalState.EmergencyStop)
        {
            return "La simulación evidencia que la combinación de presión operacional, clima y fallas encadenadas puede superar la capacidad de contención. La prioridad futura debe concentrarse en redundancia eléctrica, control de separación y recuperación operativa.";
        }

        if (averageRisk >= 70)
        {
            return "Aunque la jornada pudo completarse, el nivel medio de riesgo fue alto. Se recomienda revisar la política de capacidad, ventanas de operación en clima severo y protocolos de retiro de cabinas.";
        }

        if (_eventTimeline.Any(item => item.Severity >= EventSeverity.Critical))
        {
            return "La jornada mantuvo continuidad, pero registró incidentes severos. El sistema mostró resiliencia moderada con costos operativos perceptibles.";
        }

        return "La corrida fue consistente con una operación estable: la mayoría de la presión se explicó por demanda, clima normal y transferencias propias del sistema.";
    }

    private sealed class PendingSimulationAction : IComparable<PendingSimulationAction>
    {
        public PendingSimulationAction(long sequence, TimeSpan dueAt, int priority, string description, Action execute)
        {
            Sequence = sequence;
            DueAt = dueAt;
            Priority = priority;
            Description = description;
            Execute = execute;
        }

        public long Sequence { get; }

        public TimeSpan DueAt { get; }

        public int Priority { get; }

        public string Description { get; }

        public Action Execute { get; }

        public int CompareTo(PendingSimulationAction? other)
        {
            if (other is null)
            {
                return -1;
            }

            var dueComparison = DueAt.CompareTo(other.DueAt);
            if (dueComparison != 0)
            {
                return dueComparison;
            }

            var priorityComparison = Priority.CompareTo(other.Priority);
            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            return Sequence.CompareTo(other.Sequence);
        }
    }

    private sealed class StationOperationalStats
    {
        public int BoardedAscending { get; set; }

        public int BoardedDescending { get; set; }

        public int UnloadedPassengers { get; set; }

        public int GeneratedAscendingQueue { get; set; }

        public int GeneratedDescendingQueue { get; set; }

        public int PeakQueue { get; set; }

        public int LeftWaitingPassengers { get; set; }
    }

    private sealed class CabinOperationalStats
    {
        public int CompletedTrips { get; set; }

        public double DistanceTravelledMeters { get; set; }

        public int BoardedPassengers { get; set; }

        public int UnloadedPassengers { get; set; }

        public double PeakOccupancyPercent { get; set; }

        public int EmergencyBrakeActivations { get; set; }

        public TimeSpan OutOfServiceDuration { get; set; }

        public CabinAlertLevel PeakAlertLevel { get; set; }
    }
}
