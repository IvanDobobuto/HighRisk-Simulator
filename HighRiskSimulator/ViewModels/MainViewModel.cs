using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly DispatcherTimer _simulationTimer;

    private SimulationEngine _engine = null!;
    private SelectionOption _selectedMode = null!;
    private SelectionOption _selectedScenario = null!;
    private string _randomSeed = "20260323";
    private bool _isRunning;
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

    public MainViewModel()
    {
        _sessionService = new SimulationSessionService();
        _simulationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(70)
        };
        _simulationTimer.Tick += SimulationTimerOnTick;

        SimulationModes = new ObservableCollection<SelectionOption>
        {
            new("random", "Aleatorio inteligente", "Modo reproducible por semilla con eventos y clima estocásticos."),
            new("scripted", "Escenario específico", "Modo guionizado para reproducir incidentes concretos."),
        };

        ScenarioOptions = new ObservableCollection<SelectionOption>(
            _sessionService.GetScriptedScenarios()
                .Select(item => new SelectionOption(item.Id, item.Name, item.Description)));

        EventLog = new ObservableCollection<SimulationEvent>();
        CabinStates = new ObservableCollection<CabinSnapshot>();
        StationStates = new ObservableCollection<StationSnapshot>();

        _selectedMode = SimulationModes.First();
        _selectedScenario = ScenarioOptions.First();

        StartCommand = new RelayCommand(StartSimulation, CanStartSimulation);
        PauseCommand = new RelayCommand(PauseSimulation, CanPauseSimulation);
        StepCommand = new RelayCommand(StepSimulation, CanStepSimulation);
        ResetCommand = new RelayCommand(ResetSimulation);

        UpdateScenarioDescription();
        ResetSimulation();
    }

    public ObservableCollection<SelectionOption> SimulationModes { get; }

    public ObservableCollection<SelectionOption> ScenarioOptions { get; }

    public ObservableCollection<SimulationEvent> EventLog { get; }

    public ObservableCollection<CabinSnapshot> CabinStates { get; }

    public ObservableCollection<StationSnapshot> StationStates { get; }

    public RelayCommand StartCommand { get; }

    public RelayCommand PauseCommand { get; }

    public RelayCommand StepCommand { get; }

    public RelayCommand ResetCommand { get; }

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

    public bool IsScriptedMode => SelectedMode.Id == "scripted";

    public string RandomSeed
    {
        get => _randomSeed;
        set => SetProperty(ref _randomSeed, value);
    }

    public string NarrativeText
    {
        get => _narrativeText;
        private set => SetProperty(ref _narrativeText, value);
    }

    public string OperationalStateText
    {
        get => _operationalStateText;
        private set => SetProperty(ref _operationalStateText, value);
    }

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public string CurrentWeatherText
    {
        get => _currentWeatherText;
        private set => SetProperty(ref _currentWeatherText, value);
    }

    public string ProcessedPassengersText
    {
        get => _processedPassengersText;
        private set => SetProperty(ref _processedPassengersText, value);
    }

    public string CurrentRiskText
    {
        get => _currentRiskText;
        private set => SetProperty(ref _currentRiskText, value);
    }

    public string AverageOccupancyText
    {
        get => _averageOccupancyText;
        private set => SetProperty(ref _averageOccupancyText, value);
    }

    public string ActiveIncidentText
    {
        get => _activeIncidentText;
        private set => SetProperty(ref _activeIncidentText, value);
    }

    public string CurrentDayProfileText
    {
        get => _currentDayProfileText;
        private set => SetProperty(ref _currentDayProfileText, value);
    }

    public string ScenarioDescriptionText
    {
        get => _scenarioDescriptionText;
        private set => SetProperty(ref _scenarioDescriptionText, value);
    }

    public double CurrentRiskValue
    {
        get => _currentRiskValue;
        set => SetProperty(ref _currentRiskValue, value);
    }

    public SimulationSnapshot? LastSnapshot { get; private set; }

    private void StartSimulation()
    {
        _isRunning = true;
        ApplySnapshot(_engine.Start());
        _simulationTimer.Start();
        UpdateCommandStates();
    }

    private void PauseSimulation()
    {
        _simulationTimer.Stop();
        _isRunning = false;
        ApplySnapshot(_engine.Pause());
        UpdateCommandStates();
    }

    private void StepSimulation()
    {
        if (_isRunning)
        {
            return;
        }

        var snapshot = _engine.Step();
        if (_engine.OperationalState != SystemOperationalState.EmergencyStop)
        {
            snapshot = _engine.Pause();
        }

        ApplySnapshot(snapshot);
        UpdateCommandStates();
    }

    private void ResetSimulation()
    {
        _simulationTimer.Stop();
        _isRunning = false;

        var parsedSeed = ParseSeed();
        _engine = _sessionService.CreateEngine(SelectedMode.Id, SelectedScenario.Id, parsedSeed);
        ApplySnapshot(_engine.CurrentSnapshot);

        UpdateCommandStates();
    }

    private void SimulationTimerOnTick(object? sender, EventArgs e)
    {
        var snapshot = _engine.Step();
        ApplySnapshot(snapshot);

        if (_engine.OperationalState == SystemOperationalState.EmergencyStop)
        {
            _simulationTimer.Stop();
            _isRunning = false;
            UpdateCommandStates();
        }
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

    private void UpdateScenarioDescription()
    {
        ScenarioDescriptionText = _sessionService.ResolveScenarioDescription(SelectedMode.Id, SelectedScenario.Id);
    }

    private void ApplySnapshot(SimulationSnapshot snapshot)
    {
        LastSnapshot = snapshot;

        NarrativeText = snapshot.OperationalNarrative;
        OperationalStateText = snapshot.OperationalState.ToString();
        ElapsedText = snapshot.Elapsed.ToString("mm\\:ss");
        CurrentWeatherText = snapshot.WeatherSummary;
        ProcessedPassengersText = snapshot.ProcessedPassengers.ToString();
        CurrentRiskValue = snapshot.CurrentRiskScore;
        CurrentRiskText = $"{snapshot.CurrentRiskScore:F1} / 100";
        AverageOccupancyText = $"{snapshot.AverageOccupancyPercent:F1} %";
        ActiveIncidentText = snapshot.ActiveCriticalIssues.ToString();
        CurrentDayProfileText = $"{snapshot.DayProfileName} - {snapshot.ScenarioName}";

        SynchronizeCollection(EventLog, snapshot.RecentEvents);
        SynchronizeCollection(CabinStates, snapshot.Cabins);
        SynchronizeCollection(StationStates, snapshot.Stations);

        SnapshotUpdated?.Invoke(this, snapshot);
    }

    private void UpdateCommandStates()
    {
        StartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        StepCommand.RaiseCanExecuteChanged();
    }

    private bool CanStartSimulation()
    {
        return !_isRunning && _engine.OperationalState != SystemOperationalState.EmergencyStop;
    }

    private bool CanPauseSimulation()
    {
        return _isRunning;
    }

    private bool CanStepSimulation()
    {
        return !_isRunning && _engine.OperationalState != SystemOperationalState.EmergencyStop;
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
