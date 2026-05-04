using System.Linq;
using HighRiskSimulator.Core.Factories;
using HighRiskSimulator.Core.Simulation;
using Xunit;

namespace HighRiskSimulator.Tests.DataStructures;

/// <summary>
/// Pruebas del grafo de estaciones.
/// </summary>
public sealed class StationNetworkGraphTests
{
    [Fact]
    public void ShortestPath_ReturnsAllMukumbariStationsInOrder()
    {
        var engine = MukumbariScenarioFactory.CreateEngine(new SimulationOptions());

        var path = engine.Model.Network.GetShortestPath(1, 5);

        Assert.Equal(5, path.Stations.Count);
        Assert.Equal("Barinitas", path.Stations.First().Name);
        Assert.Equal("Pico Espejo", path.Stations.Last().Name);
        Assert.True(path.TotalDistanceMeters > 12000);
    }

    [Fact]
    public void TryGetSegmentBetween_ReturnsExistingSegment()
    {
        var engine = MukumbariScenarioFactory.CreateEngine(new SimulationOptions());

        var found = engine.Model.Network.TryGetSegmentBetween(2, 3, out var segment);

        Assert.True(found);
        Assert.NotNull(segment);
        Assert.Equal("La Montaña - La Aguada", segment!.Name);
    }

    [Fact]
    public void Clone_CreatesIndependentStationObjects()
    {
        var engine = MukumbariScenarioFactory.CreateEngine(new SimulationOptions());
        var clone = engine.Model.Network.Clone();

        var originalStation = engine.Model.Network.GetStation(1);
        var clonedStation = clone.GetStation(1);

        Assert.NotSame(originalStation, clonedStation);
        Assert.Equal(originalStation.Name, clonedStation.Name);
        Assert.Equal(originalStation.RoutePositionMeters, clonedStation.RoutePositionMeters);
    }
}
