
using System;
using HighRiskSimulator.Core.Domain;

namespace HighRiskSimulator.Core.Simulation;

/// <summary>
/// Perfil maestro de calibración de probabilidades.
/// 
/// Se mantiene como objeto explícito para desacoplar la UI de los cálculos internos
/// del motor y para dejar una unidad serializable útil para reportes y persistencia.
/// </summary>
public sealed class SimulationRiskTuningProfile
{
    public double GlobalRiskMultiplier { get; set; } = 1.0;

    public double StormProbabilityMultiplier { get; set; } = 1.0;

    public double WindProbabilityMultiplier { get; set; } = 1.0;

    public double FogProbabilityMultiplier { get; set; } = 1.0;

    public double MechanicalWearProbabilityMultiplier { get; set; } = 1.0;

    public double CabinMechanicalFailureProbabilityMultiplier { get; set; } = 1.0;

    public double PowerOutageProbabilityMultiplier { get; set; } = 1.0;

    public double VoltageSpikeProbabilityMultiplier { get; set; } = 1.0;

    public SimulationRiskTuningProfile Clone()
    {
        return new SimulationRiskTuningProfile
        {
            GlobalRiskMultiplier = GlobalRiskMultiplier,
            StormProbabilityMultiplier = StormProbabilityMultiplier,
            WindProbabilityMultiplier = WindProbabilityMultiplier,
            FogProbabilityMultiplier = FogProbabilityMultiplier,
            MechanicalWearProbabilityMultiplier = MechanicalWearProbabilityMultiplier,
            CabinMechanicalFailureProbabilityMultiplier = CabinMechanicalFailureProbabilityMultiplier,
            PowerOutageProbabilityMultiplier = PowerOutageProbabilityMultiplier,
            VoltageSpikeProbabilityMultiplier = VoltageSpikeProbabilityMultiplier
        };
    }

    public void Normalize()
    {
        GlobalRiskMultiplier = ClampMultiplier(GlobalRiskMultiplier);
        StormProbabilityMultiplier = ClampMultiplier(StormProbabilityMultiplier);
        WindProbabilityMultiplier = ClampMultiplier(WindProbabilityMultiplier);
        FogProbabilityMultiplier = ClampMultiplier(FogProbabilityMultiplier);
        MechanicalWearProbabilityMultiplier = ClampMultiplier(MechanicalWearProbabilityMultiplier);
        CabinMechanicalFailureProbabilityMultiplier = ClampMultiplier(CabinMechanicalFailureProbabilityMultiplier);
        PowerOutageProbabilityMultiplier = ClampMultiplier(PowerOutageProbabilityMultiplier);
        VoltageSpikeProbabilityMultiplier = ClampMultiplier(VoltageSpikeProbabilityMultiplier);
    }

    public double GetWeatherSelectionMultiplier(WeatherCondition condition)
    {
        return condition switch
        {
            WeatherCondition.Storm => StormProbabilityMultiplier,
            WeatherCondition.Windy => WindProbabilityMultiplier,
            WeatherCondition.Fog => FogProbabilityMultiplier,
            _ => 1.0
        };
    }

    public string ToSummaryText()
    {
        return $"Global x{GlobalRiskMultiplier:F2} | Tormenta x{StormProbabilityMultiplier:F2} | Viento x{WindProbabilityMultiplier:F2} | Neblina x{FogProbabilityMultiplier:F2} | Desgaste x{MechanicalWearProbabilityMultiplier:F2} | Falla mecánica x{CabinMechanicalFailureProbabilityMultiplier:F2} | Corte x{PowerOutageProbabilityMultiplier:F2} | Pico x{VoltageSpikeProbabilityMultiplier:F2}";
    }

    private static double ClampMultiplier(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1.0;
        }

        return Math.Clamp(value, 0.25, 4.00);
    }
}
