using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Simulation;
using HighRiskSimulator.Helpers;
using HighRiskSimulator.Models;
using HighRiskSimulator.Services;

namespace HighRiskSimulator.ViewModels;

/// <summary>
/// ViewModel principal de la aplicación.
/// </summary>
public sealed class MainViewModel : BaseViewModel
{
    private readonly SimulationSessionService _sessionService;
    private readonly SimulationReportExportService _reportExportService;
    private readonly DispatcherTimer _simulationTimer;

    private SimulationEngine _engine = null!;
    private SimulationRunReport? _lastRunReport;
    private SelectionOption _selectedMode = null!;
    private SelectionOption _selectedScenario = null!;
    private SelectionOption _selectedPressureMode = null!;
    private SelectionOption _selectedTimeScale = null!;
    private SelectionOption _selectedCabinDensity = null!;
    private SelectionOption _selectedInjectionTarget = null!;
    private DateTime? _selectedSimulationDate = DateTime.Today;
    private string _randomSeed = "20260323";
    private bool _isRunning;
    private bool _isBusy;
    private bool _isOperatorPanelExpanded = true;
    private double _currentRiskValue;
    private double _manualTimeScale = 1.0;
    private string _manualTimeScaleText = "1.0x";
    private double _serviceDurationHours = 10.0;
    private string _serviceDurationHoursText = "10 h";
    private double _passengerArrivalMultiplier = 1.0;
    private string _passengerArrivalMultiplierText = "1.0x";
    private string _narrativeText = "Sistema listo.";
    private string _operationalStateText = "Listo";
    private string _elapsedText = "00:00";
    private string _currentWeatherText = "-";
    private string _processedPassengersText = "0";
    private string _currentRiskText = "0 / 100";
    private string _averageOccupancyText = "0 %";
    private string _activeIncidentText = "0";
    private string _currentDayProfileText = "-";
    private string _scenarioDescriptionText = "-";
    private string _seasonalityText = "-";
    private string _visibilityText = "0 %";
    private string _icingText = "0 %";
    private string _pressureModeText = "-";
    private string _simulationDateText = "-";
    private string _operationalVarianceSeedText = "-";
    private string _stationTelemetrySummaryText = "Sin telemetría de estaciones.";
    private string _riskCalibrationSummaryText = "Configuración base.";
    private string _exportStatusText = "Sin exportaciones recientes.";
    private string _lastExportPdfPath = string.Empty;
    private string _lastExportJsonPath = string.Empty;
    private double _globalRiskMultiplier = 1.00;
    private double _stormProbabilityMultiplier = 1.00;
    private double _windProbabilityMultiplier = 1.00;
    private double _fogProbabilityMultiplier = 1.00;
    private double _mechanicalWearProbabilityMultiplier = 1.00;
    private double _cabinMechanicalFailureProbabilityMultiplier = 1.00;
    private double _powerOutageProbabilityMultiplier = 1.00;
    private double _voltageSpikeProbabilityMultiplier = 1.00;

    public MainViewModel()
    {
        _sessionService = new SimulationSessionService();
        _reportExportService = new SimulationReportExportService();
        _simulationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
        _simulationTimer.Tick += SimulationTimerOnTick;

        SimulationModes = new ObservableCollection<SelectionOption>
        {
            new("random", "Aleatorio inteligente", "Modo no guionizado con comportamiento estocástico realista."),
            new("scripted", "Escenario específico", "Modo para reproducir incidentes preparados y validar protocolos.")
        };

        ScenarioOptions = new ObservableCollection<SelectionOption>(
            _sessionService.GetScriptedScenarios()
                .Select(item => new SelectionOption(item.Id, item.Name, item.Description)));

        PressureModeOptions = new ObservableCollection<SelectionOption>
        {
            new("realistic", "Operación realista", "La mayoría de las jornadas deben poder terminar sin incidentes severos."),
            new("training", "Entrenamiento intensificado", "Eleva la presión operacional para practicar contingencias.")
        };

        TimeScaleOptions = new ObservableCollection<SelectionOption>
        {
            new("1", "1x", "Ritmo normal de simulación."),
            new("2", "2x", "Observación acelerada."),
            new("5", "5x", "Iteración corta sin perder lectura."),
            new("10", "10x", "Validación rápida de tendencias."),
            new("20", "20x", "Ensayo acelerado de jornada."),
            new("50", "50x", "Compresión máxima para cierres instantáneos.")
        };

        CabinDensityOptions = new ObservableCollection<SelectionOption>
        {
            new("1", "1 cabina por sentido", "Configuración base alineada al esquema público de operación por tramo."),
            new("2", "2 cabinas por sentido", "Modo académico para estrés moderado y validación de separación."),
            new("3", "3 cabinas por sentido", "Modo intensivo para pruebas de congestión y capacidad.")
        };

        InjectionTargetOptions = new ObservableCollection<SelectionOption>();
        EventLog = new ObservableCollection<SimulationEvent>();
        CabinStates = new ObservableCollection<CabinStateRowViewModel>();
        StationStates = new ObservableCollection<StationStateRowViewModel>();
        ToastNotifications = new ObservableCollection<ToastNotification>();

        _selectedMode = SimulationModes.First();
        _selectedScenario = ScenarioOptions.First();
        _selectedPressureMode = PressureModeOptions.First();
        _selectedTimeScale = TimeScaleOptions.First();
        _selectedCabinDensity = CabinDensityOptions.First();
        _selectedInjectionTarget = new SelectionOption("system", "Sistema completo", "Aplicable a inyecciones de alcance general.");

        StartCommand = new RelayCommand(StartSimulation, CanStartSimulation);
        PauseCommand = new RelayCommand(PauseSimulation, CanPauseSimulation);
        StepCommand = new RelayCommand(StepSimulation, CanStepSimulation);
        ResetCommand = new RelayCommand(ResetSimulation, CanResetSimulation);
        InstantSimulationCommand = new RelayCommand(RunInstantSimulationFromScratchAsync, CanRunInstantSimulation);
        FastForwardAndExportCommand = new RelayCommand(FastForwardCurrentSimulationAndExportAsync, CanFastForwardCurrentSimulation);
        ExportReportCommand = new RelayCommand(ExportCurrentReportAsync, CanExportCurrentReport);
        ApplyRiskTuningCommand = new RelayCommand(ApplyRiskTuning, CanApplyRiskTuning);
        InjectMechanicalFailureCommand = new RelayCommand(InjectMechanicalFailure, CanInjectCabinTargetedFailure);
        InjectElectricalFailureCommand = new RelayCommand(InjectElectricalFailure, CanInjectGeneralFailure);
        InjectStormCommand = new RelayCommand(InjectStorm, CanInjectGeneralFailure);
        InjectStrongWindCommand = new RelayCommand(InjectStrongWind, CanInjectGeneralFailure);
        InjectFogCommand = new RelayCommand(InjectFog, CanInjectGeneralFailure);
        InjectPulleyWearCommand = new RelayCommand(InjectPulleyWear, CanInjectCabinTargetedFailure);
        InjectVoltageSpikeCommand = new RelayCommand(InjectVoltageSpike, CanInjectGeneralFailure);
        InjectOverloadCommand = new RelayCommand(InjectOverload, CanInjectCabinTargetedFailure);
        InjectEmergencyStopCommand = new RelayCommand(InjectEmergencyStop, CanInjectGeneralFailure);

        ManualTimeScale = ParseSelectedTimeScale();
        UpdateScenarioDescription();
        ResetSimulation();
    }

    public ObservableCollection<SelectionOption> SimulationModes { get; }

    public ObservableCollection<SelectionOption> ScenarioOptions { get; }

    public ObservableCollection<SelectionOption> PressureModeOptions { get; }

    public ObservableCollection<SelectionOption> TimeScaleOptions { get; }

    public ObservableCollection<SelectionOption> CabinDensityOptions { get; }

    public ObservableCollection<SelectionOption> InjectionTargetOptions { get; }

    public ObservableCollection<SimulationEvent> EventLog { get; }

    public ObservableCollection<CabinStateRowViewModel> CabinStates { get; }

    public ObservableCollection<StationStateRowViewModel> StationStates { get; }

    public ObservableCollection<ToastNotification> ToastNotifications { get; }

    public RelayCommand StartCommand { get; }

    public RelayCommand PauseCommand { get; }

    public RelayCommand StepCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand InstantSimulationCommand { get; }

    public RelayCommand FastForwardAndExportCommand { get; }

    public RelayCommand ExportReportCommand { get; }

    public RelayCommand ApplyRiskTuningCommand { get; }

    public RelayCommand InjectMechanicalFailureCommand { get; }

    public RelayCommand InjectElectricalFailureCommand { get; }

    public RelayCommand InjectStormCommand { get; }

    public RelayCommand InjectStrongWindCommand { get; }

    public RelayCommand InjectFogCommand { get; }

    public RelayCommand InjectPulleyWearCommand { get; }

    public RelayCommand InjectVoltageSpikeCommand { get; }

    public RelayCommand InjectOverloadCommand { get; }

    public RelayCommand InjectEmergencyStopCommand { get; }

    public event EventHandler<SimulationSnapshot>? SnapshotUpdated;

    public SelectionOption SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsScriptedMode));
                UpdateScenarioDescription();
            }
        }
    }



    public SelectionOption SelectedScenario
    {
        get => _selectedScenario;
        set
        {
            if (SetProperty(ref _selectedScenario, value))
            {
                UpdateScenarioDescription();
            }
        }
    }

    public SelectionOption SelectedPressureMode
    {
        get => _selectedPressureMode;
        set => SetProperty(ref _selectedPressureMode, value);
    }

    public SelectionOption SelectedTimeScale
    {
        get => _selectedTimeScale;
        set
        {
            if (SetProperty(ref _selectedTimeScale, value))
            {
                ManualTimeScale = ParseSelectedTimeScale();
            }
        }
    }

    public SelectionOption SelectedCabinDensity
    {
        get => _selectedCabinDensity;
        set => SetProperty(ref _selectedCabinDensity, value);
    }

    public SelectionOption SelectedInjectionTarget
    {
        get => _selectedInjectionTarget;
        set
        {
            if (SetProperty(ref _selectedInjectionTarget, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public DateTime? SelectedSimulationDate
    {
        get => _selectedSimulationDate;
        set => SetProperty(ref _selectedSimulationDate, value);
    }

    public bool IsScriptedMode => SelectedMode.Id == "scripted";

    public bool IsOperatorPanelExpanded
    {
        get => _isOperatorPanelExpanded;
        set => SetProperty(ref _isOperatorPanelExpanded, value);
    }

    public string RandomSeed
    {
        get => _randomSeed;
        set => SetProperty(ref _randomSeed, value);
    }

    public string NarrativeText
    {
        get => _narrativeText;
        set => SetProperty(ref _narrativeText, value);
    }

    public string OperationalStateText
    {
        get => _operationalStateText;
        set => SetProperty(ref _operationalStateText, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        set => SetProperty(ref _elapsedText, value);
    }

    public string CurrentWeatherText
    {
        get => _currentWeatherText;
        set => SetProperty(ref _currentWeatherText, value);
    }

    public string ProcessedPassengersText
    {
        get => _processedPassengersText;
        set => SetProperty(ref _processedPassengersText, value);
    }

    public string CurrentRiskText
    {
        get => _currentRiskText;
        set => SetProperty(ref _currentRiskText, value);
    }

    public string AverageOccupancyText
    {
        get => _averageOccupancyText;
        set => SetProperty(ref _averageOccupancyText, value);
    }

    public string ActiveIncidentText
    {
        get => _activeIncidentText;
        set => SetProperty(ref _activeIncidentText, value);
    }

    public string CurrentDayProfileText
    {
        get => _currentDayProfileText;
        set => SetProperty(ref _currentDayProfileText, value);
    }

    public string ScenarioDescriptionText
    {
        get => _scenarioDescriptionText;
        set => SetProperty(ref _scenarioDescriptionText, value);
    }

    public string SeasonalityText
    {
        get => _seasonalityText;
        set => SetProperty(ref _seasonalityText, value);
    }

    public string VisibilityText
    {
        get => _visibilityText;
        set => SetProperty(ref _visibilityText, value);
    }

    public string IcingText
    {
        get => _icingText;
        set => SetProperty(ref _icingText, value);
    }

    public string PressureModeText
    {
        get => _pressureModeText;
        set => SetProperty(ref _pressureModeText, value);
    }

    public string SimulationDateText
    {
        get => _simulationDateText;
        set => SetProperty(ref _simulationDateText, value);
    }

    public string OperationalVarianceSeedText
    {
        get => _operationalVarianceSeedText;
        set => SetProperty(ref _operationalVarianceSeedText, value);
    }

    public string StationTelemetrySummaryText
    {
        get => _stationTelemetrySummaryText;
        set => SetProperty(ref _stationTelemetrySummaryText, value);
    }

    public string RiskCalibrationSummaryText
    {
        get => _riskCalibrationSummaryText;
        set => SetProperty(ref _riskCalibrationSummaryText, value);
    }

    public string ExportStatusText
    {
        get => _exportStatusText;
        set => SetProperty(ref _exportStatusText, value);
    }

    public string LastExportPdfPath
    {
        get => _lastExportPdfPath;
        set => SetProperty(ref _lastExportPdfPath, value);
    }

    public string LastExportJsonPath
    {
        get => _lastExportJsonPath;
        set => SetProperty(ref _lastExportJsonPath, value);
    }

    public double CurrentRiskValue
    {
        get => _currentRiskValue;
        set => SetProperty(ref _currentRiskValue, value);
    }

    public double ManualTimeScale
    {
        get => _manualTimeScale;
        set
        {
            var clamped = Math.Clamp(value, 1.0, 50.0);
            if (SetProperty(ref _manualTimeScale, clamped))
            {
                ManualTimeScaleText = $"{clamped:F1}x";
                if (_engine is not null)
                {
                    _engine.SetTimeScale(clamped);
                }
            }
        }
    }

    public string ManualTimeScaleText
    {
        get => _manualTimeScaleText;
        set => SetProperty(ref _manualTimeScaleText, value);
    }

    public double ServiceDurationHours
    {
        get => _serviceDurationHours;
        set
        {
            var clamped = Math.Clamp(value, 10.0, 16.0);
            if (SetProperty(ref _serviceDurationHours, clamped))
            {
                ServiceDurationHoursText = $"{clamped:F0} h";
                _engine?.SetServiceDurationHours(clamped);
            }
        }
    }

    public string ServiceDurationHoursText
    {
        get => _serviceDurationHoursText;
        set => SetProperty(ref _serviceDurationHoursText, value);
    }

    public double PassengerArrivalMultiplier
    {
        get => _passengerArrivalMultiplier;
        set
        {
            var clamped = Math.Clamp(value, 0.25, 3.0);
            if (SetProperty(ref _passengerArrivalMultiplier, clamped))
            {
                PassengerArrivalMultiplierText = $"{clamped:F1}x";
                _engine?.SetDemandMultiplier(clamped);
            }
        }
    }

    public string PassengerArrivalMultiplierText
    {
        get => _passengerArrivalMultiplierText;
        set => SetProperty(ref _passengerArrivalMultiplierText, value);
    }

    public double GlobalRiskMultiplier
    {
        get => _globalRiskMultiplier;
        set => SetProperty(ref _globalRiskMultiplier, value);
    }

    public double StormProbabilityMultiplier
    {
        get => _stormProbabilityMultiplier;
        set => SetProperty(ref _stormProbabilityMultiplier, value);
    }

    public double WindProbabilityMultiplier
    {
        get => _windProbabilityMultiplier;
        set => SetProperty(ref _windProbabilityMultiplier, value);
    }

    public double FogProbabilityMultiplier
    {
        get => _fogProbabilityMultiplier;
        set => SetProperty(ref _fogProbabilityMultiplier, value);
    }

    public double MechanicalWearProbabilityMultiplier
    {
        get => _mechanicalWearProbabilityMultiplier;
        set => SetProperty(ref _mechanicalWearProbabilityMultiplier, value);
    }

    public double CabinMechanicalFailureProbabilityMultiplier
    {
        get => _cabinMechanicalFailureProbabilityMultiplier;
        set => SetProperty(ref _cabinMechanicalFailureProbabilityMultiplier, value);
    }

    public double PowerOutageProbabilityMultiplier
    {
        get => _powerOutageProbabilityMultiplier;
        set => SetProperty(ref _powerOutageProbabilityMultiplier, value);
    }

    public double VoltageSpikeProbabilityMultiplier
    {
        get => _voltageSpikeProbabilityMultiplier;
        set => SetProperty(ref _voltageSpikeProbabilityMultiplier, value);
    }

    public SimulationSnapshot? LastSnapshot { get; private set; }

    private void StartSimulation()
    {
        if (_isBusy)
        {
            return;
        }

        _engine.SetTimeScale(ManualTimeScale);
        _isRunning = true;
        ApplySnapshot(_engine.Start());
        _simulationTimer.Start();
        PushToast("Simulación iniciada", $"La jornada está ejecutándose a {ManualTimeScaleText}.", "#2563EB", "▶");
        UpdateCommandStates();
    }

    private void PauseSimulation()
    {
        if (_isBusy)
        {
            return;
        }

        _simulationTimer.Stop();
        _isRunning = false;
        ApplySnapshot(_engine.Pause());
        PushToast("Simulación en pausa", "El estado quedó congelado para revisión operativa.", "#475569", "⏸");
        UpdateCommandStates();
    }

    private void StepSimulation()
    {
        if (_isBusy || _isRunning)
        {
            return;
        }

        _engine.SetTimeScale(ManualTimeScale);
        var snapshot = _engine.Step();
        if (_engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed))
        {
            snapshot = _engine.Pause();
        }

        ApplySnapshot(snapshot);
        UpdateCommandStates();
    }

    private void ResetSimulation()
    {
        if (_isBusy)
        {
            return;
        }

        _simulationTimer.Stop();
        _isRunning = false;
        _engine = _sessionService.CreateEngine(BuildSessionRequest());
        _engine.SetTimeScale(ManualTimeScale);
        _engine.SetDemandMultiplier(PassengerArrivalMultiplier);
        _lastRunReport = null;
        LastExportPdfPath = string.Empty;
        LastExportJsonPath = string.Empty;
        ExportStatusText = "Sin exportaciones recientes.";
        LoadRiskTuningProfile(_engine.Options.RiskTuning);
        ApplySnapshot(_engine.CurrentSnapshot);
        PushToast("Sesión reiniciada", "Se reconstruyó la jornada con la configuración actual.", "#0F766E", "↺");
        UpdateCommandStates();
    }

    private async void RunInstantSimulationFromScratchAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _simulationTimer.Stop();
        _isRunning = false;
        SetBusyState(true, "Procesando simulacro instantáneo. Por favor, espera...");

        try
        {
            var engine = _sessionService.CreateEngine(BuildSessionRequest());
            engine.SetTimeScale(ManualTimeScale);
            engine.SetDemandMultiplier(PassengerArrivalMultiplier);

            var executionResult = await Task.Run(() =>
            {
                var snapshot = engine.RunToEndOfService();
                var report = engine.CreateRunReport();
                var artifacts = _reportExportService.Export(report);
                return (Engine: engine, Snapshot: snapshot, Report: report, Artifacts: artifacts);
            });

            _engine = executionResult.Engine;
            ApplySnapshot(executionResult.Snapshot);
            ApplyExportArtifacts(executionResult.Report, executionResult.Artifacts, "Se ejecutó un simulacro instantáneo desde cero y se generó el reporte.");
            OpenExportDirectory(executionResult.Artifacts.PdfPath);
            PushToast("Simulacro completado", "La jornada completa se ejecutó y se exportaron sus artefactos.", "#7C3AED", "⚡");
        }
        catch (Exception exception)
        {
            ExportStatusText = $"No se pudo completar el simulacro instantáneo: {exception.Message}";
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async void FastForwardCurrentSimulationAndExportAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _simulationTimer.Stop();
        _isRunning = false;
        SetBusyState(true, "Acelerando la jornada actual y preparando la exportación...");

        try
        {
            var engine = _engine;
            engine.SetTimeScale(ManualTimeScale);
            engine.SetDemandMultiplier(PassengerArrivalMultiplier);

            var executionResult = await Task.Run(() =>
            {
                var snapshot = engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed)
                    ? engine.RunToEndOfService()
                    : engine.CurrentSnapshot;

                var report = engine.CreateRunReport();
                var artifacts = _reportExportService.Export(report);
                return (Snapshot: snapshot, Report: report, Artifacts: artifacts);
            });

            ApplySnapshot(executionResult.Snapshot);
            ApplyExportArtifacts(executionResult.Report, executionResult.Artifacts, "Se aceleró la jornada actual hasta su cierre y se generó el reporte.");
            OpenExportDirectory(executionResult.Artifacts.PdfPath);
            PushToast("Cierre acelerado", "La corrida actual fue completada y exportada.", "#1D4ED8", "⇢");
        }
        catch (Exception exception)
        {
            ExportStatusText = $"No se pudo finalizar y exportar la jornada: {exception.Message}";
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async void ExportCurrentReportAsync()
    {
        if (_isBusy || _isRunning || LastSnapshot is null)
        {
            return;
        }

        SetBusyState(true, "Exportando reporte actual...");

        try
        {
            var engine = _engine;

            var exportResult = await Task.Run(() =>
            {
                var report = engine.CreateRunReport();
                var artifacts = _reportExportService.Export(report);
                return (Report: report, Artifacts: artifacts);
            });

            ApplyExportArtifacts(exportResult.Report, exportResult.Artifacts, "Se exportó el reporte de la corrida actual.");
            OpenExportDirectory(exportResult.Artifacts.PdfPath);
            PushToast("Reporte exportado", "Los artefactos PDF y JSON quedaron listos.", "#0F766E", "⬇");
        }
        catch (Exception exception)
        {
            ExportStatusText = $"No se pudo exportar el reporte: {exception.Message}";
        }
        finally
        {
            SetBusyState(false);
        }
    }

    public async Task<ExportedSimulationArtifacts?> ExportSingleDayReportAsync()
    {
        if (_isBusy)
        {
            return null;
        }

        _simulationTimer.Stop();
        _isRunning = false;
        SetBusyState(true, "Preparando reporte de un día...");

        try
        {
            var request = BuildSessionRequest();
            var timeScale = ManualTimeScale;
            var demandMultiplier = PassengerArrivalMultiplier;

            var executionResult = await Task.Run(() =>
            {
                var engine = _sessionService.CreateEngine(request);
                engine.SetTimeScale(Math.Max(timeScale, 20.0));
                engine.SetDemandMultiplier(demandMultiplier);
                var snapshot = engine.RunToEndOfService();
                var report = engine.CreateRunReport();
                var artifacts = _reportExportService.Export(report);
                return (Engine: engine, Snapshot: snapshot, Report: report, Artifacts: artifacts);
            });

            _engine = executionResult.Engine;
            ApplySnapshot(executionResult.Snapshot);
            ApplyExportArtifacts(executionResult.Report, executionResult.Artifacts, "Se generó el reporte del día seleccionado.");
            OpenExportDirectory(executionResult.Artifacts.PdfPath);
            PushToast("Reporte listo", "Se exportó el PDF y el JSON del día simulado.", "#0F766E", "⬇");
            return executionResult.Artifacts;
        }
        catch (Exception exception)
        {
            ExportStatusText = $"No se pudo exportar el reporte diario: {exception.Message}";
            PushToast("Error de reporte", exception.Message, "#991B1B", "!");
            return null;
        }
        finally
        {
            SetBusyState(false);
        }
    }

    public async Task<ExportedSimulationArtifacts?> ExportDateRangeReportAsync(DateTime startDate, DateTime endDate)
    {
        if (_isBusy)
        {
            return null;
        }

        if (endDate.Date < startDate.Date)
        {
            (startDate, endDate) = (endDate.Date, startDate.Date);
        }

        var dates = Enumerable.Range(0, (endDate.Date - startDate.Date).Days + 1)
            .Select(offset => startDate.Date.AddDays(offset))
            .ToArray();

        _simulationTimer.Stop();
        _isRunning = false;
        SetBusyState(true, dates.Length == 1
            ? "Simulando el día seleccionado para reporte..."
            : $"Simulando lote de {dates.Length} días para reporte...");

        try
        {
            var requestTemplate = BuildSessionRequest();
            var timeScale = ManualTimeScale;
            var demandMultiplier = PassengerArrivalMultiplier;

            var executionResult = await Task.Run(() =>
            {
                var reports = new List<SimulationRunReport>();
                SimulationEngine? lastEngine = null;
                SimulationSnapshot? lastSnapshot = null;

                foreach (var simulationDate in dates)
                {
                    var request = CloneSessionRequestForDate(requestTemplate, simulationDate);
                    var engine = _sessionService.CreateEngine(request);
                    engine.SetTimeScale(Math.Max(timeScale, 20.0));
                    engine.SetDemandMultiplier(demandMultiplier);
                    lastSnapshot = engine.RunToEndOfService();
                    reports.Add(engine.CreateRunReport());
                    lastEngine = engine;
                }

                var artifacts = reports.Count == 1
                    ? _reportExportService.Export(reports[0])
                    : _reportExportService.ExportBatch(reports);

                return (Reports: reports, LastEngine: lastEngine!, LastSnapshot: lastSnapshot!, Artifacts: artifacts);
            });

            _engine = executionResult.LastEngine;
            ApplySnapshot(executionResult.LastSnapshot);

            var lastReport = executionResult.Reports.Last();
            _lastRunReport = lastReport;
            LastExportPdfPath = executionResult.Artifacts.PdfPath;
            LastExportJsonPath = executionResult.Artifacts.JsonPath;
            RiskCalibrationSummaryText = lastReport.RiskCalibrationSummary;
            ExportStatusText = executionResult.Reports.Count == 1
                ? $"Reporte diario generado. PDF: {executionResult.Artifacts.PdfPath} | JSON: {executionResult.Artifacts.JsonPath}"
                : $"Reporte por lotes generado ({executionResult.Reports.Count} días). PDF: {executionResult.Artifacts.PdfPath} | JSON: {executionResult.Artifacts.JsonPath}";

            OpenExportDirectory(executionResult.Artifacts.PdfPath);
            PushToast("Reporte listo", executionResult.Reports.Count == 1
                ? "Se exportó el día seleccionado."
                : "Se exportó el consolidado por lotes.", "#0F766E", "⬇");
            return executionResult.Artifacts;
        }
        catch (Exception exception)
        {
            ExportStatusText = $"No se pudo exportar el reporte por fechas: {exception.Message}";
            PushToast("Error de reporte", exception.Message, "#991B1B", "!");
            return null;
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void ApplyRiskTuning()
    {
        if (_isBusy || _engine is null)
        {
            return;
        }

        var profile = BuildRiskTuningProfile();
        _engine.ApplyRiskTuning(profile);
        RiskCalibrationSummaryText = profile.ToSummaryText();
        PushToast("Riesgo recalibrado", "La nueva matriz de probabilidades quedó aplicada en caliente.", "#DC2626", "⚙");
        ApplySnapshot(_engine.CurrentSnapshot);
        UpdateCommandStates();
    }

    private void InjectMechanicalFailure()
    {
        if (_isBusy)
        {
            return;
        }

        var cabinId = ResolveSelectedCabinId();
        if (cabinId is null)
        {
            return;
        }

        ApplySnapshot(_engine.InjectMechanicalFailure(cabinId.Value));
        PushToast("Falla mecánica", "Se forzó una desviación mecánica sobre la cabina seleccionada.", "#EA580C", "⚠");
        UpdateCommandStates();
    }

    private void InjectElectricalFailure()
    {
        if (_isBusy)
        {
            return;
        }

        var cabinId = SelectedInjectionTarget.Id == "system"
            ? null
            : ResolveSelectedCabinId();

        ApplySnapshot(_engine.InjectElectricalFailure(cabinId));
        PushToast("Falla eléctrica", "Se inyectó una contingencia eléctrica sin detener la simulación.", "#D97706", "⚡");
        UpdateCommandStates();
    }

    private void InjectStorm()
    {
        if (_isBusy)
        {
            return;
        }

        ApplySnapshot(_engine.InjectStorm());
        PushToast("Tormenta", "El entorno pasó a condición de tormenta de altura.", "#7C3AED", "☈");
        UpdateCommandStates();
    }



    private void InjectStrongWind()
    {
        if (_isBusy)
        {
            return;
        }

        ApplySnapshot(_engine.InjectStrongWind());
        PushToast("Viento fuerte", "Se activó una ráfaga intensa sobre la línea.", "#2563EB", "🡹");
        UpdateCommandStates();
    }

    private void InjectFog()
    {
        if (_isBusy)
        {
            return;
        }

        ApplySnapshot(_engine.InjectFog());
        PushToast("Neblina", "La visibilidad fue degradada para validar lectura operativa.", "#64748B", "◌");
        UpdateCommandStates();
    }

    private void InjectPulleyWear()
    {
        if (_isBusy)
        {
            return;
        }

        var cabinId = ResolveSelectedCabinId();
        if (cabinId is null)
        {
            return;
        }

        ApplySnapshot(_engine.InjectPulleyWear(cabinId.Value));
        PushToast("Desgaste acelerado", "Se aplicó una degradación puntual de rodadura y frenado.", "#B45309", "⌁");
        UpdateCommandStates();
    }

    private void InjectVoltageSpike()
    {
        if (_isBusy)
        {
            return;
        }

        var cabinId = SelectedInjectionTarget.Id == "system"
            ? null
            : ResolveSelectedCabinId();

        ApplySnapshot(_engine.InjectVoltageSpike(cabinId));
        PushToast("Pico de tensión", "El transitorio eléctrico quedó registrado en telemetría.", "#9333EA", "ϟ");
        UpdateCommandStates();
    }

    private void InjectOverload()
    {
        if (_isBusy)
        {
            return;
        }

        var cabinId = SelectedInjectionTarget.Id == "system"
            ? null
            : ResolveSelectedCabinId();

        ApplySnapshot(_engine.InjectOverload(cabinId));
        PushToast("Sobrecarga", cabinId is null
            ? "Se elevó la ocupación de todas las cabinas activas."
            : "Se elevó la ocupación de la cabina seleccionada.", "#DC2626", "⬤");
        UpdateCommandStates();
    }

    private void InjectEmergencyStop()
    {
        if (_isBusy)
        {
            return;
        }

        ApplySnapshot(_engine.InjectEmergencyStop());
        PushToast("Emergencia controlada", "El sistema frenó de forma protectora y quedó en recuperación degradada.", "#991B1B", "■");
        UpdateCommandStates();
    }

    private void SimulationTimerOnTick(object? sender, EventArgs e)
    {
        try
        {
            var snapshot = _engine.Step();
            ApplySnapshot(snapshot);

            if (_engine.OperationalState is SystemOperationalState.EmergencyStop or SystemOperationalState.Completed)
            {
                _simulationTimer.Stop();
                _isRunning = false;
                PushToast(
                    _engine.OperationalState == SystemOperationalState.Completed ? "Jornada completada" : "Jornada detenida",
                    _engine.OperationalState == SystemOperationalState.Completed
                        ? "La simulación llegó al final del servicio."
                        : "La jornada terminó por un escalamiento crítico de seguridad.",
                    _engine.OperationalState == SystemOperationalState.Completed ? "#0F766E" : "#991B1B",
                    _engine.OperationalState == SystemOperationalState.Completed ? "✓" : "■");
                UpdateCommandStates();
            }
        }
        catch (Exception exception)
        {
            _simulationTimer.Stop();
            _isRunning = false;
            ExportStatusText = $"La simulación se detuvo por un error: {exception.Message}";
            PushToast("Error de ejecución", exception.Message, "#991B1B", "!");
            UpdateCommandStates();
        }
    }

    private SimulationSessionRequest BuildSessionRequest()
    {
        return new SimulationSessionRequest
        {
            ModeId = SelectedMode.Id,
            ScenarioId = SelectedScenario.Id,
            RandomSeed = ParseSeed(),
            SimulationDate = SelectedSimulationDate ?? DateTime.Today,
            ServiceDurationHours = ServiceDurationHours,
            PassengerDemandMultiplier = PassengerArrivalMultiplier,
            PressureMode = SelectedPressureMode.Id == "training"
                ? SimulationPressureMode.IntensifiedTraining
                : SimulationPressureMode.Realistic,
            CabinsPerDirectionPerSegment = ParseSelectedCabinDensity(),
            RiskTuning = BuildRiskTuningProfile()
        };
    }

    private static SimulationSessionRequest CloneSessionRequestForDate(SimulationSessionRequest template, DateTime simulationDate)
    {
        return new SimulationSessionRequest
        {
            ModeId = template.ModeId,
            ScenarioId = template.ScenarioId,
            RandomSeed = template.RandomSeed,
            SimulationDate = simulationDate.Date,
            ServiceDurationHours = template.ServiceDurationHours,
            PassengerDemandMultiplier = template.PassengerDemandMultiplier,
            PressureMode = template.PressureMode,
            CabinsPerDirectionPerSegment = template.CabinsPerDirectionPerSegment,
            RiskTuning = template.RiskTuning.Clone()
        };
    }

    private static void OpenExportDirectory(string pdfPath)
    {
        var directory = Path.GetDirectoryName(pdfPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch
        {
            // Si Windows no permite abrir la carpeta, la ruta queda visible en ExportStatusText.
        }
    }

    private SimulationRiskTuningProfile BuildRiskTuningProfile()
    {
        var profile = new SimulationRiskTuningProfile
        {
            GlobalRiskMultiplier = GlobalRiskMultiplier,
            StormProbabilityMultiplier = StormProbabilityMultiplier,
            WindProbabilityMultiplier = WindProbabilityMultiplier,
            FogProbabilityMultiplier = FogProbabilityMultiplier,
            MechanicalWearProbabilityMultiplier = MechanicalWearProbabilityMultiplier,
            CabinMechanicalFailureProbabilityMultiplier = CabinMechanicalFailureProbabilityMultiplier,
            PowerOutageProbabilityMultiplier = PowerOutageProbabilityMultiplier,
            VoltageSpikeProbabilityMultiplier = VoltageSpikeProbabilityMultiplier
        };

        profile.Normalize();
        return profile;
    }

    private void LoadRiskTuningProfile(SimulationRiskTuningProfile? profile)
    {
        var effective = profile?.Clone() ?? new SimulationRiskTuningProfile();
        effective.Normalize();

        GlobalRiskMultiplier = effective.GlobalRiskMultiplier;
        StormProbabilityMultiplier = effective.StormProbabilityMultiplier;
        WindProbabilityMultiplier = effective.WindProbabilityMultiplier;
        FogProbabilityMultiplier = effective.FogProbabilityMultiplier;
        MechanicalWearProbabilityMultiplier = effective.MechanicalWearProbabilityMultiplier;
        CabinMechanicalFailureProbabilityMultiplier = effective.CabinMechanicalFailureProbabilityMultiplier;
        PowerOutageProbabilityMultiplier = effective.PowerOutageProbabilityMultiplier;
        VoltageSpikeProbabilityMultiplier = effective.VoltageSpikeProbabilityMultiplier;
        RiskCalibrationSummaryText = effective.ToSummaryText();
    }


    private int ParseSeed()
    {
        if (int.TryParse(RandomSeed, out var parsedSeed))
        {
            return parsedSeed;
        }

        RandomSeed = "20260323";
        return 20260323;
    }

    private double ParseSelectedTimeScale()
    {
        return double.TryParse(SelectedTimeScale.Id, out var parsedScale)
            ? parsedScale
            : 1.0;
    }

    private int ParseSelectedCabinDensity()
    {
        return int.TryParse(SelectedCabinDensity.Id, out var parsedDensity)
            ? Math.Max(1, parsedDensity)
            : 1;
    }

    private int? ResolveSelectedCabinId()
    {
        if (SelectedInjectionTarget.Id == "system")
        {
            return LastSnapshot?.Cabins
                .OrderByDescending(cabin => cabin.PassengerCount)
                .ThenBy(cabin => cabin.Id)
                .Select(cabin => (int?)cabin.Id)
                .FirstOrDefault();
        }

        return int.TryParse(SelectedInjectionTarget.Id, out var cabinId)
            ? cabinId
            : null;
    }

    private void UpdateScenarioDescription()
    {
        ScenarioDescriptionText = _sessionService.ResolveScenarioDescription(SelectedMode.Id, SelectedScenario.Id);
    }

    private void ApplySnapshot(SimulationSnapshot snapshot)
    {
        LastSnapshot = snapshot;

        NarrativeText = snapshot.OperationalNarrative;
        OperationalStateText = snapshot.OperationalStateDisplay;
        ElapsedText = snapshot.Elapsed.ToClockText();
        CurrentWeatherText = snapshot.WeatherSummary;
        ProcessedPassengersText = snapshot.ProcessedPassengers.ToString();
        CurrentRiskValue = snapshot.CurrentRiskScore;
        CurrentRiskText = $"{snapshot.CurrentRiskScore:F1} / 100";
        AverageOccupancyText = $"{snapshot.AverageOccupancyPercent:F1} %";
        ActiveIncidentText = snapshot.ActiveCriticalIssues.ToString();
        CurrentDayProfileText = snapshot.DayProfileName;
        SeasonalityText = snapshot.SeasonalityLabel;
        VisibilityText = $"{snapshot.VisibilityPercent:F1} %";
        IcingText = $"{snapshot.IcingRiskPercent:F1} %";
        PressureModeText = snapshot.PressureModeDisplay;
        SimulationDateText = snapshot.SimulationDate.ToString("yyyy-MM-dd");
        OperationalVarianceSeedText = snapshot.OperationalVarianceSeed.ToString();
        StationTelemetrySummaryText = BuildStationTelemetrySummary(snapshot.Stations);

        SynchronizeCollection(EventLog, snapshot.RecentEvents);
        SynchronizeCabinStates(snapshot.Cabins);
        SynchronizeStationStates(snapshot.Stations);
        RefreshInjectionTargets(snapshot);

        SnapshotUpdated?.Invoke(this, snapshot);
        UpdateCommandStates();
    }

    private static string BuildStationTelemetrySummary(IReadOnlyList<StationSnapshot> stations)
    {
        if (stations.Count == 0)
        {
            return "Sin telemetría de estaciones.";
        }

        var ascending = stations.Sum(station => station.WaitingAscendingPassengers);
        var descending = stations.Sum(station => station.WaitingDescendingPassengers);
        var peakStation = stations
            .OrderByDescending(station => station.TotalWaitingPassengers)
            .ThenBy(station => station.Id)
            .First();

        return $"Ascenso {ascending} pax | Descenso {descending} pax | Mayor presión: {peakStation.Name} ({peakStation.TotalWaitingPassengers} pax).";
    }

    private void RefreshInjectionTargets(SimulationSnapshot snapshot)
    {
        var orderedCabins = snapshot.Cabins.OrderBy(cabin => cabin.Id).ToList();
        var expectedIds = new List<string> { "system" };
        expectedIds.AddRange(orderedCabins.Select(cabin => cabin.Id.ToString()));

        var currentIds = InjectionTargetOptions.Select(option => option.Id).ToList();
        if (currentIds.SequenceEqual(expectedIds))
        {
            if (SelectedInjectionTarget is null && InjectionTargetOptions.Count > 0)
            {
                SelectedInjectionTarget = InjectionTargetOptions.First();
            }

            return;
        }

        var previousSelectionId = SelectedInjectionTarget?.Id;
        InjectionTargetOptions.Clear();
        InjectionTargetOptions.Add(new SelectionOption("system", "Sistema completo / auto", "Usa el sistema completo o selecciona automáticamente la cabina de mayor carga."));

        foreach (var cabin in orderedCabins)
        {
            InjectionTargetOptions.Add(new SelectionOption(
                cabin.Id.ToString(),
                $"{cabin.Code} - {cabin.SegmentName}",
                $"{cabin.DirectionDisplay} | {cabin.OccupancyLabel} | {cabin.AlertLevelDisplay}"));
        }

        SelectedInjectionTarget = InjectionTargetOptions.FirstOrDefault(option => option.Id == previousSelectionId)
            ?? InjectionTargetOptions.First();
    }

    private void ApplyExportArtifacts(SimulationRunReport report, ExportedSimulationArtifacts artifacts, string contextMessage)
    {
        _lastRunReport = report;
        LastExportPdfPath = artifacts.PdfPath;
        LastExportJsonPath = artifacts.JsonPath;
        RiskCalibrationSummaryText = report.RiskCalibrationSummary;
        ExportStatusText = $"{contextMessage} PDF: {artifacts.PdfPath} | JSON: {artifacts.JsonPath}";
    }

    private void PushToast(string title, string message, string accentColor, string iconGlyph)
    {
        var notification = new ToastNotification(title, message, accentColor, iconGlyph);
        ToastNotifications.Insert(0, notification);

        while (ToastNotifications.Count > 4)
        {
            ToastNotifications.RemoveAt(ToastNotifications.Count - 1);
        }

        var expirationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3.8)
        };

        expirationTimer.Tick += (_, _) =>
        {
            expirationTimer.Stop();
            ToastNotifications.Remove(notification);
        };

        expirationTimer.Start();
    }

    private void SetBusyState(bool isBusy, string? statusMessage = null)
    {
        _isBusy = isBusy;

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            ExportStatusText = statusMessage!;
        }

        UpdateCommandStates();
    }

    private void UpdateCommandStates()
    {
        StartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        StepCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
        InstantSimulationCommand.RaiseCanExecuteChanged();
        FastForwardAndExportCommand.RaiseCanExecuteChanged();
        ExportReportCommand.RaiseCanExecuteChanged();
        ApplyRiskTuningCommand.RaiseCanExecuteChanged();
        InjectMechanicalFailureCommand.RaiseCanExecuteChanged();
        InjectElectricalFailureCommand.RaiseCanExecuteChanged();
        InjectStormCommand.RaiseCanExecuteChanged();
        InjectStrongWindCommand.RaiseCanExecuteChanged();
        InjectFogCommand.RaiseCanExecuteChanged();
        InjectPulleyWearCommand.RaiseCanExecuteChanged();
        InjectVoltageSpikeCommand.RaiseCanExecuteChanged();
        InjectOverloadCommand.RaiseCanExecuteChanged();
        InjectEmergencyStopCommand.RaiseCanExecuteChanged();
    }

    private bool CanStartSimulation()
    {
        return !_isBusy && !_isRunning && _engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed);
    }

    private bool CanPauseSimulation()
    {
        return !_isBusy && _isRunning;
    }

    private bool CanStepSimulation()
    {
        return !_isBusy && !_isRunning && _engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed);
    }

    private bool CanResetSimulation()
    {
        return !_isBusy;
    }

    private bool CanRunInstantSimulation()
    {
        return !_isBusy && !_isRunning;
    }

    private bool CanFastForwardCurrentSimulation()
    {
        return !_isBusy && !_isRunning && _engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed);
    }

    private bool CanExportCurrentReport()
    {
        return !_isBusy && !_isRunning && LastSnapshot is not null;
    }

    private bool CanApplyRiskTuning()
    {
        return !_isBusy && LastSnapshot is not null && _engine.OperationalState is not SystemOperationalState.EmergencyStop;
    }

    private bool CanInjectCabinTargetedFailure()
    {
        return !_isBusy
            && LastSnapshot is not null
            && _engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed)
            && ResolveSelectedCabinId() is not null;
    }

    private bool CanInjectGeneralFailure()
    {
        return !_isBusy
            && LastSnapshot is not null
            && _engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed);
    }

    private static void SynchronizeCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        var items = source.ToList();
        var sharedCount = Math.Min(target.Count, items.Count);

        for (var index = 0; index < sharedCount; index++)
        {
            if (!EqualityComparer<T>.Default.Equals(target[index], items[index]))
            {
                target[index] = items[index];
            }
        }

        while (target.Count > items.Count)
        {
            target.RemoveAt(target.Count - 1);
        }

        for (var index = sharedCount; index < items.Count; index++)
        {
            target.Add(items[index]);
        }
    }

    private void SynchronizeCabinStates(IReadOnlyList<CabinSnapshot> snapshots)
    {
        if (!CabinStates.Select(row => row.Id).SequenceEqual(snapshots.Select(snapshot => snapshot.Id)))
        {
            CabinStates.Clear();
            foreach (var snapshot in snapshots)
            {
                CabinStates.Add(new CabinStateRowViewModel(snapshot));
            }

            return;
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            CabinStates[index].UpdateFrom(snapshots[index]);
        }
    }

    private void SynchronizeStationStates(IReadOnlyList<StationSnapshot> snapshots)
    {
        if (!StationStates.Select(row => row.Id).SequenceEqual(snapshots.Select(snapshot => snapshot.Id)))
        {
            StationStates.Clear();
            foreach (var snapshot in snapshots)
            {
                StationStates.Add(new StationStateRowViewModel(snapshot));
            }

            return;
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            StationStates[index].UpdateFrom(snapshots[index]);
        }
    }
}

public sealed class CabinStateRowViewModel : BaseViewModel
{
    private int _id;
    private string _code = string.Empty;
    private string _segmentName = string.Empty;
    private string _compactOccupancyLabel = string.Empty;
    private double _velocityMetersPerSecond;
    private string _operationalStateDisplay = string.Empty;
    private string _healthSummaryDisplay = string.Empty;
    private string _alertLevelDisplay = string.Empty;

    public CabinStateRowViewModel(CabinSnapshot snapshot)
    {
        UpdateFrom(snapshot);
    }

    public int Id { get => _id; private set => SetProperty(ref _id, value); }

    public string Code { get => _code; private set => SetProperty(ref _code, value); }

    public string SegmentName { get => _segmentName; private set => SetProperty(ref _segmentName, value); }

    public string CompactOccupancyLabel { get => _compactOccupancyLabel; private set => SetProperty(ref _compactOccupancyLabel, value); }

    public double VelocityMetersPerSecond { get => _velocityMetersPerSecond; private set => SetProperty(ref _velocityMetersPerSecond, value); }

    public string OperationalStateDisplay { get => _operationalStateDisplay; private set => SetProperty(ref _operationalStateDisplay, value); }

    public string HealthSummaryDisplay { get => _healthSummaryDisplay; private set => SetProperty(ref _healthSummaryDisplay, value); }

    public string AlertLevelDisplay { get => _alertLevelDisplay; private set => SetProperty(ref _alertLevelDisplay, value); }

    public void UpdateFrom(CabinSnapshot snapshot)
    {
        Id = snapshot.Id;
        Code = snapshot.Code;
        SegmentName = snapshot.SegmentName;
        CompactOccupancyLabel = snapshot.CompactOccupancyLabel;
        VelocityMetersPerSecond = snapshot.VelocityMetersPerSecond;
        OperationalStateDisplay = snapshot.OperationalStateDisplay;
        HealthSummaryDisplay = snapshot.HealthSummaryDisplay;
        AlertLevelDisplay = snapshot.AlertLevelDisplay;
    }
}

public sealed class StationStateRowViewModel : BaseViewModel
{
    private int _id;
    private string _name = string.Empty;
    private int _totalWaitingPassengers;
    private double _altitudeMeters;
    private int _waitingAscendingPassengers;
    private int _waitingDescendingPassengers;
    private string _boardingRulesDisplay = string.Empty;

    public StationStateRowViewModel(StationSnapshot snapshot)
    {
        UpdateFrom(snapshot);
    }

    public int Id { get => _id; private set => SetProperty(ref _id, value); }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }

    public int TotalWaitingPassengers { get => _totalWaitingPassengers; private set => SetProperty(ref _totalWaitingPassengers, value); }

    public double AltitudeMeters { get => _altitudeMeters; private set => SetProperty(ref _altitudeMeters, value); }

    public int WaitingAscendingPassengers { get => _waitingAscendingPassengers; private set => SetProperty(ref _waitingAscendingPassengers, value); }

    public int WaitingDescendingPassengers { get => _waitingDescendingPassengers; private set => SetProperty(ref _waitingDescendingPassengers, value); }

    public string BoardingRulesDisplay { get => _boardingRulesDisplay; private set => SetProperty(ref _boardingRulesDisplay, value); }

    public void UpdateFrom(StationSnapshot snapshot)
    {
        Id = snapshot.Id;
        Name = snapshot.Name;
        TotalWaitingPassengers = snapshot.TotalWaitingPassengers;
        AltitudeMeters = snapshot.AltitudeMeters;
        WaitingAscendingPassengers = snapshot.WaitingAscendingPassengers;
        WaitingDescendingPassengers = snapshot.WaitingDescendingPassengers;
        BoardingRulesDisplay = snapshot.BoardingRulesDisplay;
    }
}
