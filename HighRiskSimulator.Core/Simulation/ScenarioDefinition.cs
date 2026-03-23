using System;
using System.Collections.Generic;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Incidente programado para escenarios específicos.
/// </summary>
public sealed class ScheduledIncident
{
    public ScheduledIncident(
        TimeSpan triggerAt,
        SimulationEventType eventType,
        EventSeverity severity,
        string title,
        string description,
        double riskDelta,
        string sourceTag,
        int? cabinId = null,
        int? segmentId = null,
        WeatherCondition? forcedWeather = null,
        bool requiresEmergencyStop = false)
    {
        TriggerAt = triggerAt;
        EventType = eventType;
        Severity = severity;
        Title = title;
        Description = description;
        RiskDelta = riskDelta;
        SourceTag = sourceTag;
        CabinId = cabinId;
        SegmentId = segmentId;
        ForcedWeather = forcedWeather;
        RequiresEmergencyStop = requiresEmergencyStop;
    }

    public TimeSpan TriggerAt { get; }

    public SimulationEventType EventType { get; }

    public EventSeverity Severity { get; }

    public string Title { get; }

    public string Description { get; }

    public double RiskDelta { get; }

    public string SourceTag { get; }

    public int? CabinId { get; }

    public int? SegmentId { get; }

    public WeatherCondition? ForcedWeather { get; }

    public bool RequiresEmergencyStop { get; }
}

/// <summary>
/// Definición de un escenario reproducible.
/// </summary>
public sealed class ScenarioDefinition
{
    public const string NoScenarioId = "random-intelligent";

    public ScenarioDefinition(string id, string name, string description, IReadOnlyList<ScheduledIncident> scheduledIncidents)
    {
        Id = id;
        Name = name;
        Description = description;
        ScheduledIncidents = scheduledIncidents;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<ScheduledIncident> ScheduledIncidents { get; }
}
