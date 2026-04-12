
namespace HighRiskSimulator.Core.Domain;

/// <summary>
/// Define los modos principales del simulador.
/// </summary>
public enum SimulationMode
{
    IntelligentRandom,
    ScriptedScenario
}

/// <summary>
/// Representa el sentido operativo sobre el perfil principal del sistema.
/// </summary>
public enum TravelDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Expresa el estado cinemático/operativo de una cabina.
/// </summary>
public enum CabinOperationalState
{
    IdleAtStation,
    Accelerating,
    Cruising,
    Braking,
    EmergencyBraking,
    Faulted,
    OutOfService
}

/// <summary>
/// Macroestado visible de seguridad para una cabina.
/// </summary>
public enum CabinAlertLevel
{
    Normal,
    Alert,
    Critical
}

/// <summary>
/// Estado global del motor de simulación.
/// </summary>
public enum SystemOperationalState
{
    Ready,
    Running,
    Paused,
    Degraded,
    EmergencyStop,
    Completed
}

/// <summary>
/// Perfil global de presión del simulador.
/// </summary>
public enum SimulationPressureMode
{
    Realistic,
    IntensifiedTraining
}

/// <summary>
/// Banda de estacionalidad turística/demanda.
/// </summary>
public enum SeasonDemandBand
{
    Low,
    Regular,
    High,
    Peak
}

/// <summary>
/// Condición climática simplificada para esta fase del proyecto.
/// </summary>
public enum WeatherCondition
{
    Clear,
    Cold,
    Fog,
    Windy,
    Snow,
    Storm
}

/// <summary>
/// Severidad de un evento de simulación.
/// </summary>
public enum EventSeverity
{
    Info,
    Warning,
    Critical,
    Catastrophic
}

/// <summary>
/// Catálogo principal de eventos reconocidos por el sistema.
/// </summary>
public enum SimulationEventType
{
    PassengerDemand,
    Overload,
    MechanicalWear,
    MechanicalFailure,
    ElectricalFailure,
    VoltageSpike,
    ExtremeWeather,
    EmergencyBrake,
    CabinOutOfService,
    SeparationLoss,
    Accident,
    ScenarioInjected
}
