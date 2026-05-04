
using System;
using System.Collections.Generic;
using HighRiskSimulator.Core.Simulation;

namespace HighRiskSimulator.Core.Persistence;

/// <summary>
/// Contrato base para la futura persistencia histórica de jornadas.
///
/// Esta interfaz no se usa aún desde la UI para no acoplar el motor a MySQL,
/// pero deja definido el punto de entrada correcto para la siguiente fase.
/// </summary>
public interface ISimulationRunRepository
{
    void SaveMetadata(SimulationRunDescriptor descriptor);

    void SaveReport(SimulationRunReport report);

    IReadOnlyList<SimulationRunDescriptor> GetRecentRuns(int maxCount = 20);
}

/// <summary>
/// Descriptor liviano de una corrida, pensado para listados y consultas rápidas.
/// </summary>
public sealed class SimulationRunDescriptor
{
    public Guid RunId { get; set; } = Guid.NewGuid();

    public DateTime SimulationDate { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public string ScenarioName { get; set; } = string.Empty;

    public string FinalStateLabel { get; set; } = string.Empty;

    public int BaseSeed { get; set; }

    public int OperationalVarianceSeed { get; set; }

    public int TotalEvents { get; set; }

    public double MaxRiskScore { get; set; }
}

/// <summary>
/// Implementación nula mientras la fase MySQL permanezca desacoplada.
/// </summary>
public sealed class NullSimulationRunRepository : ISimulationRunRepository
{
    public static readonly NullSimulationRunRepository Instance = new();

    private NullSimulationRunRepository()
    {
    }

    public void SaveMetadata(SimulationRunDescriptor descriptor)
    {
        // Intencionalmente vacío.
    }

    public void SaveReport(SimulationRunReport report)
    {
        // Intencionalmente vacío.
    }

    public IReadOnlyList<SimulationRunDescriptor> GetRecentRuns(int maxCount = 20)
    {
        return Array.Empty<SimulationRunDescriptor>();
    }
}
