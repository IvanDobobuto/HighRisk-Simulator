using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.DataStructures;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Perfil operativo diario. Se usa para introducir variación natural entre días
/// sin convertir todavía el simulador en un sistema completo de demanda OD.
/// </summary>
public sealed class OperationalDayProfile
{
    public OperationalDayProfile(string name, string description, double demandMultiplier, double weatherVolatility, double incidentPressure)
    {
        Name = name;
        Description = description;
        DemandMultiplier = demandMultiplier;
        WeatherVolatility = weatherVolatility;
        IncidentPressure = incidentPressure;
    }

    public string Name { get; }

    public string Description { get; }

    public double DemandMultiplier { get; }

    public double WeatherVolatility { get; }

    public double IncidentPressure { get; }
}

/// <summary>
/// Flota de cabinas asociada a un tramo.
/// 
/// Aquí conviven dos visiones útiles:
/// - la lista circular de despacho/rotación;
/// - la lista lineal ordenable por posición física para reglas de seguridad.
/// </summary>
public sealed class SegmentFleet
{
    private readonly List<Cabin> _cabins = new();

    public SegmentFleet(TrackSegment segment)
    {
        Segment = segment;
        DispatchRing = new CabinRing();
    }

    public TrackSegment Segment { get; }

    public CabinRing DispatchRing { get; }

    public IReadOnlyList<Cabin> Cabins => _cabins;

    public void RegisterCabin(Cabin cabin)
    {
        _cabins.Add(cabin);
        RebuildDispatchRing();
    }

    public void RebuildDispatchRing()
    {
        // Se ordena por posición del ciclo ida/vuelta para conservar una semántica circular real.
        var ordered = _cabins
            .OrderBy(cabin => cabin.GetRoundTripCyclePosition(Segment.LengthMeters))
            .ToList();

        DispatchRing.Rebuild(ordered);
    }

    public IReadOnlyList<Cabin> GetCabinsOrderedByTrackPosition()
    {
        return _cabins
            .OrderBy(cabin => cabin.SegmentPositionMeters)
            .ToList();
    }
}

/// <summary>
/// Estado completo sobre el que opera el motor.
/// </summary>
public sealed class SimulationModel
{
    public SimulationModel(
        string systemName,
        string description,
        StationNetworkGraph network,
        OperationalDayProfile dayProfile,
        IReadOnlyDictionary<int, SegmentFleet> segmentFleets,
        WeatherState weatherState)
    {
        SystemName = systemName;
        Description = description;
        Network = network;
        DayProfile = dayProfile;
        SegmentFleets = segmentFleets;
        WeatherState = weatherState;

        Stations = Network.GetStationsOrderedByRoutePosition();
        Segments = Network.GetSegmentsOrderedByVisualOrder();
        Cabins = SegmentFleets.Values.SelectMany(fleet => fleet.Cabins).OrderBy(cabin => cabin.Id).ToList();
    }

    public string SystemName { get; }

    public string Description { get; }

    public StationNetworkGraph Network { get; }

    public OperationalDayProfile DayProfile { get; }

    public IReadOnlyDictionary<int, SegmentFleet> SegmentFleets { get; }

    public WeatherState WeatherState { get; }

    public IReadOnlyList<Station> Stations { get; }

    public IReadOnlyList<TrackSegment> Segments { get; }

    public IReadOnlyList<Cabin> Cabins { get; }

    public Cabin GetCabin(int cabinId)
    {
        return Cabins.First(cabin => cabin.Id == cabinId);
    }

    public Station GetStation(int stationId)
    {
        return Network.GetStation(stationId);
    }

    public TrackSegment GetSegment(int segmentId)
    {
        return Network.GetSegment(segmentId);
    }

    public SegmentFleet GetSegmentFleet(int segmentId)
    {
        return SegmentFleets[segmentId];
    }
}
