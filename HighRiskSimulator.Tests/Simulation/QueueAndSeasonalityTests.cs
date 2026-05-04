using System;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Simulation.Seasonality;
using Xunit;

namespace HighRiskSimulator.Tests.Simulation;

/// <summary>
/// Pruebas de reglas terminales y temporada.
/// </summary>
public sealed class QueueAndSeasonalityTests
{
    [Fact]
    public void LowerTerminal_DoesNotAcceptDescendingQueue()
    {
        var station = new Station(1, "BAR", "Barinitas", 1577, 0, TimeSpan.FromSeconds(12), 1.0, isLowerTerminal: true);

        station.EnqueuePassengers(10, 8);

        Assert.Equal(10, station.WaitingAscendingPassengers);
        Assert.Equal(0, station.WaitingDescendingPassengers);
    }

    [Fact]
    public void UpperTerminal_DoesNotAcceptAscendingQueue()
    {
        var station = new Station(5, "PES", "Pico Espejo", 4765, 12500, TimeSpan.FromSeconds(18), 1.0, isUpperTerminal: true);

        station.EnqueuePassengers(12, 7);

        Assert.Equal(0, station.WaitingAscendingPassengers);
        Assert.Equal(7, station.WaitingDescendingPassengers);
    }

    [Fact]
    public void ChristmasDay_ResolvesAsHolidayHighDemand()
    {
        var profile = VenezuelanTourismCalendar.Resolve(new DateTime(2026, 12, 25));

        Assert.True(profile.IsHoliday);
        Assert.True(profile.DemandMultiplier > 1.2);
    }
}
