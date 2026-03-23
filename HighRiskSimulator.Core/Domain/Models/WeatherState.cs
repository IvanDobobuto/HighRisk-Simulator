using System;

namespace HighRiskSimulator.Core.Domain.Models;

/// <summary>
/// Estado meteorológico global simplificado.
/// </summary>
public sealed class WeatherState
{
    public WeatherCondition Condition { get; private set; } = WeatherCondition.Clear;

    public double WindSpeedMetersPerSecond { get; private set; }

    public double TemperatureCelsius { get; private set; }

    public double VisibilityFactor { get; private set; } = 1.0;

    public double SpeedMultiplier { get; private set; } = 1.0;

    public double RiskMultiplier { get; private set; } = 1.0;

    /// <summary>
    /// Aplica un nuevo estado meteorológico ya resuelto por el motor.
    /// </summary>
    public void Apply(
        WeatherCondition condition,
        double windSpeedMetersPerSecond,
        double temperatureCelsius,
        double visibilityFactor,
        double speedMultiplier,
        double riskMultiplier)
    {
        Condition = condition;
        WindSpeedMetersPerSecond = Math.Max(0, windSpeedMetersPerSecond);
        TemperatureCelsius = temperatureCelsius;
        VisibilityFactor = Math.Clamp(visibilityFactor, 0.1, 1.0);
        SpeedMultiplier = Math.Clamp(speedMultiplier, 0.25, 1.0);
        RiskMultiplier = Math.Clamp(riskMultiplier, 1.0, 3.0);
    }

    public WeatherState Clone()
    {
        var clone = new WeatherState();
        clone.Apply(Condition, WindSpeedMetersPerSecond, TemperatureCelsius, VisibilityFactor, SpeedMultiplier, RiskMultiplier);
        return clone;
    }

    public string ToDisplayText()
    {
        return $"{Condition} | viento {WindSpeedMetersPerSecond:F1} m/s | visibilidad {VisibilityFactor:P0}";
    }
}
