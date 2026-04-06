using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.DataStructures;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Perfil operativo diario. Se usa para introducir variación natural entre días.
/// </summary>
public sealed class OperationalDayProfile
{
    public OperationalDayProfile(
        string name,
        string description,
        double demandMultiplier,
        double weatherVolatility,
        double incidentPressure,
        double transferContinuationBias,
        double returnBias)
    {
        Name = name;
        Description = description;
        DemandMultiplier = demandMultiplier;
        WeatherVolatility = weatherVolatility;
        IncidentPressure = incidentPressure;
        TransferContinuationBias = transferContinuationBias;
        ReturnBias = returnBias;
    }

    public string Name { get; }

    public string Description { get; }

    public double DemandMultiplier { get; }

    public double WeatherVolatility { get; }

    public double IncidentPressure { get; }

    /// <summary>
    /// Sesgo a que los pasajeros continúen la ruta en la misma dirección en estaciones intermedias.
    /// </summary>
    public double TransferContinuationBias { get; }

    /// <summary>
    /// Sesgo a que los pasajeros generen cola de retorno tras permanecer en la estación.
    /// </summary>
    public double ReturnBias { get; }
}

/// <summary>
/// Resultado del calendario turístico/estacional aplicado a la simulación.
/// </summary>
public sealed class DemandSeasonalityProfile
{
    public DemandSeasonalityProfile(
        string label,
        string description,
        SeasonDemandBand band,
        double demandMultiplier,
        double weatherVolatilityMultiplier,
        double incidentPressureMultiplier,
        bool isHoliday,
        string? holidayName)
    {
        Label = label;
        Description = description;
        Band = band;
        DemandMultiplier = demandMultiplier;
        WeatherVolatilityMultiplier = weatherVolatilityMultiplier;
        IncidentPressureMultiplier = incidentPressureMultiplier;
        IsHoliday = isHoliday;
        HolidayName = holidayName;
    }

    public string Label { get; }

    public string Description { get; }

    public SeasonDemandBand Band { get; }

    public double DemandMultiplier { get; }

    public double WeatherVolatilityMultiplier { get; }

    public double IncidentPressureMultiplier { get; }

    public bool IsHoliday { get; }

    public string? HolidayName { get; }

    public string FullDisplayName => IsHoliday && !string.IsNullOrWhiteSpace(HolidayName)
        ? $"{Label} ({HolidayName})"
        : Label;
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

    public void RemoveCabin(int cabinId)
    {
        var target = _cabins.FirstOrDefault(item => item.Id == cabinId);
        if (target is null)
        {
            return;
        }

        _cabins.Remove(target);
        RebuildDispatchRing();
    }

    public void RebuildDispatchRing()
    {
        var ordered = _cabins
            .OrderBy(cabin => cabin.GetRoundTripCyclePosition(Segment.LengthMeters))
            .ToList();

        DispatchRing.Rebuild(ordered);
    }

    public IReadOnlyList<Cabin> GetCabinsOrderedByTrackPosition(TravelDirection? direction = null)
    {
        IEnumerable<Cabin> query = _cabins;

        if (direction is TravelDirection specificDirection)
        {
            query = query.Where(cabin => cabin.Direction == specificDirection);
        }

        return query
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
        DateTime simulationDate,
        StationNetworkGraph network,
        DemandSeasonalityProfile seasonalityProfile,
        OperationalDayProfile dayProfile,
        IReadOnlyDictionary<int, SegmentFleet> segmentFleets,
        WeatherState weatherState)
    {
        SystemName = systemName;
        Description = description;
        SimulationDate = simulationDate;
        Network = network;
        SeasonalityProfile = seasonalityProfile;
        DayProfile = dayProfile;
        SegmentFleets = segmentFleets;
        WeatherState = weatherState;

        Stations = Network.GetStationsOrderedByRoutePosition();
        Segments = Network.GetSegmentsOrderedByVisualOrder();
        Cabins = SegmentFleets.Values.SelectMany(fleet => fleet.Cabins).OrderBy(cabin => cabin.Id).ToList();
    }

    public string SystemName { get; }

    public string Description { get; }

    public DateTime SimulationDate { get; }

    public StationNetworkGraph Network { get; }

    public DemandSeasonalityProfile SeasonalityProfile { get; }

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
