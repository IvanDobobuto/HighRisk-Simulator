
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.Persistence;

/// <summary>
/// Punto de extensión para persistencia futura.
/// 
/// Aquí es donde más adelante podrá conectarse MySQL sin acoplar el motor
/// a detalles de infraestructura.
/// </summary>
public interface ISimulationSnapshotRepository
{
    void Save(SimulationSnapshot snapshot);
}

/// <summary>
/// Implementación nula usada en esta fase para dejar la persistencia desacoplada.
/// </summary>
public sealed class NullSimulationSnapshotRepository : ISimulationSnapshotRepository
{
    public static readonly NullSimulationSnapshotRepository Instance = new();

    private NullSimulationSnapshotRepository()
    {
    }

    public void Save(SimulationSnapshot snapshot)
    {
        // Intencionalmente vacío.
        // En la siguiente fase este punto se sustituye por MySQL u otra infraestructura.
    }
}
