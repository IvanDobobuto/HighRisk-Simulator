using System;
using HighRiskSimulator.Core.Domain;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Parámetros de arranque del motor.
/// </summary>
public sealed class SimulationOptions
{
    public int RandomSeed { get; init; } = 20260323;

    public TimeSpan FixedTimeStep { get; init; } = TimeSpan.FromMilliseconds(100);

    public SimulationMode Mode { get; init; } = SimulationMode.IntelligentRandom;

    public string ScenarioId { get; init; } = ScenarioDefinition.NoScenarioId;

    public double DemandMultiplier { get; init; } = 1.0;

    public double RandomIncidentMultiplier { get; init; } = 1.0;

    public int TelemetryCapacity { get; init; } = 300;

    public bool EnableWeatherSystem { get; init; } = true;

    public bool EnableRandomDemand { get; init; } = true;

    public bool EnableSafetyEscalation { get; init; } = true;

    public TimeSpan ServiceStartTime { get; init; } = new(8, 0, 0);
}
