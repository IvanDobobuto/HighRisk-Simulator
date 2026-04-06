using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
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
    private double _currentRiskValue;
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
    private string _exportStatusText = "Sin exportaciones recientes.";
    private string _lastExportPdfPath = string.Empty;
    private string _lastExportJsonPath = string.Empty;

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
            new("scripted", "Escenario específico", "Modo para reproducir incidentes preparados y validar protocolos."),
        };

        ScenarioOptions = new ObservableCollection<SelectionOption>(
            _sessionService.GetScriptedScenarios()
                .Select(item => new SelectionOption(item.Id, item.Name, item.Description)));

        PressureModeOptions = new ObservableCollection<SelectionOption>
        {
            new("realistic", "Operación realista", "La mayoría de las jornadas deben poder terminar sin incidentes severos."),
            new("training", "Entrenamiento intensificado", "Eleva la presión operacional para practicar contingencias."),
        };

        TimeScaleOptions = new ObservableCollection<SelectionOption>
        {
            new("1", "1x", "Ritmo normal de simulación."),
            new("2", "2x", "Acelera el avance temporal sin romper la física."),
            new("3", "3x", "Máxima velocidad interactiva permitida en la interfaz."),
        };

        CabinDensityOptions = new ObservableCollection<SelectionOption>
        {
            new("1", "1 cabina por sentido", "Configuración base alineada al esquema público de operación por tramo."),
            new("2", "2 cabinas por sentido", "Modo académico para estrés moderado y validación de separación."),
            new("3", "3 cabinas por sentido", "Modo intensivo para pruebas de congestión y capacidad."),
        };

        InjectionTargetOptions = new ObservableCollection<SelectionOption>();
        EventLog = new ObservableCollection<SimulationEvent>();
        CabinStates = new ObservableCollection<CabinSnapshot>();
        StationStates = new ObservableCollection<StationSnapshot>();

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
        InjectMechanicalFailureCommand = new RelayCommand(InjectMechanicalFailure, CanInjectCabinTargetedFailure);
        InjectElectricalFailureCommand = new RelayCommand(InjectElectricalFailure, CanInjectGeneralFailure);
        InjectStormCommand = new RelayCommand(InjectStorm, CanInjectGeneralFailure);
        InjectOverloadCommand = new RelayCommand(InjectOverload, CanInjectCabinTargetedFailure);
        InjectEmergencyStopCommand = new RelayCommand(InjectEmergencyStop, CanInjectGeneralFailure);

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

    public ObservableCollection<CabinSnapshot> CabinStates { get; }

    public ObservableCollection<StationSnapshot> StationStates { get; }

    public RelayCommand StartCommand { get; }

    public RelayCommand PauseCommand { get; }

    public RelayCommand StepCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand InstantSimulationCommand { get; }

    public RelayCommand FastForwardAndExportCommand { get; }

    public RelayCommand ExportReportCommand { get; }

    public RelayCommand InjectMechanicalFailureCommand { get; }

    public RelayCommand InjectElectricalFailureCommand { get; }

    public RelayCommand InjectStormCommand { get; }

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
            if (SetProperty(ref _selectedTimeScale, value) && _engine is not null)
            {
                _engine.SetTimeScale(ParseSelectedTimeScale());
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

    public SimulationSnapshot? LastSnapshot { get; private set; }

    private void StartSimulation()
    {
        if (_isBusy)
        {
            return;
        }

        _engine.SetTimeScale(ParseSelectedTimeScale());
        _isRunning = true;
        ApplySnapshot(_engine.Start());
        _simulationTimer.Start();
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
        UpdateCommandStates();
    }

    private void StepSimulation()
    {
        if (_isBusy || _isRunning)
        {
            return;
        }

        _engine.SetTimeScale(ParseSelectedTimeScale());
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
        _engine.SetTimeScale(ParseSelectedTimeScale());
        _lastRunReport = null;
        LastExportPdfPath = string.Empty;
        LastExportJsonPath = string.Empty;
        ExportStatusText = "Sin exportaciones recientes.";
        ApplySnapshot(_engine.CurrentSnapshot);
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
            engine.SetTimeScale(ParseSelectedTimeScale());

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
            engine.SetTimeScale(ParseSelectedTimeScale());

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
        UpdateCommandStates();
    }

    private void InjectStorm()
    {
        if (_isBusy)
        {
            return;
        }

        ApplySnapshot(_engine.InjectStorm());
        UpdateCommandStates();
    }

    private void InjectOverload()
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

        ApplySnapshot(_engine.InjectOverload(cabinId.Value));
        UpdateCommandStates();
    }

    private void InjectEmergencyStop()
    {
        if (_isBusy)
        {
            return;
        }

        _simulationTimer.Stop();
        _isRunning = false;
        ApplySnapshot(_engine.InjectEmergencyStop());
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
                UpdateCommandStates();
            }
        }
        catch (Exception exception)
        {
            _simulationTimer.Stop();
            _isRunning = false;
            ExportStatusText = $"La simulación se detuvo por un error: {exception.Message}";
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
            PressureMode = SelectedPressureMode.Id == "training"
                ? SimulationPressureMode.IntensifiedTraining
                : SimulationPressureMode.Realistic,
            CabinsPerDirectionPerSegment = ParseSelectedCabinDensity()
        };
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

        SynchronizeCollection(EventLog, snapshot.RecentEvents);
        SynchronizeCollection(CabinStates, snapshot.Cabins);
        SynchronizeCollection(StationStates, snapshot.Stations);
        RefreshInjectionTargets(snapshot);

        SnapshotUpdated?.Invoke(this, snapshot);
        UpdateCommandStates();
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
        ExportStatusText = $"{contextMessage} PDF: {artifacts.PdfPath} | JSON: {artifacts.JsonPath}";
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
        InjectMechanicalFailureCommand.RaiseCanExecuteChanged();
        InjectElectricalFailureCommand.RaiseCanExecuteChanged();
        InjectStormCommand.RaiseCanExecuteChanged();
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

    private bool CanInjectCabinTargetedFailure()
    {
        return !_isBusy
            && !_isRunning
            && LastSnapshot is not null
            && _engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed)
            && ResolveSelectedCabinId() is not null;
    }

    private bool CanInjectGeneralFailure()
    {
        return !_isBusy
            && !_isRunning
            && LastSnapshot is not null
            && _engine.OperationalState is not (SystemOperationalState.EmergencyStop or SystemOperationalState.Completed);
    }

    private static void SynchronizeCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
