using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Factories;
using HighRiskSimulator.Core.Persistence;
using HighRiskSimulator.Core.Simulation;

namespace HighRiskSimulator.Services;

/// <summary>
/// Servicio UI que encapsula la creación de sesiones de simulación.
/// </summary>
public sealed class SimulationSessionService
{
    public IReadOnlyList<ScenarioDefinition> GetScriptedScenarios()
    {
        return MukumbariScenarioFactory.CreateScenarioCatalog();
    }

    public SimulationEngine CreateEngine(string modeId, string scenarioId, int randomSeed)
    {
        var mode = modeId == "scripted"
            ? SimulationMode.ScriptedScenario
            : SimulationMode.IntelligentRandom;

        var options = new SimulationOptions
        {
            RandomSeed = randomSeed,
            Mode = mode,
            ScenarioId = scenarioId,
            EnableRandomDemand = true,
            EnableWeatherSystem = true,
            EnableSafetyEscalation = true,
            RandomIncidentMultiplier = 1.0,
            DemandMultiplier = 1.0,
            FixedTimeStep = System.TimeSpan.FromMilliseconds(100),
            TelemetryCapacity = 320
        };

        var effectiveScenarioId = mode == SimulationMode.ScriptedScenario
            ? scenarioId
            : ScenarioDefinition.NoScenarioId;

        return MukumbariScenarioFactory.CreateEngine(options, effectiveScenarioId, NullSimulationSnapshotRepository.Instance);
    }

    public string ResolveScenarioDescription(string modeId, string scenarioId)
    {
        if (modeId != "scripted")
        {
            return "Modo aleatorio inteligente: el sistema usa una semilla reproducible para combinar demanda, clima, fallas y reglas de seguridad sin guion fijo.";
        }

        return GetScriptedScenarios()
            .FirstOrDefault(item => item.Id == scenarioId)
            ?.Description
            ?? "Escenario no encontrado.";
    }
}
