using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Persistence;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Motor principal del simulador.
/// 
/// La idea de esta implementación es dejar una base profesional, determinista
/// y fácilmente extensible:
/// - tiempo fijo por ticks;
/// - física cinemática simplificada pero consistente;
/// - eventos aleatorios reproducibles por semilla;
/// - escenarios guionizados;
/// - snapshot inmutable para UI, pruebas y persistencia futura.
/// </summary>
public sealed class SimulationEngine
{
    private readonly SimulationModel _model;
    private readonly SimulationOptions _options;
    private readonly ScenarioDefinition _scenario;
    private readonly ISimulationSnapshotRepository _repository;
    private readonly Random _random;
    private readonly RollingMetricSeries _riskSeries;
    private readonly RollingMetricSeries _occupancySeries;
    private readonly RollingMetricSeries _weatherSeries;
    private readonly List<SimulationEvent> _eventHistory = new();
    private readonly Dictionary<string, TimeSpan> _cooldowns = new();
    private readonly HashSet<int> _consumedScenarioIndices = new();

    private int _tickIndex;
    private TimeSpan _elapsed;
    private double _currentRiskScore;
    private int _processedPassengers;
    private int _activeCriticalIssues;
    private double _severityBudget;
    private long _eventSequence;
    private bool _systemWidePowerOutage;
    private TimeSpan _powerOutageRemaining;
    private bool _accidentTriggered;

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
        _random = new Random(options.RandomSeed);
        _riskSeries = new RollingMetricSeries(options.TelemetryCapacity);
        _occupancySeries = new RollingMetricSeries(options.TelemetryCapacity);
        _weatherSeries = new RollingMetricSeries(options.TelemetryCapacity);

        OperationalState = SystemOperationalState.Ready;
        CurrentSnapshot = CreateSnapshot("Sistema inicializado y listo para ejecutar.");
    }

    public SimulationModel Model => _model;

    public SimulationOptions Options => _options;

    public ScenarioDefinition Scenario => _scenario;

    public SystemOperationalState OperationalState { get; private set; }

    public SimulationSnapshot CurrentSnapshot { get; private set; }

    /// <summary>
    /// Coloca el motor en estado de ejecución.
    /// </summary>
    public SimulationSnapshot Start()
    {
        if (OperationalState != SystemOperationalState.EmergencyStop)
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
        if (OperationalState != SystemOperationalState.EmergencyStop)
        {
            OperationalState = SystemOperationalState.Paused;
            CurrentSnapshot = CreateSnapshot("Simulación en pausa.");
        }

        return CurrentSnapshot;
    }

    /// <summary>
    /// Avanza exactamente un tick fijo.
    /// </summary>
    public SimulationSnapshot Step()
    {
        if (OperationalState == SystemOperationalState.EmergencyStop ||
            OperationalState == SystemOperationalState.Completed)
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

    private void ExecuteTickCore()
    {
        var delta = _options.FixedTimeStep;

        _tickIndex++;
        _elapsed += delta;

        UpdatePowerGridState(delta);
        UpdateWeather();
        UpdatePassengerDemand();
        ProcessScheduledIncidents();
        ProcessRandomIncidents();
        ProcessCabinMotion(delta);
        RecalculateRiskAndTelemetry();
        EvaluateSafetyRules(delta);
        UpdateOperationalState();

        var narrative = ComposeNarrative();
        CurrentSnapshot = CreateSnapshot(narrative);
        _repository.Save(CurrentSnapshot);
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
    }

    private void UpdateWeather()
    {
        if (!_options.EnableWeatherSystem)
        {
            return;
        }

        // El clima no necesita cambiar cada 100 ms; basta con una transición cada segundo.
        if (_tickIndex % 10 != 0)
        {
            return;
        }

        var currentCondition = _model.WeatherState.Condition;
        var nextCondition = ResolveNextCondition(currentCondition);

        ApplyWeatherCondition(nextCondition);

        if ((nextCondition == WeatherCondition.Snow || nextCondition == WeatherCondition.Storm) &&
            nextCondition != currentCondition &&
            TryAcquireCooldown($"weather-{nextCondition}", TimeSpan.FromSeconds(12)))
        {
            EmitEvent(
                SimulationEventType.ExtremeWeather,
                nextCondition == WeatherCondition.Storm ? EventSeverity.Critical : EventSeverity.Warning,
                nextCondition == WeatherCondition.Storm ? "Tormenta en altura" : "Nieve y visibilidad reducida",
                $"El sistema detecta transición climática hacia {nextCondition}. Se aplican restricciones automáticas de velocidad.",
                nextCondition == WeatherCondition.Storm ? 18 : 10,
                "Clima");
        }
    }

    private WeatherCondition ResolveNextCondition(WeatherCondition currentCondition)
    {
        var shouldShift = _random.NextDouble() < 0.18 * _model.DayProfile.WeatherVolatility;
        if (!shouldShift)
        {
            return currentCondition;
        }

        var roll = _random.NextDouble();

        return currentCondition switch
        {
            WeatherCondition.Clear => roll < 0.55 ? WeatherCondition.Cold
                : roll < 0.85 ? WeatherCondition.Windy
                : roll < 0.95 ? WeatherCondition.Snow
                : WeatherCondition.Storm,

            WeatherCondition.Cold => roll < 0.30 ? WeatherCondition.Clear
                : roll < 0.65 ? WeatherCondition.Windy
                : roll < 0.90 ? WeatherCondition.Snow
                : WeatherCondition.Storm,

            WeatherCondition.Windy => roll < 0.25 ? WeatherCondition.Clear
                : roll < 0.48 ? WeatherCondition.Cold
                : roll < 0.78 ? WeatherCondition.Snow
                : WeatherCondition.Storm,

            WeatherCondition.Snow => roll < 0.20 ? WeatherCondition.Cold
                : roll < 0.45 ? WeatherCondition.Windy
                : roll < 0.78 ? WeatherCondition.Snow
                : WeatherCondition.Storm,

            WeatherCondition.Storm => roll < 0.22 ? WeatherCondition.Windy
                : roll < 0.44 ? WeatherCondition.Snow
                : roll < 0.65 ? WeatherCondition.Cold
                : WeatherCondition.Storm,

            _ => currentCondition,
        };
    }

    private void ApplyWeatherCondition(WeatherCondition condition)
    {
        switch (condition)
        {
            case WeatherCondition.Clear:
                _model.WeatherState.Apply(condition, 4 + _random.NextDouble() * 2, 13 + _random.NextDouble() * 2, 1.00, 1.00, 1.00);
                break;
            case WeatherCondition.Cold:
                _model.WeatherState.Apply(condition, 6 + _random.NextDouble() * 2, 8 + _random.NextDouble() * 3, 0.95, 0.95, 1.08);
                break;
            case WeatherCondition.Windy:
                _model.WeatherState.Apply(condition, 9 + _random.NextDouble() * 5, 6 + _random.NextDouble() * 3, 0.88, 0.84, 1.22);
                break;
            case WeatherCondition.Snow:
                _model.WeatherState.Apply(condition, 7 + _random.NextDouble() * 5, -1 + _random.NextDouble() * 4, 0.72, 0.68, 1.35);
                break;
            case WeatherCondition.Storm:
                _model.WeatherState.Apply(condition, 14 + _random.NextDouble() * 8, 1 + _random.NextDouble() * 4, 0.55, 0.52, 1.60);
                break;
        }
    }

    private void UpdatePassengerDemand()
    {
        if (!_options.EnableRandomDemand)
        {
            return;
        }

        // Se agregan pasajeros en ráfagas cortas, no en cada tick.
        if (_tickIndex % 5 != 0)
        {
            return;
        }

        var serviceClock = _options.ServiceStartTime + _elapsed;
        var wave = 0.95 + 0.25 * Math.Sin((serviceClock.TotalHours - 8.0) / 4.0 * Math.PI);

        foreach (var station in _model.Stations)
        {
            var baseDemand = station.DemandWeight * _model.DayProfile.DemandMultiplier * _options.DemandMultiplier * wave;
            var randomFactor = 0.65 + _random.NextDouble() * 0.85;

            var ascending = (int)Math.Round(baseDemand * randomFactor);
            var descending = (int)Math.Round(baseDemand * (0.45 + _random.NextDouble() * 0.55));

            if (station.RoutePositionMeters <= 3300)
            {
                ascending = (int)Math.Round(ascending * 1.45);
            }
            else if (station.RoutePositionMeters >= 9365)
            {
                descending = (int)Math.Round(descending * 1.55);
            }

            station.EnqueuePassengers(ascending, descending);
        }
    }

    private void ProcessScheduledIncidents()
    {
        if (_options.Mode != SimulationMode.ScriptedScenario || _scenario.ScheduledIncidents.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _scenario.ScheduledIncidents.Count; index++)
        {
            if (_consumedScenarioIndices.Contains(index))
            {
                continue;
            }

            var incident = _scenario.ScheduledIncidents[index];
            if (_elapsed < incident.TriggerAt)
            {
                continue;
            }

            ApplyScheduledIncident(incident);
            _consumedScenarioIndices.Add(index);
        }
    }

    private void ApplyScheduledIncident(ScheduledIncident incident)
    {
        switch (incident.EventType)
        {
            case SimulationEventType.Overload:
            {
                var cabin = _model.GetCabin(incident.CabinId ?? 1);
                cabin.SetPassengers(cabin.Capacity + _random.Next(6, 18));
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
                var cabin = _model.GetCabin(incident.CabinId ?? 1);
                cabin.ApplyMechanicalDamage(0.35 + _random.NextDouble() * 0.20, true, TimeSpan.FromSeconds(20 + _random.Next(0, 12)));
                cabin.ApplyBrakeDamage(0.05 + _random.NextDouble() * 0.08);
                cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(8));
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
                    cabin.MarkOutOfService(TimeSpan.FromSeconds(24));
                    EmitEvent(
                        SimulationEventType.CabinOutOfService,
                        EventSeverity.Critical,
                        "Cabina fuera de servicio",
                        $"La {cabin.Code} sale temporalmente de servicio para contener la falla programada.",
                        16,
                        incident.SourceTag,
                        cabin: cabin,
                        segment: _model.GetSegment(cabin.AssignedSegmentId));
                }

                break;
            }

            case SimulationEventType.ElectricalFailure:
            {
                if (incident.CabinId is null)
                {
                    _systemWidePowerOutage = true;
                    _powerOutageRemaining = TimeSpan.FromSeconds(18);

                    foreach (var cabin in _model.Cabins)
                    {
                        cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(8));
                    }

                    EmitEvent(
                        incident.EventType,
                        incident.Severity,
                        incident.Title,
                        incident.Description,
                        incident.RiskDelta,
                        incident.SourceTag,
                        requiresEmergencyStop: incident.RequiresEmergencyStop);

                    EmitEvent(
                        SimulationEventType.EmergencyBrake,
                        EventSeverity.Warning,
                        "Frenado preventivo",
                        "Todas las cabinas activan frenado preventivo mientras se estabiliza la energía.",
                        12,
                        incident.SourceTag);
                }
                else
                {
                    var cabin = _model.GetCabin(incident.CabinId.Value);
                    cabin.ApplyElectricalDamage(0.40 + _random.NextDouble() * 0.15, true, TimeSpan.FromSeconds(16));
                    cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(8));

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
                EmitEvent(
                    incident.EventType,
                    incident.Severity,
                    incident.Title,
                    incident.Description,
                    incident.RiskDelta,
                    incident.SourceTag);

                if (incident.Severity >= EventSeverity.Critical)
                {
                    foreach (var cabin in _model.Cabins.Where(item => item.AssignedSegmentId >= 3))
                    {
                        cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(6));
                    }
                }

                break;
            }

            case SimulationEventType.EmergencyBrake:
            {
                if (incident.CabinId is int specificCabinId)
                {
                    var cabin = _model.GetCabin(specificCabinId);
                    cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(8));
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
                        cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(8));
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
                var cabin = _model.GetCabin(incident.CabinId ?? 1);
                cabin.MarkOutOfService(TimeSpan.FromSeconds(25));
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

    private void ProcessRandomIncidents()
    {
        var weatherFactor = _model.WeatherState.RiskMultiplier;
        var pressure = _model.DayProfile.IncidentPressure * _options.RandomIncidentMultiplier;

        // Fallas eléctricas sistémicas poco frecuentes pero relevantes.
        if (!_systemWidePowerOutage &&
            !_accidentTriggered &&
            _random.NextDouble() < 0.00018 * pressure * weatherFactor &&
            TryAcquireCooldown("grid-blackout", TimeSpan.FromSeconds(25)))
        {
            _systemWidePowerOutage = true;
            _powerOutageRemaining = TimeSpan.FromSeconds(15 + _random.Next(0, 10));

            foreach (var cabin in _model.Cabins)
            {
                cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(8));
            }

            EmitEvent(
                SimulationEventType.ElectricalFailure,
                EventSeverity.Critical,
                "Falla eléctrica general",
                "La red principal de energía entra en falla y el sistema reduce operación automáticamente.",
                26,
                "Aleatorio");

            EmitEvent(
                SimulationEventType.EmergencyBrake,
                EventSeverity.Warning,
                "Frenado automático",
                "La estrategia de protección desacelera las cabinas mientras actúa el protocolo eléctrico.",
                12,
                "Seguridad");
        }

        foreach (var cabin in _model.Cabins)
        {
            if (cabin.IsOutOfService)
            {
                continue;
            }

            if (cabin.IsOverloaded && TryAcquireCooldown($"overload-{cabin.Id}", TimeSpan.FromSeconds(12)))
            {
                EmitEvent(
                    SimulationEventType.Overload,
                    EventSeverity.Warning,
                    $"Sobrecarga en {cabin.Code}",
                    $"{cabin.Code} supera su capacidad nominal con {cabin.PassengerCount} pasajeros.",
                    12 + (cabin.OccupancyRatio - 1.0) * 10,
                    "Aleatorio",
                    cabin: cabin,
                    segment: _model.GetSegment(cabin.AssignedSegmentId));
            }

            var loadFactor = 0.55 + cabin.OccupancyRatio;
            var mechanicalChance =
                0.00040 * pressure * weatherFactor * loadFactor * (1.0 + (1.0 - cabin.MechanicalHealth) * 1.8);

            if (_random.NextDouble() < mechanicalChance && TryAcquireCooldown($"mechanical-{cabin.Id}", TimeSpan.FromSeconds(18)))
            {
                var severe = _random.NextDouble() < 0.38 || cabin.MechanicalHealth < 0.55;
                cabin.ApplyMechanicalDamage(0.12 + _random.NextDouble() * 0.18, severe, TimeSpan.FromSeconds(16 + _random.Next(0, 12)));
                cabin.ApplyBrakeDamage(0.04 + _random.NextDouble() * 0.06);
                cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(6));

                EmitEvent(
                    SimulationEventType.MechanicalFailure,
                    severe ? EventSeverity.Critical : EventSeverity.Warning,
                    $"Falla mecánica en {cabin.Code}",
                    severe
                        ? $"{cabin.Code} presenta una degradación mecánica que obliga a protección inmediata."
                        : $"{cabin.Code} presenta una anomalía mecánica controlada.",
                    severe ? 24 : 14,
                    "Aleatorio",
                    cabin: cabin,
                    segment: _model.GetSegment(cabin.AssignedSegmentId));

                if (severe && _random.NextDouble() < 0.45)
                {
                    cabin.MarkOutOfService(TimeSpan.FromSeconds(20 + _random.Next(0, 12)));
                    EmitEvent(
                        SimulationEventType.CabinOutOfService,
                        EventSeverity.Critical,
                        $"Retiro temporal de {cabin.Code}",
                        $"La cabina {cabin.Code} queda fuera de servicio mientras se contiene la desviación mecánica.",
                        16,
                        "Seguridad",
                        cabin: cabin,
                        segment: _model.GetSegment(cabin.AssignedSegmentId));
                }
            }

            var cabinElectricalChance =
                0.00016 * pressure * weatherFactor * (0.8 + (1.0 - cabin.ElectricalHealth) * 2.0);

            if (_random.NextDouble() < cabinElectricalChance && TryAcquireCooldown($"electrical-{cabin.Id}", TimeSpan.FromSeconds(18)))
            {
                cabin.ApplyElectricalDamage(0.14 + _random.NextDouble() * 0.15, true, TimeSpan.FromSeconds(12 + _random.Next(0, 10)));
                cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(6));

                EmitEvent(
                    SimulationEventType.ElectricalFailure,
                    EventSeverity.Warning,
                    $"Anomalía eléctrica en {cabin.Code}",
                    $"{cabin.Code} detecta una inestabilidad eléctrica local y desacelera de forma protectiva.",
                    13,
                    "Aleatorio",
                    cabin: cabin,
                    segment: _model.GetSegment(cabin.AssignedSegmentId));
            }
        }
    }

    private void ProcessCabinMotion(TimeSpan delta)
    {
        foreach (var fleet in _model.SegmentFleets.Values.OrderBy(item => item.Segment.VisualOrder))
        {
            fleet.RebuildDispatchRing();

            foreach (var cabin in fleet.DispatchRing.EnumerateDispatchOrder())
            {
                UpdateCabinMotion(fleet, cabin, delta);
            }

            fleet.DispatchRing.Rotate();
        }
    }

    private void UpdateCabinMotion(SegmentFleet fleet, Cabin cabin, TimeSpan delta)
    {
        var segment = fleet.Segment;
        var dt = delta.TotalSeconds;

        cabin.AdvanceFaultTimers(delta);

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
            return;
        }

        var targetSpeed = CalculateTargetSpeed(cabin, segment);
        var distanceToArrival = cabin.Direction == TravelDirection.Ascending
            ? segment.LengthMeters - cabin.SegmentPositionMeters
            : cabin.SegmentPositionMeters;

        var brakingDistance = cabin.VelocityMetersPerSecond * cabin.VelocityMetersPerSecond /
            (2 * Math.Max(0.05, segment.ServiceDecelerationMetersPerSecondSquared));

        double acceleration;
        CabinOperationalState newState;

        if (distanceToArrival <= Math.Max(12, brakingDistance + 6))
        {
            acceleration = -segment.ServiceDecelerationMetersPerSecondSquared;
            newState = CabinOperationalState.Braking;
        }
        else if (cabin.VelocityMetersPerSecond < targetSpeed - 0.10)
        {
            acceleration = segment.ServiceAccelerationMetersPerSecondSquared;
            newState = CabinOperationalState.Accelerating;
        }
        else if (cabin.VelocityMetersPerSecond > targetSpeed + 0.10)
        {
            acceleration = -segment.ServiceDecelerationMetersPerSecondSquared;
            newState = CabinOperationalState.Braking;
        }
        else
        {
            acceleration = 0;
            newState = CabinOperationalState.Cruising;
        }

        var nextVelocity = Math.Clamp(cabin.VelocityMetersPerSecond + acceleration * dt, 0, targetSpeed);
        var travelDistance = Math.Max(0, (cabin.VelocityMetersPerSecond + nextVelocity) * 0.5 * dt);
        var nextPosition = cabin.Direction == TravelDirection.Ascending
            ? cabin.SegmentPositionMeters + travelDistance
            : cabin.SegmentPositionMeters - travelDistance;

        if (cabin.Direction == TravelDirection.Ascending && nextPosition >= segment.LengthMeters)
        {
            cabin.UpdateMotion(segment.LengthMeters, 0, 0, CabinOperationalState.IdleAtStation);
            ResolveStationArrival(cabin, segment, _model.GetStation(segment.EndStationId));
            return;
        }

        if (cabin.Direction == TravelDirection.Descending && nextPosition <= 0)
        {
            cabin.UpdateMotion(0, 0, 0, CabinOperationalState.IdleAtStation);
            ResolveStationArrival(cabin, segment, _model.GetStation(segment.StartStationId));
            return;
        }

        cabin.UpdateMotion(nextPosition, nextVelocity, acceleration, newState);
    }

    private void ApplyEmergencyBraking(Cabin cabin, TrackSegment segment, double dt)
    {
        if (!cabin.IsEmergencyBrakeActive)
        {
            cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(4));
        }

        var emergencyDeceleration = segment.EmergencyDecelerationMetersPerSecondSquared * Math.Max(0.45, cabin.BrakeHealth);
        var nextVelocity = Math.Max(0, cabin.VelocityMetersPerSecond - emergencyDeceleration * dt);
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

    private double CalculateTargetSpeed(Cabin cabin, TrackSegment segment)
    {
        var weatherPenalty = 1.0 - ((1.0 - _model.WeatherState.SpeedMultiplier) * segment.WeatherExposureFactor);
        weatherPenalty = Math.Clamp(weatherPenalty, 0.35, 1.0);

        var healthPenalty = Math.Clamp((cabin.MechanicalHealth + cabin.ElectricalHealth + cabin.BrakeHealth) / 3.0, 0.45, 1.0);
        var loadPenalty = cabin.IsOverloaded ? 0.82 : (cabin.OccupancyRatio > 0.85 ? 0.92 : 1.0);

        return segment.MaxOperationalSpeedMetersPerSecond * weatherPenalty * healthPenalty * loadPenalty;
    }

    private void ResolveStationArrival(Cabin cabin, TrackSegment segment, Station arrivalStation)
    {
        _processedPassengers += cabin.UnloadAllPassengers();
        cabin.StartStationStop(arrivalStation.DefaultDwellTime);

        // La cabina invierte sentido porque cada tramo se modela como ida y vuelta.
        cabin.ReverseDirection();

        if (cabin.IsOutOfService || cabin.HasElectricalFailure || cabin.HasMechanicalFailure)
        {
            return;
        }

        var availableCapacity = cabin.Capacity - cabin.PassengerCount;
        var boardedPassengers = cabin.Direction == TravelDirection.Ascending
            ? arrivalStation.DequeueAscendingPassengers(availableCapacity)
            : arrivalStation.DequeueDescendingPassengers(availableCapacity);

        cabin.BoardPassengers(boardedPassengers);
    }

    private void EvaluateSafetyRules(TimeSpan delta)
    {
        var separationViolation = false;

        foreach (var fleet in _model.SegmentFleets.Values)
        {
            var orderedCabins = fleet.GetCabinsOrderedByTrackPosition();
            for (var index = 0; index < orderedCabins.Count - 1; index++)
            {
                var first = orderedCabins[index];
                var second = orderedCabins[index + 1];
                var gap = second.SegmentPositionMeters - first.SegmentPositionMeters;

                if (gap < fleet.Segment.MinimumSeparationMeters &&
                    TryAcquireCooldown($"separation-{fleet.Segment.Id}", TimeSpan.FromSeconds(8)))
                {
                    separationViolation = true;

                    first.ActivateEmergencyBrake(TimeSpan.FromSeconds(6));
                    second.ActivateEmergencyBrake(TimeSpan.FromSeconds(6));

                    EmitEvent(
                        SimulationEventType.SeparationLoss,
                        gap < fleet.Segment.MinimumSeparationMeters * 0.5 ? EventSeverity.Critical : EventSeverity.Warning,
                        "Pérdida de separación segura",
                        $"El tramo {fleet.Segment.Name} reduce su distancia operativa segura entre {first.Code} y {second.Code}.",
                        gap < fleet.Segment.MinimumSeparationMeters * 0.5 ? 26 : 14,
                        "Seguridad",
                        cabin: first,
                        segment: fleet.Segment);
                }
            }
        }

        var criticalRuleBreach =
            separationViolation ||
            (_systemWidePowerOutage && _model.WeatherState.Condition == WeatherCondition.Storm) ||
            _model.Cabins.Any(cabin => cabin.IsOverloaded && cabin.HasMechanicalFailure) ||
            _model.Cabins.Count(cabin => cabin.IsOutOfService) >= 2;

        if (criticalRuleBreach)
        {
            _severityBudget += delta.TotalSeconds * (1.5 + _activeCriticalIssues * 0.65);
        }
        else
        {
            _severityBudget = Math.Max(0, _severityBudget - delta.TotalSeconds * 1.3);
        }

        if (_options.EnableSafetyEscalation &&
            !_accidentTriggered &&
            criticalRuleBreach &&
            _currentRiskScore >= 78 &&
            _severityBudget >= 10)
        {
            TriggerAccident(
                "Accidente operacional simulado",
                "Se superó el umbral de severidad acumulada: regla crítica vulnerada + riesgo elevado + consecuencia operacional real.",
                "Seguridad");
        }
    }

    private void TriggerAccident(string title, string description, string sourceTag)
    {
        _accidentTriggered = true;
        _currentRiskScore = 100;

        foreach (var cabin in _model.Cabins)
        {
            cabin.ActivateEmergencyBrake(TimeSpan.FromSeconds(15));
            if (_random.NextDouble() < 0.35)
            {
                cabin.MarkOutOfService(TimeSpan.FromSeconds(40));
            }
        }

        EmitEvent(
            SimulationEventType.Accident,
            EventSeverity.Catastrophic,
            title,
            description,
            40,
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
            if (cabin.IsOverloaded)
            {
                risk += 12 + Math.Max(0, cabin.OccupancyRatio - 1.0) * 16;
                criticalIssues++;
            }

            if (cabin.HasMechanicalFailure)
            {
                risk += 18 * (1.1 + (1.0 - cabin.MechanicalHealth));
                criticalIssues++;
            }

            if (cabin.HasElectricalFailure)
            {
                risk += 15 * (1.0 + (1.0 - cabin.ElectricalHealth));
                criticalIssues++;
            }

            if (cabin.IsEmergencyBrakeActive)
            {
                risk += 8;
            }

            if (cabin.IsOutOfService)
            {
                risk += 14;
                criticalIssues++;
            }
        }

        if (_systemWidePowerOutage)
        {
            risk += 18;
            criticalIssues++;
        }

        if (_model.WeatherState.Condition is WeatherCondition.Snow or WeatherCondition.Storm)
        {
            risk += (_model.WeatherState.RiskMultiplier - 1.0) * 20;
            criticalIssues++;
        }

        var averageQueue = _model.Stations.Average(station => station.WaitingAscendingPassengers + station.WaitingDescendingPassengers);
        risk += Math.Min(10, averageQueue / 12.0);

        _currentRiskScore = Math.Clamp(risk, 0, 100);
        _activeCriticalIssues = criticalIssues;

        var averageOccupancyPercent = _model.Cabins.Count == 0
            ? 0
            : _model.Cabins.Average(cabin => cabin.OccupancyRatio) * 100.0;

        _riskSeries.Add(_elapsed, _currentRiskScore);
        _occupancySeries.Add(_elapsed, averageOccupancyPercent);
        _weatherSeries.Add(_elapsed, (_model.WeatherState.RiskMultiplier - 1.0) * 100.0);
    }

    private void UpdateOperationalState()
    {
        if (OperationalState == SystemOperationalState.EmergencyStop)
        {
            return;
        }

        if (_activeCriticalIssues > 0 || _systemWidePowerOutage)
        {
            OperationalState = SystemOperationalState.Degraded;
        }
        else
        {
            OperationalState = SystemOperationalState.Running;
        }
    }

    private string ComposeNarrative()
    {
        if (OperationalState == SystemOperationalState.EmergencyStop)
        {
            return "El sistema entró en parada de emergencia por escalamiento de riesgo.";
        }

        if (_systemWidePowerOutage)
        {
            return "Operación degradada: corte eléctrico activo y frenado de protección en ejecución.";
        }

        if (_model.WeatherState.Condition == WeatherCondition.Storm)
        {
            return "Operación restringida por tormenta en cotas altas. El motor reduce velocidad y prioriza seguridad.";
        }

        if (_model.Cabins.Any(cabin => cabin.IsOutOfService))
        {
            return "Operación degradada: una o más cabinas están temporalmente fuera de servicio.";
        }

        if (_model.Cabins.Any(cabin => cabin.IsOverloaded))
        {
            return "Atención operativa: se detectan niveles altos de ocupación en la flota.";
        }

        return "Operación estable. El sistema mantiene movimiento, boarding y monitoreo continuo.";
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

        _eventHistory.Add(simulationEvent);

        if (_eventHistory.Count > 150)
        {
            _eventHistory.RemoveRange(0, _eventHistory.Count - 150);
        }

        return simulationEvent;
    }

    private SimulationSnapshot CreateSnapshot(string narrative)
    {
        var cabinSnapshots = _model.Cabins
            .OrderBy(cabin => cabin.AssignedSegmentId)
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
            _options.Mode,
            _scenario.Name,
            _model.DayProfile.Name,
            OperationalState,
            narrative,
            _model.WeatherState.ToDisplayText(),
            _model.WeatherState.Condition,
            _currentRiskScore,
            cabinSnapshots.Count == 0 ? 0 : cabinSnapshots.Average(snapshot => snapshot.OccupancyPercent),
            _processedPassengers,
            _activeCriticalIssues,
            cabinSnapshots,
            stationSnapshots,
            _eventHistory.TakeLast(60).Reverse().ToList(),
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
            cabin.PassengerCount,
            cabin.Capacity,
            cabin.SegmentPositionMeters,
            globalRoutePosition,
            altitude,
            cabin.VelocityMetersPerSecond,
            cabin.IsEmergencyBrakeActive,
            cabin.HasMechanicalFailure,
            cabin.HasElectricalFailure,
            cabin.IsOutOfService);
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
            station.WaitingDescendingPassengers);
    }
}
