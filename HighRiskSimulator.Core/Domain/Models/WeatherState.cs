using System;
using HighRiskSimulator.Core.Domain;

namespace HighRiskSimulator.Core.Domain.Models;

/// <summary>
/// Estado meteorológico global con helpers para obtener impacto por altitud y por tramo.
/// </summary>
public sealed class WeatherState
{
    private const double ReferenceAltitudeMeters = 1577.0;
    private const double StandardLapseRatePerMeter = 0.0065;

    public WeatherCondition Condition { get; private set; } = WeatherCondition.Clear;

    public double WindSpeedMetersPerSecond { get; private set; }

    /// <summary>
    /// Temperatura base en la estación inferior del sistema.
    /// </summary>
    public double BaseTemperatureCelsius { get; private set; }

    public double VisibilityFactor { get; private set; } = 1.0;

    public double SpeedMultiplier { get; private set; } = 1.0;

    public double RiskMultiplier { get; private set; } = 1.0;

    public double HumidityFactor { get; private set; } = 0.55;

    public double IcingRiskIndex { get; private set; }

    public double PrecipitationIntensity { get; private set; }

    /// <summary>
    /// Aplica un nuevo estado meteorológico ya resuelto por el motor.
    /// </summary>
    public void Apply(
        WeatherCondition condition,
        double windSpeedMetersPerSecond,
        double baseTemperatureCelsius,
        double visibilityFactor,
        double speedMultiplier,
        double riskMultiplier,
        double humidityFactor = 0.55,
        double icingRiskIndex = 0.0,
        double precipitationIntensity = 0.0)
    {
        Condition = condition;
        WindSpeedMetersPerSecond = Math.Max(0, windSpeedMetersPerSecond);
        BaseTemperatureCelsius = baseTemperatureCelsius;
        VisibilityFactor = Math.Clamp(visibilityFactor, 0.1, 1.0);
        SpeedMultiplier = Math.Clamp(speedMultiplier, 0.20, 1.0);
        RiskMultiplier = Math.Clamp(riskMultiplier, 1.0, 4.0);
        HumidityFactor = Math.Clamp(humidityFactor, 0.10, 1.0);
        IcingRiskIndex = Math.Clamp(icingRiskIndex, 0.0, 1.0);
        PrecipitationIntensity = Math.Clamp(precipitationIntensity, 0.0, 1.0);
    }

    public double EstimateTemperatureAtAltitude(double altitudeMeters)
    {
        var altitudeDelta = Math.Max(0, altitudeMeters - ReferenceAltitudeMeters);
        return BaseTemperatureCelsius - (altitudeDelta * StandardLapseRatePerMeter);
    }

    public double EstimateIcingRiskAtAltitude(double altitudeMeters)
    {
        var estimatedTemperature = EstimateTemperatureAtAltitude(altitudeMeters);
        var coldFactor = estimatedTemperature <= 0 ? 1.0 : Math.Clamp(1.0 - (estimatedTemperature / 8.0), 0.0, 1.0);
        return Math.Clamp((IcingRiskIndex * 0.65) + (HumidityFactor * 0.20) + (coldFactor * 0.15), 0.0, 1.0);
    }

    public double EstimateSegmentSpeedMultiplier(TrackSegment segment, double averageAltitudeMeters)
    {
        var altitudePenalty = 1.0 - (EstimateIcingRiskAtAltitude(averageAltitudeMeters) * 0.18 * segment.IcingExposureFactor);
        var exposurePenalty = 1.0 - ((1.0 - SpeedMultiplier) * segment.WeatherExposureFactor);
        return Math.Clamp(exposurePenalty * altitudePenalty, 0.22, 1.0);
    }

    public double EstimateSegmentRiskMultiplier(TrackSegment segment, double averageAltitudeMeters)
    {
        var altitudeIcing = EstimateIcingRiskAtAltitude(averageAltitudeMeters);
        var exposurePenalty = 1.0 + ((RiskMultiplier - 1.0) * segment.WeatherExposureFactor);
        return Math.Clamp(exposurePenalty + (altitudeIcing * 0.45 * segment.IcingExposureFactor), 1.0, 4.2);
    }

    public WeatherState Clone()
    {
        var clone = new WeatherState();
        clone.Apply(
            Condition,
            WindSpeedMetersPerSecond,
            BaseTemperatureCelsius,
            VisibilityFactor,
            SpeedMultiplier,
            RiskMultiplier,
            HumidityFactor,
            IcingRiskIndex,
            PrecipitationIntensity);
        return clone;
    }

    public string ToDisplayText()
    {
        return $"{Condition.ToDisplayText()} | viento {WindSpeedMetersPerSecond:F1} m/s | visibilidad {VisibilityFactor:P0} | hielo {IcingRiskIndex:P0}";
    }
}
