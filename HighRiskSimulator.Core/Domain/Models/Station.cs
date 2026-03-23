using System;

namespace HighRiskSimulator.Core.Domain.Models;

/// <summary>
/// Estación del sistema de teleférico.
/// 
/// La clase mezcla información estructural (nombre, altura, posición en ruta)
/// con estado operativo ligero (colas de pasajeros) para mantener esta primera
/// base sólida simple de entender y extender.
/// </summary>
public sealed class Station
{
    public Station(
        int id,
        string code,
        string name,
        double altitudeMeters,
        double routePositionMeters,
        TimeSpan defaultDwellTime,
        double demandWeight)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("El código de la estación es obligatorio.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la estación es obligatorio.", nameof(name));
        }

        if (altitudeMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(altitudeMeters), "La altitud no puede ser negativa.");
        }

        if (routePositionMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(routePositionMeters), "La posición en ruta no puede ser negativa.");
        }

        if (defaultDwellTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDwellTime), "El tiempo de permanencia no puede ser negativo.");
        }

        if (demandWeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(demandWeight), "El peso de demanda debe ser positivo.");
        }

        Id = id;
        Code = code;
        Name = name;
        AltitudeMeters = altitudeMeters;
        RoutePositionMeters = routePositionMeters;
        DefaultDwellTime = defaultDwellTime;
        DemandWeight = demandWeight;
    }

    /// <summary>
    /// Identificador interno estable.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Código corto útil para UI, trazas y persistencia futura.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Nombre completo de la estación.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Altitud sobre el nivel del mar en metros.
    /// </summary>
    public double AltitudeMeters { get; }

    /// <summary>
    /// Distancia acumulada sobre la ruta principal 1D.
    /// </summary>
    public double RoutePositionMeters { get; }

    /// <summary>
    /// Tiempo normal de parada para descarga/carga.
    /// </summary>
    public TimeSpan DefaultDwellTime { get; }

    /// <summary>
    /// Peso relativo de demanda de la estación.
    /// </summary>
    public double DemandWeight { get; }

    /// <summary>
    /// Cola agregada de pasajeros que desean ir en sentido ascendente.
    /// </summary>
    public int WaitingAscendingPassengers { get; private set; }

    /// <summary>
    /// Cola agregada de pasajeros que desean ir en sentido descendente.
    /// </summary>
    public int WaitingDescendingPassengers { get; private set; }

    /// <summary>
    /// Agrega pasajeros a las colas de la estación.
    /// </summary>
    public void EnqueuePassengers(int ascendingPassengers, int descendingPassengers)
    {
        WaitingAscendingPassengers = Math.Max(0, WaitingAscendingPassengers + Math.Max(0, ascendingPassengers));
        WaitingDescendingPassengers = Math.Max(0, WaitingDescendingPassengers + Math.Max(0, descendingPassengers));
    }

    /// <summary>
    /// Atiende parte de la cola ascendente, retornando cuántos pasajeros efectivamente abordaron.
    /// </summary>
    public int DequeueAscendingPassengers(int requested)
    {
        var served = Math.Max(0, Math.Min(requested, WaitingAscendingPassengers));
        WaitingAscendingPassengers -= served;
        return served;
    }

    /// <summary>
    /// Atiende parte de la cola descendente, retornando cuántos pasajeros efectivamente abordaron.
    /// </summary>
    public int DequeueDescendingPassengers(int requested)
    {
        var served = Math.Max(0, Math.Min(requested, WaitingDescendingPassengers));
        WaitingDescendingPassengers -= served;
        return served;
    }

    /// <summary>
    /// Limpia las colas cuando se requiere reconstruir el escenario.
    /// </summary>
    public void ClearQueues()
    {
        WaitingAscendingPassengers = 0;
        WaitingDescendingPassengers = 0;
    }

    /// <summary>
    /// Crea una copia profunda de la estación para permitir reinicios seguros del motor.
    /// </summary>
    public Station Clone()
    {
        var clone = new Station(Id, Code, Name, AltitudeMeters, RoutePositionMeters, DefaultDwellTime, DemandWeight);
        clone.EnqueuePassengers(WaitingAscendingPassengers, WaitingDescendingPassengers);
        return clone;
    }

    public override string ToString()
    {
        return $"{Code} - {Name}";
    }
}
