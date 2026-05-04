using System;
using HighRiskSimulator.Core.Domain;

namespace HighRiskSimulator.Core.Domain.Models;

/// <summary>
/// Registro inmutable de un evento del motor.
/// </summary>
public sealed class SimulationEvent
{
    public SimulationEvent(
        long id,
        TimeSpan occurredAt,
        SimulationEventType type,
        EventSeverity severity,
        string title,
        string description,
        double riskDelta,
        string sourceTag,
        string? cabinCode = null,
        string? segmentName = null,
        string? stationName = null,
        bool requiresEmergencyStop = false)
    {
        Id = id;
        OccurredAt = occurredAt;
        Type = type;
        Severity = severity;
        Title = title;
        Description = description;
        RiskDelta = riskDelta;
        SourceTag = sourceTag;
        CabinCode = cabinCode;
        SegmentName = segmentName;
        StationName = stationName;
        RequiresEmergencyStop = requiresEmergencyStop;
    }

    public long Id { get; }

    public TimeSpan OccurredAt { get; }

    public SimulationEventType Type { get; }

    public EventSeverity Severity { get; }

    public string Title { get; }

    public string Description { get; }

    public double RiskDelta { get; }

    /// <summary>
    /// Origen lógico del evento: Aleatorio, Escenario, Seguridad, etc.
    /// </summary>
    public string SourceTag { get; }

    public string? CabinCode { get; }

    public string? SegmentName { get; }

    public string? StationName { get; }

    public bool RequiresEmergencyStop { get; }

    public string SeverityDisplay => Severity.ToDisplayText();

    public string TypeDisplay => Type.ToDisplayText();

    public string OccurredAtDisplay => OccurredAt.ToString(@"hh\:mm\:ss");

    public string LocationDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(StationName))
            {
                return StationName!;
            }

            if (!string.IsNullOrWhiteSpace(SegmentName))
            {
                return SegmentName!;
            }

            return "Sistema";
        }
    }

    public override string ToString()
    {
        return $"[{OccurredAtDisplay}] {SeverityDisplay} - {Title}";
    }
}
