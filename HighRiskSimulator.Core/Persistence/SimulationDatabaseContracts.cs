
using System;
using System.Collections.Generic;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Simulation;

namespace HighRiskSimulator.Core.Persistence;

/// <summary>
/// Configuración base preparada para una futura implementación concreta sobre MySQL.
/// No conecta todavía; su función es dejar un contrato claro y coherente con la siguiente fase.
/// </summary>
public sealed class SimulationDatabaseSettings
{
    public string Server { get; set; } = "localhost";

    public int Port { get; set; } = 3306;

    public string Database { get; set; } = "highrisk_simulator";

    public string User { get; set; } = "root";

    public string Password { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    public string TablePrefix { get; set; } = "hrs_";

    public string ToConnectionString()
    {
        var sslMode = UseSsl ? "Required" : "Preferred";
        return $"Server={Server};Port={Port};Database={Database};User ID={User};Password={Password};SslMode={sslMode};Allow User Variables=True;";
    }
}

/// <summary>
/// Paquete lógico preparado para persistir una corrida completa sin obligar al motor
/// a conocer detalles de infraestructura.
/// </summary>
public sealed class SimulationPersistenceEnvelope
{
    public Guid RunId { get; set; } = Guid.NewGuid();

    public SimulationRunReport Report { get; set; } = new();

    public IReadOnlyList<SimulationSnapshot> Snapshots { get; set; } = Array.Empty<SimulationSnapshot>();

    public IReadOnlyList<SimulationEvent> Timeline { get; set; } = Array.Empty<SimulationEvent>();
}
