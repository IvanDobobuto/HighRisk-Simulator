using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Serie acotada para telemetría.
/// 
/// Se limita la cantidad de puntos para que la UI siga siendo fluida
/// cuando la simulación corre por períodos prolongados.
/// </summary>
public sealed class RollingMetricSeries
{
    private readonly Queue<MetricPoint> _points = new();

    public RollingMetricSeries(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "La capacidad de la serie debe ser positiva.");
        }

        Capacity = capacity;
    }

    public int Capacity { get; }

    public void Add(TimeSpan elapsed, double value)
    {
        _points.Enqueue(new MetricPoint(elapsed.TotalSeconds, value));

        while (_points.Count > Capacity)
        {
            _points.Dequeue();
        }
    }

    public IReadOnlyList<MetricPoint> Snapshot()
    {
        return _points.ToList();
    }

    public void Clear()
    {
        _points.Clear();
    }
}
