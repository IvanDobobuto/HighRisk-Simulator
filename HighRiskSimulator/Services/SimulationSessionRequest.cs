
using System;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Simulation;

namespace HighRiskSimulator.Services;

/// <summary>
/// Configuración de sesión solicitada por la interfaz.
/// </summary>
public sealed class SimulationSessionRequest
{
    public string ModeId { get; set; } = "random";

    public string ScenarioId { get; set; } = string.Empty;

    public int RandomSeed { get; set; }

    public DateTime SimulationDate { get; set; } = DateTime.Today;

    public double ServiceDurationHours { get; set; } = 10.0;

    public double PassengerDemandMultiplier { get; set; } = 1.0;

    public SimulationPressureMode PressureMode { get; set; } = SimulationPressureMode.Realistic;

    public int CabinsPerDirectionPerSegment { get; set; } = 1;

    public SimulationRiskTuningProfile RiskTuning { get; set; } = new();
}
