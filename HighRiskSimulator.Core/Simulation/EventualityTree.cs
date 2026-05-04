using System;
using System.Collections.Generic;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Árbol binario interno de contexto/eventualidades.
/// 
/// No se muestra directamente al usuario. Su objetivo es que el simulador no trate
/// los eventos como hechos aislados: el árbol conserva memoria contextual del día y
/// aporta presión causal acumulada a nuevas decisiones.
/// </summary>
public sealed class EventualityTree
{
    private readonly int _structuralSeed;
    private EventualityNode? _root;

    public EventualityTree(int structuralSeed)
    {
        _structuralSeed = structuralSeed;
    }

    public string Fingerprint => _root is null
        ? $"ctx-{NormalizeInt(_structuralSeed)}-empty"
        : $"ctx-{NormalizeInt(_structuralSeed)}-{_root.HashFragment}";

    public void SeedDailyContext(DemandSeasonalityProfile seasonality, OperationalDayProfile dayProfile, WeatherState weather, SimulationPressureMode pressureMode)
    {
        RegisterFactor(
            category: "contexto",
            label: $"{seasonality.FullDisplayName} / {dayProfile.Name}",
            pressure: 0.12 + (seasonality.DemandMultiplier - 1.0) * 0.10,
            severity: EventSeverity.Info,
            occurredAt: TimeSpan.Zero,
            tags: new[]
            {
                seasonality.Band.ToString(),
                weather.Condition.ToString(),
                pressureMode.ToString()
            });

        RegisterFactor(
            category: "clima",
            label: weather.Condition.ToDisplayText(),
            pressure: Math.Clamp((weather.RiskMultiplier - 1.0) * 0.35, 0.05, 0.40),
            severity: weather.Condition is WeatherCondition.Storm or WeatherCondition.Snow ? EventSeverity.Warning : EventSeverity.Info,
            occurredAt: TimeSpan.Zero,
            tags: new[]
            {
                weather.Condition.ToString(),
                "altitud",
                "visibilidad"
            });
    }

    public void RegisterWeatherContext(WeatherState weather, TimeSpan occurredAt)
    {
        RegisterFactor(
            category: "clima",
            label: weather.Condition.ToDisplayText(),
            pressure: Math.Clamp((weather.RiskMultiplier - 1.0) * 0.45 + (weather.IcingRiskIndex * 0.25), 0.05, 0.75),
            severity: weather.Condition is WeatherCondition.Storm ? EventSeverity.Critical : EventSeverity.Warning,
            occurredAt: occurredAt,
            tags: new[]
            {
                weather.Condition.ToString(),
                weather.IcingRiskIndex >= 0.40 ? "hielo" : "frio",
                weather.VisibilityFactor <= 0.70 ? "visibilidad-baja" : "visibilidad-media"
            });
    }

    public void RegisterEvent(SimulationEvent simulationEvent)
    {
        var tags = new List<string>
        {
            simulationEvent.Type.ToString(),
            simulationEvent.Severity.ToString(),
            simulationEvent.SourceTag
        };

        if (!string.IsNullOrWhiteSpace(simulationEvent.CabinCode))
        {
            tags.Add(simulationEvent.CabinCode!);
        }

        if (!string.IsNullOrWhiteSpace(simulationEvent.SegmentName))
        {
            tags.Add(simulationEvent.SegmentName!);
        }

        RegisterFactor(
            category: ResolveCategory(simulationEvent.Type),
            label: simulationEvent.Title,
            pressure: Math.Clamp(simulationEvent.RiskDelta / 60.0, 0.04, 0.95),
            severity: simulationEvent.Severity,
            occurredAt: simulationEvent.OccurredAt,
            tags: tags);
    }

    public double EvaluateCascadePressure(
        string category,
        TimeSpan evaluationTime,
        IEnumerable<string>? contextTags = null)
    {
        if (_root is null)
        {
            return 0.0;
        }

        var tagSet = contextTags is null
            ? Array.Empty<string>()
            : new List<string>(contextTags).ToArray();

        var raw = EvaluateNode(_root, category, tagSet, evaluationTime, depth: 0);
        return Math.Clamp(raw, 0.0, 1.75);
    }

    private void RegisterFactor(
        string category,
        string label,
        double pressure,
        EventSeverity severity,
        TimeSpan occurredAt,
        IEnumerable<string> tags)
    {
        var node = new EventualityNode(category, label, pressure, severity, occurredAt, tags, _structuralSeed);

        if (_root is null)
        {
            _root = node;
            return;
        }

        InsertNode(_root, node, depth: 0);
    }

    private void InsertNode(EventualityNode current, EventualityNode candidate, int depth)
    {
        var scoreCurrent = current.SortScore;
        var scoreCandidate = candidate.SortScore;
        var goLeft = scoreCandidate < scoreCurrent;

        if (Math.Abs(scoreCandidate - scoreCurrent) < 0.0001)
        {
            goLeft = ((candidate.HashFragment + depth + _structuralSeed) & 1) == 0;
        }

        if (goLeft)
        {
            if (current.Left is null)
            {
                current.Left = candidate;
                return;
            }

            InsertNode(current.Left, candidate, depth + 1);
            return;
        }

        if (current.Right is null)
        {
            current.Right = candidate;
            return;
        }

        InsertNode(current.Right, candidate, depth + 1);
    }

    private static double EvaluateNode(
        EventualityNode? node,
        string category,
        IReadOnlyCollection<string> tags,
        TimeSpan evaluationTime,
        int depth)
    {
        if (node is null)
        {
            return 0.0;
        }

        var ageMinutes = Math.Max(0.0, (evaluationTime - node.OccurredAt).TotalMinutes);
        var recency = 1.0 / (1.0 + (ageMinutes / 18.0));
        var depthPenalty = 1.0 / (1.0 + (depth * 0.18));

        var categoryMatch = string.Equals(node.Category, category, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        var tagMatches = 0;

        foreach (var tag in tags)
        {
            if (node.Tags.Contains(tag))
            {
                tagMatches++;
            }
        }

        var tagBoost = Math.Min(0.45, tagMatches * 0.12);
        var severityBoost = node.Severity switch
        {
            EventSeverity.Info => 0.02,
            EventSeverity.Warning => 0.08,
            EventSeverity.Critical => 0.16,
            EventSeverity.Catastrophic => 0.28,
            _ => 0.0
        };

        var contribution = node.Pressure * recency * depthPenalty * (categoryMatch + tagBoost + severityBoost);
        return contribution
            + EvaluateNode(node.Left, category, tags, evaluationTime, depth + 1)
            + EvaluateNode(node.Right, category, tags, evaluationTime, depth + 1);
    }

    private static string ResolveCategory(SimulationEventType eventType)
    {
        return eventType switch
        {
            SimulationEventType.MechanicalFailure => "mecanica",
            SimulationEventType.ElectricalFailure => "electrica",
            SimulationEventType.ExtremeWeather => "clima",
            SimulationEventType.Overload => "sobrecarga",
            SimulationEventType.EmergencyBrake => "proteccion",
            SimulationEventType.SeparationLoss => "seguridad",
            SimulationEventType.Accident => "accidente",
            _ => "operacion"
        };
    }


    private static int NormalizeInt(int value)
    {
        return value == int.MinValue ? int.MaxValue : Math.Abs(value);
    }

    private sealed class EventualityNode
    {
        public EventualityNode(
            string category,
            string label,
            double pressure,
            EventSeverity severity,
            TimeSpan occurredAt,
            IEnumerable<string> tags,
            int structuralSeed)
        {
            Category = category;
            Label = label;
            Pressure = pressure;
            Severity = severity;
            OccurredAt = occurredAt;
            Tags = new HashSet<string>(tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            HashFragment = NormalizeInt(HashCode.Combine(label, category, occurredAt.TotalSeconds, structuralSeed));
            SortScore = (pressure * 1000.0) + occurredAt.TotalSeconds + (HashFragment % 17) * 0.01;
        }

        public string Category { get; }

        public string Label { get; }

        public double Pressure { get; }

        public EventSeverity Severity { get; }

        public TimeSpan OccurredAt { get; }

        public HashSet<string> Tags { get; }

        public int HashFragment { get; }

        public double SortScore { get; }

        public EventualityNode? Left { get; set; }

        public EventualityNode? Right { get; set; }
    }
}
