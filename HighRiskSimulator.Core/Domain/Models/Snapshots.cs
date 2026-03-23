using System.Collections.Generic;
using System.Linq;

namespace HighRiskSimulator.Core.Domain.Models;

/// <summary>
/// Punto temporal genérico para telemetría.
/// </summary>
public sealed class MetricPoint
{
    public MetricPoint(double timeSeconds, double value)
    {
        TimeSeconds = timeSeconds;
        Value = value;
    }

    public double TimeSeconds { get; }

    public double Value { get; }
}

/// <summary>
/// Estado congelado de una cabina para UI, pruebas y persistencia futura.
/// </summary>
public sealed class CabinSnapshot
{
    public CabinSnapshot(
        int id,
        string code,
        string segmentName,
        TravelDirection direction,
        CabinOperationalState operationalState,
        int passengerCount,
        int capacity,
        double segmentPositionMeters,
        double globalRoutePositionMeters,
        double altitudeMeters,
        double velocityMetersPerSecond,
        bool hasEmergencyBrake,
        bool hasMechanicalFailure,
        bool hasElectricalFailure,
        bool isOutOfService)
    {
        Id = id;
        Code = code;
        SegmentName = segmentName;
        Direction = direction;
        OperationalState = operationalState;
        PassengerCount = passengerCount;
        Capacity = capacity;
        SegmentPositionMeters = segmentPositionMeters;
        GlobalRoutePositionMeters = globalRoutePositionMeters;
        AltitudeMeters = altitudeMeters;
        VelocityMetersPerSecond = velocityMetersPerSecond;
        HasEmergencyBrake = hasEmergencyBrake;
        HasMechanicalFailure = hasMechanicalFailure;
        HasElectricalFailure = hasElectricalFailure;
        IsOutOfService = isOutOfService;
    }

    public int Id { get; }

    public string Code { get; }

    public string SegmentName { get; }

    public TravelDirection Direction { get; }

    public CabinOperationalState OperationalState { get; }

    public int PassengerCount { get; }

    public int Capacity { get; }

    public double SegmentPositionMeters { get; }

    public double GlobalRoutePositionMeters { get; }

    public double AltitudeMeters { get; }

    public double VelocityMetersPerSecond { get; }

    public bool HasEmergencyBrake { get; }

    public bool HasMechanicalFailure { get; }

    public bool HasElectricalFailure { get; }

    public bool IsOutOfService { get; }

    public double OccupancyPercent => Capacity <= 0 ? 0.0 : (double)PassengerCount / Capacity * 100.0;
}

/// <summary>
/// Estado congelado de una estación.
/// </summary>
public sealed class StationSnapshot
{
    public StationSnapshot(
        int id,
        string code,
        string name,
        double altitudeMeters,
        double routePositionMeters,
        int waitingAscendingPassengers,
        int waitingDescendingPassengers)
    {
        Id = id;
        Code = code;
        Name = name;
        AltitudeMeters = altitudeMeters;
        RoutePositionMeters = routePositionMeters;
        WaitingAscendingPassengers = waitingAscendingPassengers;
        WaitingDescendingPassengers = waitingDescendingPassengers;
    }

    public int Id { get; }

    public string Code { get; }

    public string Name { get; }

    public double AltitudeMeters { get; }

    public double RoutePositionMeters { get; }

    public int WaitingAscendingPassengers { get; }

    public int WaitingDescendingPassengers { get; }

    public int TotalWaitingPassengers => WaitingAscendingPassengers + WaitingDescendingPassengers;
}

/// <summary>
/// Fotografía de las series de ScottPlot.
/// </summary>
public sealed class TelemetrySnapshot
{
    public TelemetrySnapshot(
        IReadOnlyList<MetricPoint> riskSeries,
        IReadOnlyList<MetricPoint> occupancySeries,
        IReadOnlyList<MetricPoint> weatherSeries)
    {
        RiskSeries = riskSeries;
        OccupancySeries = occupancySeries;
        WeatherSeries = weatherSeries;
    }

    public IReadOnlyList<MetricPoint> RiskSeries { get; }

    public IReadOnlyList<MetricPoint> OccupancySeries { get; }

    public IReadOnlyList<MetricPoint> WeatherSeries { get; }

    public double[] RiskX => RiskSeries.Select(point => point.TimeSeconds).ToArray();

    public double[] RiskY => RiskSeries.Select(point => point.Value).ToArray();

    public double[] OccupancyX => OccupancySeries.Select(point => point.TimeSeconds).ToArray();

    public double[] OccupancyY => OccupancySeries.Select(point => point.Value).ToArray();

    public double[] WeatherX => WeatherSeries.Select(point => point.TimeSeconds).ToArray();

    public double[] WeatherY => WeatherSeries.Select(point => point.Value).ToArray();
}

/// <summary>
/// Snapshot global de simulación.
/// </summary>
public sealed class SimulationSnapshot
{
    public SimulationSnapshot(
        int tickIndex,
        TimeSpan elapsed,
        SimulationMode mode,
        string scenarioName,
        string dayProfileName,
        SystemOperationalState operationalState,
        string operationalNarrative,
        string weatherSummary,
        WeatherCondition weatherCondition,
        double currentRiskScore,
        double averageOccupancyPercent,
        int processedPassengers,
        int activeCriticalIssues,
        IReadOnlyList<CabinSnapshot> cabins,
        IReadOnlyList<StationSnapshot> stations,
        IReadOnlyList<SimulationEvent> recentEvents,
        TelemetrySnapshot telemetry)
    {
        TickIndex = tickIndex;
        Elapsed = elapsed;
        Mode = mode;
        ScenarioName = scenarioName;
        DayProfileName = dayProfileName;
        OperationalState = operationalState;
        OperationalNarrative = operationalNarrative;
        WeatherSummary = weatherSummary;
        WeatherCondition = weatherCondition;
        CurrentRiskScore = currentRiskScore;
        AverageOccupancyPercent = averageOccupancyPercent;
        ProcessedPassengers = processedPassengers;
        ActiveCriticalIssues = activeCriticalIssues;
        Cabins = cabins;
        Stations = stations;
        RecentEvents = recentEvents;
        Telemetry = telemetry;
    }

    public int TickIndex { get; }

    public TimeSpan Elapsed { get; }

    public SimulationMode Mode { get; }

    public string ScenarioName { get; }

    public string DayProfileName { get; }

    public SystemOperationalState OperationalState { get; }

    public string OperationalNarrative { get; }

    public string WeatherSummary { get; }

    public WeatherCondition WeatherCondition { get; }

    public double CurrentRiskScore { get; }

    public double AverageOccupancyPercent { get; }

    public int ProcessedPassengers { get; }

    public int ActiveCriticalIssues { get; }

    public IReadOnlyList<CabinSnapshot> Cabins { get; }

    public IReadOnlyList<StationSnapshot> Stations { get; }

    public IReadOnlyList<SimulationEvent> RecentEvents { get; }

    public TelemetrySnapshot Telemetry { get; }
}
