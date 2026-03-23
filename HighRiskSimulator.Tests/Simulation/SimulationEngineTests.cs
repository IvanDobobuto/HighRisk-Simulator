using System.Linq;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Factories;
using HighRiskSimulator.Core.Simulation;
using Xunit;

namespace HighRiskSimulator.Tests.Simulation;

/// <summary>
/// Pruebas del motor principal.
/// </summary>
public sealed class SimulationEngineTests
{
    [Fact]
    public void Engine_InitializesInReadyState()
    {
        var engine = MukumbariScenarioFactory.CreateEngine(new SimulationOptions());

        Assert.Equal(SystemOperationalState.Ready, engine.OperationalState);
        Assert.Equal(0, engine.CurrentSnapshot.TickIndex);
    }

    [Fact]
    public void Step_AdvancesTimeAndCreatesTelemetry()
    {
        var engine = MukumbariScenarioFactory.CreateEngine(new SimulationOptions());

        var snapshot = engine.Step();

        Assert.Equal(1, snapshot.TickIndex);
        Assert.True(snapshot.Elapsed > System.TimeSpan.Zero);
        Assert.NotEmpty(snapshot.Telemetry.RiskSeries);
    }

    [Fact]
    public void OverloadedCabin_ProducesOverloadEvent()
    {
        var engine = MukumbariScenarioFactory.CreateEngine(new SimulationOptions());
        var firstCabin = engine.Model.Cabins.First();

        firstCabin.SetPassengers(firstCabin.Capacity + 8);

        var snapshot = engine.Step();

        Assert.Contains(snapshot.RecentEvents, item => item.Type == SimulationEventType.Overload);
    }

    [Fact]
    public void ScriptedScenario_InjectsScheduledEvents()
    {
        var engine = MukumbariScenarioFactory.CreateEngine(new SimulationOptions
        {
            Mode = SimulationMode.ScriptedScenario,
            ScenarioId = "electrical-blackout"
        });

        SimulationSnapshot? latest = null;

        for (var index = 0; index < 200; index++)
        {
            latest = engine.Step();
        }

        Assert.NotNull(latest);
        Assert.Contains(latest!.RecentEvents, item => item.Type == SimulationEventType.ElectricalFailure);
    }
}
