using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.DataStructures;

/// <summary>
/// Resultado de búsqueda de ruta sobre el grafo de estaciones.
/// </summary>
public sealed class RoutePath
{
    public RoutePath(IReadOnlyList<Station> stations, IReadOnlyList<TrackSegment> segments, double totalDistanceMeters)
    {
        Stations = stations;
        Segments = segments;
        TotalDistanceMeters = totalDistanceMeters;
    }

    public IReadOnlyList<Station> Stations { get; }

    public IReadOnlyList<TrackSegment> Segments { get; }

    public double TotalDistanceMeters { get; }
}

/// <summary>
/// Grafo manual basado en listas de adyacencia dirigidas.
/// 
/// Cada tramo físico existe una sola vez, pero el grafo mantiene por separado
/// salidas y entradas para soportar crecimiento futuro, cierres unidireccionales,
/// mantenimientos parciales y análisis de rutas dirigidas.
/// </summary>
public sealed class StationNetworkGraph
{
    private readonly Dictionary<int, Station> _stations = new();
    private readonly Dictionary<int, TrackSegment> _segments = new();
    private readonly Dictionary<int, List<int>> _outgoingAdjacency = new();
    private readonly Dictionary<int, List<int>> _incomingAdjacency = new();

    public IReadOnlyList<Station> GetStationsOrderedByRoutePosition()
    {
        return _stations.Values.OrderBy(station => station.RoutePositionMeters).ToList();
    }

    public IReadOnlyList<TrackSegment> GetSegmentsOrderedByVisualOrder()
    {
        return _segments.Values.OrderBy(segment => segment.VisualOrder).ToList();
    }

    public void AddStation(Station station)
    {
        if (_stations.ContainsKey(station.Id))
        {
            throw new InvalidOperationException($"La estación {station.Id} ya existe en el grafo.");
        }

        _stations[station.Id] = station;
        _outgoingAdjacency[station.Id] = new List<int>();
        _incomingAdjacency[station.Id] = new List<int>();
    }

    public void AddSegment(TrackSegment segment, bool? bidirectional = null)
    {
        if (_segments.ContainsKey(segment.Id))
        {
            throw new InvalidOperationException($"El tramo {segment.Id} ya existe en el grafo.");
        }

        if (!_stations.ContainsKey(segment.StartStationId) || !_stations.ContainsKey(segment.EndStationId))
        {
            throw new InvalidOperationException("Ambas estaciones del tramo deben existir antes de agregar el segmento.");
        }

        _segments[segment.Id] = segment;

        _outgoingAdjacency[segment.StartStationId].Add(segment.Id);
        _incomingAdjacency[segment.EndStationId].Add(segment.Id);

        var allowReverseTraversal = bidirectional ?? segment.AllowsReverseTraversal;
        if (allowReverseTraversal)
        {
            _outgoingAdjacency[segment.EndStationId].Add(segment.Id);
            _incomingAdjacency[segment.StartStationId].Add(segment.Id);
        }
    }

    public Station GetStation(int stationId)
    {
        return _stations.TryGetValue(stationId, out var station)
            ? station
            : throw new KeyNotFoundException($"No existe la estación {stationId}.");
    }

    public TrackSegment GetSegment(int segmentId)
    {
        return _segments.TryGetValue(segmentId, out var segment)
            ? segment
            : throw new KeyNotFoundException($"No existe el tramo {segmentId}.");
    }

    public IReadOnlyList<TrackSegment> GetOutgoingSegments(int stationId)
    {
        if (!_outgoingAdjacency.TryGetValue(stationId, out var segmentIds))
        {
            return Array.Empty<TrackSegment>();
        }

        return segmentIds.Select(GetSegment).DistinctBy(segment => segment.Id).ToList();
    }

    public IReadOnlyList<TrackSegment> GetIncomingSegments(int stationId)
    {
        if (!_incomingAdjacency.TryGetValue(stationId, out var segmentIds))
        {
            return Array.Empty<TrackSegment>();
        }

        return segmentIds.Select(GetSegment).DistinctBy(segment => segment.Id).ToList();
    }

    public IReadOnlyList<TrackSegment> GetAdjacentSegments(int stationId)
    {
        return GetOutgoingSegments(stationId)
            .Concat(GetIncomingSegments(stationId))
            .DistinctBy(segment => segment.Id)
            .ToList();
    }

    public bool TryGetSegmentBetween(int stationIdA, int stationIdB, out TrackSegment? segment)
    {
        segment = _segments.Values.FirstOrDefault(candidate =>
            (candidate.StartStationId == stationIdA && candidate.EndStationId == stationIdB) ||
            (candidate.AllowsReverseTraversal && candidate.StartStationId == stationIdB && candidate.EndStationId == stationIdA));

        return segment is not null;
    }

    public RoutePath GetShortestPath(int startStationId, int endStationId)
    {
        if (!_stations.ContainsKey(startStationId) || !_stations.ContainsKey(endStationId))
        {
            throw new InvalidOperationException("Las estaciones de origen y destino deben existir.");
        }

        var distances = _stations.Keys.ToDictionary(key => key, _ => double.PositiveInfinity);
        var previousStation = new Dictionary<int, int>();
        var previousSegment = new Dictionary<int, int>();
        var unvisited = new HashSet<int>(_stations.Keys);

        distances[startStationId] = 0;

        while (unvisited.Count > 0)
        {
            var currentStationId = unvisited
                .OrderBy(stationId => distances[stationId])
                .First();

            if (double.IsPositiveInfinity(distances[currentStationId]))
            {
                break;
            }

            if (currentStationId == endStationId)
            {
                break;
            }

            unvisited.Remove(currentStationId);

            foreach (var segment in GetOutgoingSegments(currentStationId))
            {
                if (!TryResolveNeighbor(segment, currentStationId, out var neighborStationId))
                {
                    continue;
                }

                if (!unvisited.Contains(neighborStationId))
                {
                    continue;
                }

                var candidateDistance = distances[currentStationId] + segment.LengthMeters;
                if (candidateDistance < distances[neighborStationId])
                {
                    distances[neighborStationId] = candidateDistance;
                    previousStation[neighborStationId] = currentStationId;
                    previousSegment[neighborStationId] = segment.Id;
                }
            }
        }

        if (!previousStation.ContainsKey(endStationId) && startStationId != endStationId)
        {
            throw new InvalidOperationException("No existe ruta entre las estaciones indicadas.");
        }

        var stationPath = new List<Station> { GetStation(endStationId) };
        var segmentPath = new List<TrackSegment>();

        var cursor = endStationId;
        while (cursor != startStationId)
        {
            var segmentId = previousSegment[cursor];
            var parentStationId = previousStation[cursor];

            segmentPath.Add(GetSegment(segmentId));
            stationPath.Add(GetStation(parentStationId));
            cursor = parentStationId;
        }

        stationPath.Reverse();
        segmentPath.Reverse();

        return new RoutePath(stationPath, segmentPath, distances[endStationId]);
    }

    public StationNetworkGraph Clone()
    {
        var clone = new StationNetworkGraph();

        foreach (var station in GetStationsOrderedByRoutePosition())
        {
            clone.AddStation(station.Clone());
        }

        foreach (var segment in GetSegmentsOrderedByVisualOrder())
        {
            clone.AddSegment(segment.Clone(), segment.AllowsReverseTraversal);
        }

        return clone;
    }

    private static bool TryResolveNeighbor(TrackSegment segment, int currentStationId, out int neighborStationId)
    {
        if (segment.StartStationId == currentStationId)
        {
            neighborStationId = segment.EndStationId;
            return true;
        }

        if (segment.AllowsReverseTraversal && segment.EndStationId == currentStationId)
        {
            neighborStationId = segment.StartStationId;
            return true;
        }

        neighborStationId = default;
        return false;
    }
}
