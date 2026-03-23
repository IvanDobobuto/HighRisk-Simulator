using System;

namespace HighRiskSimulator.Core.Domain.Models;

/// <summary>
/// Representa un tramo físico entre dos estaciones.
/// </summary>
public sealed class TrackSegment
{
    public TrackSegment(
        int id,
        string code,
        string name,
        int startStationId,
        int endStationId,
        double lengthMeters,
        double maxOperationalSpeedMetersPerSecond,
        double serviceAccelerationMetersPerSecondSquared,
        double serviceDecelerationMetersPerSecondSquared,
        double emergencyDecelerationMetersPerSecondSquared,
        double minimumSeparationMeters,
        double weatherExposureFactor,
        int visualOrder)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("El código del tramo es obligatorio.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del tramo es obligatorio.", nameof(name));
        }

        if (lengthMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthMeters), "La longitud del tramo debe ser positiva.");
        }

        if (maxOperationalSpeedMetersPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOperationalSpeedMetersPerSecond), "La velocidad máxima debe ser positiva.");
        }

        if (serviceAccelerationMetersPerSecondSquared <= 0 ||
            serviceDecelerationMetersPerSecondSquared <= 0 ||
            emergencyDecelerationMetersPerSecondSquared <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(serviceAccelerationMetersPerSecondSquared), "Las aceleraciones deben ser positivas.");
        }

        if (minimumSeparationMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSeparationMeters), "La separación mínima no puede ser negativa.");
        }

        Id = id;
        Code = code;
        Name = name;
        StartStationId = startStationId;
        EndStationId = endStationId;
        LengthMeters = lengthMeters;
        MaxOperationalSpeedMetersPerSecond = maxOperationalSpeedMetersPerSecond;
        ServiceAccelerationMetersPerSecondSquared = serviceAccelerationMetersPerSecondSquared;
        ServiceDecelerationMetersPerSecondSquared = serviceDecelerationMetersPerSecondSquared;
        EmergencyDecelerationMetersPerSecondSquared = emergencyDecelerationMetersPerSecondSquared;
        MinimumSeparationMeters = minimumSeparationMeters;
        WeatherExposureFactor = Math.Clamp(weatherExposureFactor, 0.1, 3.0);
        VisualOrder = visualOrder;
    }

    public int Id { get; }

    public string Code { get; }

    public string Name { get; }

    public int StartStationId { get; }

    public int EndStationId { get; }

    public double LengthMeters { get; }

    public double MaxOperationalSpeedMetersPerSecond { get; }

    public double ServiceAccelerationMetersPerSecondSquared { get; }

    public double ServiceDecelerationMetersPerSecondSquared { get; }

    public double EmergencyDecelerationMetersPerSecondSquared { get; }

    public double MinimumSeparationMeters { get; }

    public double WeatherExposureFactor { get; }

    /// <summary>
    /// Orden visual usado por la UI para dibujar el perfil del sistema.
    /// </summary>
    public int VisualOrder { get; }

    /// <summary>
    /// Longitud total del ciclo ida/vuelta del tramo. Este dato permite justificar el uso
    /// de una lista circular para cabinas que repiten indefinidamente el mismo circuito operativo.
    /// </summary>
    public double RoundTripCycleLengthMeters => LengthMeters * 2.0;

    public TrackSegment Clone()
    {
        return new TrackSegment(
            Id,
            Code,
            Name,
            StartStationId,
            EndStationId,
            LengthMeters,
            MaxOperationalSpeedMetersPerSecond,
            ServiceAccelerationMetersPerSecondSquared,
            ServiceDecelerationMetersPerSecondSquared,
            EmergencyDecelerationMetersPerSecondSquared,
            MinimumSeparationMeters,
            WeatherExposureFactor,
            VisualOrder);
    }

    public override string ToString()
    {
        return $"{Code} - {Name}";
    }
}
