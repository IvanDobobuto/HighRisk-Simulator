
using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Factories;
using HighRiskSimulator.Core.Persistence;
using HighRiskSimulator.Core.Simulation;

namespace HighRiskSimulator.Services;

/// <summary>
/// Servicio de aplicación que encapsula la creación de sesiones de simulación.
/// </summary>
public sealed class SimulationSessionService
{
    public IReadOnlyList<ScenarioDefinition> GetScriptedScenarios()
    {
        return MukumbariScenarioFactory.CreateScenarioCatalog();
    }

    public SimulationEngine CreateEngine(SimulationSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mode = request.ModeId == "scripted"
            ? SimulationMode.ScriptedScenario
            : SimulationMode.IntelligentRandom;

        var effectiveScenarioId = mode == SimulationMode.ScriptedScenario
            ? request.ScenarioId
            : ScenarioDefinition.NoScenarioId;

        var operationalVarianceSeed = CreateOperationalVarianceSeed(request.RandomSeed, request.SimulationDate);
        var incidentMultiplier = request.PressureMode == SimulationPressureMode.IntensifiedTraining ? 1.18 : 1.0;
        var telemetryCapacity = request.PressureMode == SimulationPressureMode.IntensifiedTraining ? 960 : 720;
        var tuning = request.RiskTuning?.Clone() ?? new SimulationRiskTuningProfile();
        tuning.Normalize();

        var options = new SimulationOptions
        {
            RandomSeed = request.RandomSeed,
            OperationalVarianceSeed = operationalVarianceSeed,
            SimulationDate = request.SimulationDate.Date,
            PressureMode = request.PressureMode,
            CabinsPerDirectionPerSegment = Math.Max(1, request.CabinsPerDirectionPerSegment),
            Mode = mode,
            ScenarioId = effectiveScenarioId,
            EnableRandomDemand = true,
            EnableWeatherSystem = true,
            EnableSafetyEscalation = true,
            RandomIncidentMultiplier = incidentMultiplier,
            DemandMultiplier = 1.0,
            FixedTimeStep = TimeSpan.FromMilliseconds(250),
            ServiceStartTime = new TimeSpan(9, 0, 0),
            ServiceDuration = TimeSpan.FromHours(request.PressureMode == SimulationPressureMode.IntensifiedTraining ? 11 : 10),
            TelemetryCapacity = telemetryCapacity,
            RiskTuning = tuning
        };

        return MukumbariScenarioFactory.CreateEngine(options, effectiveScenarioId, NullSimulationSnapshotRepository.Instance);
    }

    public string ResolveScenarioDescription(string modeId, string scenarioId)
    {
        if (modeId != "scripted")
        {
            return "Modo aleatorio inteligente: combina demanda turística, clima, memoria causal, tunable risk panel y fallas no forzadas por jornada.";
        }

        return GetScriptedScenarios()
            .FirstOrDefault(item => item.Id == scenarioId)
            ?.Description
            ?? "Escenario no encontrado.";
    }

    private static int CreateOperationalVarianceSeed(int baseSeed, DateTime simulationDate)
    {
        var rollingEntropy = HashCode.Combine(
            baseSeed,
            simulationDate.Year,
            simulationDate.Month,
            simulationDate.Day,
            Environment.TickCount,
            DateTime.UtcNow.Ticks);

        return rollingEntropy == 0 ? 1 : rollingEntropy;
    }
}
