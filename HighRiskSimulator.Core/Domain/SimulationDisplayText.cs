
using System;

namespace HighRiskSimulator.Core.Domain;

/// <summary>
/// Traducciones y textos visibles del dominio. Permite dejar enums técnicos en inglés
/// y, al mismo tiempo, mostrar una interfaz completamente en español.
/// </summary>
public static class SimulationDisplayText
{
    public static string ToDisplayText(this TravelDirection direction)
    {
        return direction == TravelDirection.Ascending ? "Ascenso" : "Descenso";
    }

    public static string ToDisplayText(this CabinOperationalState state)
    {
        return state switch
        {
            CabinOperationalState.IdleAtStation => "Detenida en estación",
            CabinOperationalState.Accelerating => "Acelerando",
            CabinOperationalState.Cruising => "En crucero",
            CabinOperationalState.Braking => "Frenando",
            CabinOperationalState.EmergencyBraking => "Frenado de emergencia",
            CabinOperationalState.Faulted => "Con falla",
            CabinOperationalState.OutOfService => "Fuera de servicio",
            _ => state.ToString()
        };
    }

    public static string ToDisplayText(this CabinAlertLevel level)
    {
        return level switch
        {
            CabinAlertLevel.Normal => "Normal",
            CabinAlertLevel.Alert => "Alerta",
            CabinAlertLevel.Critical => "Crítico",
            _ => level.ToString()
        };
    }

    public static string ToDisplayText(this SystemOperationalState state)
    {
        return state switch
        {
            SystemOperationalState.Ready => "Listo",
            SystemOperationalState.Running => "En ejecución",
            SystemOperationalState.Paused => "En pausa",
            SystemOperationalState.Degraded => "Operación degradada",
            SystemOperationalState.EmergencyStop => "Parada de emergencia",
            SystemOperationalState.Completed => "Jornada completada",
            _ => state.ToString()
        };
    }

    public static string ToDisplayText(this SimulationPressureMode mode)
    {
        return mode switch
        {
            SimulationPressureMode.Realistic => "Operación realista",
            SimulationPressureMode.IntensifiedTraining => "Entrenamiento intensificado",
            _ => mode.ToString()
        };
    }

    public static string ToDisplayText(this SeasonDemandBand band)
    {
        return band switch
        {
            SeasonDemandBand.Low => "Baja",
            SeasonDemandBand.Regular => "Regular",
            SeasonDemandBand.High => "Alta",
            SeasonDemandBand.Peak => "Pico",
            _ => band.ToString()
        };
    }

    public static string ToDisplayText(this WeatherCondition condition)
    {
        return condition switch
        {
            WeatherCondition.Clear => "Despejado",
            WeatherCondition.Cold => "Frío intenso",
            WeatherCondition.Fog => "Neblina densa",
            WeatherCondition.Windy => "Viento fuerte",
            WeatherCondition.Snow => "Nevada",
            WeatherCondition.Storm => "Tormenta",
            _ => condition.ToString()
        };
    }

    public static string ToDisplayText(this EventSeverity severity)
    {
        return severity switch
        {
            EventSeverity.Info => "Informativo",
            EventSeverity.Warning => "Advertencia",
            EventSeverity.Critical => "Crítico",
            EventSeverity.Catastrophic => "Catastrófico",
            _ => severity.ToString()
        };
    }

    public static string ToDisplayText(this SimulationEventType eventType)
    {
        return eventType switch
        {
            SimulationEventType.PassengerDemand => "Demanda de pasajeros",
            SimulationEventType.Overload => "Sobrecarga",
            SimulationEventType.MechanicalWear => "Desgaste mecánico",
            SimulationEventType.MechanicalFailure => "Falla mecánica",
            SimulationEventType.ElectricalFailure => "Falla eléctrica",
            SimulationEventType.VoltageSpike => "Pico de tensión",
            SimulationEventType.ExtremeWeather => "Clima extremo",
            SimulationEventType.EmergencyBrake => "Frenado de emergencia",
            SimulationEventType.CabinOutOfService => "Cabina fuera de servicio",
            SimulationEventType.SeparationLoss => "Pérdida de separación",
            SimulationEventType.Accident => "Accidente",
            SimulationEventType.ScenarioInjected => "Evento inyectado",
            _ => eventType.ToString()
        };
    }

    public static string ToClockText(this TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return time.ToString(@"hh\:mm\:ss");
        }

        return time.ToString(@"mm\:ss");
    }
}
