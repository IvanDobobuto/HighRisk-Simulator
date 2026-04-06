using System;
using System.Collections.Generic;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Resumen tabular por estación para reportes de jornada.
/// </summary>
public sealed class StationReportEntry
{
    public string Name { get; set; } = string.Empty;

    public string BoardingRules { get; set; } = string.Empty;

    public double AltitudeMeters { get; set; }

    public int BoardedAscending { get; set; }

    public int BoardedDescending { get; set; }

    public int UnloadedPassengers { get; set; }

    public int GeneratedAscendingQueue { get; set; }

    public int GeneratedDescendingQueue { get; set; }

    public int PeakQueue { get; set; }

    public int FinalQueue { get; set; }

    public int LeftWaitingPassengers { get; set; }
}

/// <summary>
/// Resumen tabular por cabina para reportes de jornada.
/// </summary>
public sealed class CabinReportEntry
{
    public string Code { get; set; } = string.Empty;

    public string SegmentName { get; set; } = string.Empty;

    public string PeakAlertLevel { get; set; } = string.Empty;

    public int CompletedTrips { get; set; }

    public double DistanceTravelledMeters { get; set; }

    public int BoardedPassengers { get; set; }

    public int UnloadedPassengers { get; set; }

    public double PeakOccupancyPercent { get; set; }

    public int EmergencyBrakeActivations { get; set; }

    public double OutOfServiceMinutes { get; set; }

    public double MechanicalHealthPercent { get; set; }

    public double ElectricalHealthPercent { get; set; }

    public double BrakeHealthPercent { get; set; }
}

/// <summary>
/// Reporte consolidado de una jornada completa o de un cierre instantáneo parcial.
/// </summary>
public sealed class SimulationRunReport
{
    public string SystemName { get; set; } = string.Empty;

    public string ScenarioName { get; set; } = string.Empty;

    public string DayProfileName { get; set; } = string.Empty;

    public string SeasonalityLabel { get; set; } = string.Empty;

    public string PressureModeLabel { get; set; } = string.Empty;

    public string FinalStateLabel { get; set; } = string.Empty;

    public string ExecutiveSummary { get; set; } = string.Empty;

    public string Conclusions { get; set; } = string.Empty;

    public string EventualityFingerprint { get; set; } = string.Empty;

    public DateTime SimulationDate { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int BaseSeed { get; set; }

    public int OperationalVarianceSeed { get; set; }

    public TimeSpan SimulatedElapsed { get; set; }

    public double MaxRiskScore { get; set; }

    public double AverageRiskScore { get; set; }

    public double AverageOccupancyPercent { get; set; }

    public double AverageVisibilityPercent { get; set; }

    public double PeakIcingRiskPercent { get; set; }

    public double PeakWindSpeedMetersPerSecond { get; set; }

    public double LowestTemperatureCelsius { get; set; }

    public int TotalProcessedPassengers { get; set; }

    public int TotalRejectedPassengers { get; set; }

    public int TotalEvents { get; set; }

    public int WarningEvents { get; set; }

    public int CriticalEvents { get; set; }

    public int CatastrophicEvents { get; set; }

    public bool EndedByEmergencyStop { get; set; }

    public IReadOnlyList<SimulationEvent> Timeline { get; set; } = Array.Empty<SimulationEvent>();

    public IReadOnlyList<StationReportEntry> Stations { get; set; } = Array.Empty<StationReportEntry>();

    public IReadOnlyList<CabinReportEntry> Cabins { get; set; } = Array.Empty<CabinReportEntry>();

    public IReadOnlyList<MetricPoint> RiskSeries { get; set; } = Array.Empty<MetricPoint>();

    public IReadOnlyList<MetricPoint> OccupancySeries { get; set; } = Array.Empty<MetricPoint>();

    public IReadOnlyList<MetricPoint> WeatherSeries { get; set; } = Array.Empty<MetricPoint>();
}
