using System;
using HighRiskSimulator.Core.Domain;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Parámetros de arranque del motor.
/// </summary>
public sealed class SimulationOptions
{
    public int RandomSeed { get; set; } = 20260323;

    /// <summary>
    /// Semilla derivada para microvariaciones entre corridas del mismo día base.
    /// Permite resultados muy parecidos para una misma semilla principal, pero no clones perfectos.
    /// </summary>
    public int OperationalVarianceSeed { get; set; } = Environment.TickCount;

    public TimeSpan FixedTimeStep { get; set; } = TimeSpan.FromMilliseconds(250);

    public SimulationMode Mode { get; set; } = SimulationMode.IntelligentRandom;

    public string ScenarioId { get; set; } = ScenarioDefinition.NoScenarioId;

    public double DemandMultiplier { get; set; } = 1.0;

    public double RandomIncidentMultiplier { get; set; } = 1.0;

    public int TelemetryCapacity { get; set; } = 360;

    public bool EnableWeatherSystem { get; set; } = true;

    public bool EnableRandomDemand { get; set; } = true;

    public bool EnableSafetyEscalation { get; set; } = true;

    public TimeSpan ServiceStartTime { get; set; } = new(8, 0, 0);

    public TimeSpan ServiceDuration { get; set; } = TimeSpan.FromHours(9);

    public DateTime SimulationDate { get; set; } = DateTime.Today;

    public SimulationPressureMode PressureMode { get; set; } = SimulationPressureMode.Realistic;

    public int CabinsPerDirectionPerSegment { get; set; } = 1;
}
