using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.DataStructures;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Persistence;
using HighRiskSimulator.Core.Simulation;
using HighRiskSimulator.Core.Simulation.Seasonality;

namespace HighRiskSimulator.Core.Factories;

/// <summary>
/// Fábrica principal del escenario inspirado en el sistema Mukumbarí.
///
/// Se respetan los elementos operativos públicos más estables del sistema real:
/// 5 estaciones, 4 tramos, recorrido total cercano a 12.5 km y una cabina por sentido
/// en cada tramo como configuración por defecto. A partir de esa base se habilita una
/// configuración académica más flexible para pruebas de estrés y entrenamiento.
/// </summary>
public static class MukumbariScenarioFactory
{
    public static IReadOnlyList<ScenarioDefinition> CreateScenarioCatalog()
    {
        return new List<ScenarioDefinition>
        {
            new(
                "peak-overload",
                "Sobrecarga en temporada alta",
                "Reproduce una jornada de alta afluencia donde la estación base concentra gran parte de la demanda y se fuerza una sobrecarga controlada para validar protocolos.",
                new List<ScheduledIncident>
                {
                    new(TimeSpan.FromMinutes(22), SimulationEventType.Overload, EventSeverity.Warning, "Sobrecarga contenida en tramo inicial", "Se induce una sobrecarga temporal sobre una cabina del primer tramo para evaluar respuesta operativa.", 14, "Escenario", segmentId: 1),
                    new(TimeSpan.FromMinutes(37), SimulationEventType.MechanicalFailure, EventSeverity.Critical, "Fatiga mecánica por pico operacional", "Se simula una degradación mecánica asociada al uso intensivo del sistema durante una jornada de alta presión.", 20, "Escenario", segmentId: 1),
                }),

            new(
                "electrical-blackout",
                "Falla eléctrica general",
                "Simula una caída de red que obliga al sistema a transitar hacia frenado de protección y recuperación posterior.",
                new List<ScheduledIncident>
                {
                    new(TimeSpan.FromMinutes(18), SimulationEventType.ElectricalFailure, EventSeverity.Critical, "Pérdida de red principal", "Se simula una pérdida súbita de alimentación eléctrica sobre el sistema principal.", 26, "Escenario", requiresEmergencyStop: false),
                    new(TimeSpan.FromMinutes(19), SimulationEventType.EmergencyBrake, EventSeverity.Warning, "Protección automática del sistema", "La lógica de seguridad aplica frenado de protección mientras se estabiliza la alimentación.", 10, "Escenario"),
                }),

            new(
                "andes-storm",
                "Tormenta andina en cotas altas",
                "Introduce viento fuerte y caída de visibilidad en tramos altos, con riesgo de hielo y degradación progresiva.",
                new List<ScheduledIncident>
                {
                    new(TimeSpan.FromMinutes(14), SimulationEventType.ExtremeWeather, EventSeverity.Critical, "Entrada súbita de tormenta", "El sistema entra en un frente meteorológico severo que afecta especialmente a La Aguada, Loma Redonda y Pico Espejo.", 20, "Escenario", forcedWeather: WeatherCondition.Storm),
                    new(TimeSpan.FromMinutes(26), SimulationEventType.EmergencyBrake, EventSeverity.Warning, "Reducción operativa por viento cruzado", "Una cabina de altura reduce su operación al mínimo seguro por viento lateral y baja visibilidad.", 9, "Escenario", segmentId: 4),
                    new(TimeSpan.FromMinutes(41), SimulationEventType.MechanicalFailure, EventSeverity.Critical, "Golpe dinámico en polea guía", "Se induce una falla mecánica temporal asociada al esfuerzo extra bajo clima severo.", 22, "Escenario", segmentId: 4),
                }),
        };
    }

    public static SimulationEngine CreateEngine(
        SimulationOptions options,
        string? scenarioId = null,
        ISimulationSnapshotRepository? repository = null)
    {
        var simulationDate = options.SimulationDate == default
            ? DateTime.Today
            : options.SimulationDate.Date;

        var dateSeed = simulationDate.DayOfYear + (simulationDate.Year * 31);
        var macroRandom = new Random(options.RandomSeed ^ dateSeed);
        var microRandom = new Random(options.RandomSeed ^ options.OperationalVarianceSeed ^ dateSeed);
        var seasonalityProfile = VenezuelanTourismCalendar.Resolve(simulationDate);
        var dayProfile = ChooseDayProfile(macroRandom, seasonalityProfile, options.PressureMode);
        var network = BuildNetwork();
        var weather = CreateInitialWeatherState(microRandom, dayProfile, seasonalityProfile);

        SeedInitialQueues(network, microRandom, dayProfile, seasonalityProfile, options);
        var segmentFleets = BuildSegmentFleets(network, microRandom, dayProfile, seasonalityProfile, options);

        var model = new SimulationModel(
            "Mukumbarí Digital Twin",
            "Base estadística-operativa del simulador 2D del sistema Mukumbarí con clima, colas realistas, eventualidades y analítica de jornada.",
            simulationDate,
            network,
            seasonalityProfile,
            dayProfile,
            segmentFleets,
            weather);

        var resolvedScenario = ResolveScenario(options.Mode, scenarioId ?? options.ScenarioId);

        return new SimulationEngine(
            model,
            options,
            resolvedScenario,
            repository ?? NullSimulationSnapshotRepository.Instance);
    }

    private static ScenarioDefinition ResolveScenario(SimulationMode mode, string scenarioId)
    {
        if (mode != SimulationMode.ScriptedScenario)
        {
            return new ScenarioDefinition(
                ScenarioDefinition.NoScenarioId,
                "Simulación aleatoria inteligente",
                "Modo estocástico con memoria causal del día, presión operacional variable, clima, colas realistas y eventualidades no forzadas.",
                Array.Empty<ScheduledIncident>());
        }

        var scenario = CreateScenarioCatalog().FirstOrDefault(item => item.Id == scenarioId);
        return scenario ?? CreateScenarioCatalog().First();
    }

    private static OperationalDayProfile ChooseDayProfile(
        Random random,
        DemandSeasonalityProfile seasonalityProfile,
        SimulationPressureMode pressureMode)
    {
        var trainingFactor = pressureMode == SimulationPressureMode.IntensifiedTraining ? 1.06 : 1.0;

        var profiles = new List<(OperationalDayProfile Profile, double Weight)>
        {
            (
                new OperationalDayProfile(
                    "Operación conservadora",
                    "Jornada con flujo controlado, tiempos de visita moderados y menor volatilidad operativa.",
                    0.82 * trainingFactor,
                    0.86,
                    0.84,
                    0.96,
                    0.92),
                seasonalityProfile.Band == SeasonDemandBand.Low ? 0.42 : 0.20),

            (
                new OperationalDayProfile(
                    "Operación regular",
                    "Jornada equilibrada entre flujo turístico, clima y presión operativa.",
                    1.00 * trainingFactor,
                    1.00,
                    1.00,
                    1.00,
                    1.00),
                seasonalityProfile.Band is SeasonDemandBand.Regular or SeasonDemandBand.High ? 0.46 : 0.32),

            (
                new OperationalDayProfile(
                    "Alta afluencia",
                    "Jornada de mayor densidad de pasajeros, más transferencias y mayor sensibilidad a incidentes operativos.",
                    1.18 * trainingFactor,
                    1.12,
                    1.16,
                    1.08,
                    1.05),
                seasonalityProfile.Band is SeasonDemandBand.High or SeasonDemandBand.Peak ? 0.34 : 0.18),
        };

        var totalWeight = profiles.Sum(item => item.Weight);
        var roll = random.NextDouble() * totalWeight;
        var cumulative = 0.0;

        foreach (var item in profiles)
        {
            cumulative += item.Weight;
            if (roll <= cumulative)
            {
                return item.Profile;
            }
        }

        return profiles.Last().Profile;
    }

    private static StationNetworkGraph BuildNetwork()
    {
        var graph = new StationNetworkGraph();

        // Altitudes y estructura de estaciones calibradas con referencias públicas del sistema Mukumbarí.
        graph.AddStation(new Station(1, "BAR", "Barinitas", 1577, 0, TimeSpan.FromSeconds(12), 1.95, isLowerTerminal: true, attractionFactor: 1.15));
        graph.AddStation(new Station(2, "MON", "La Montaña", 2436, 3300, TimeSpan.FromSeconds(14), 0.95, attractionFactor: 1.05));
        graph.AddStation(new Station(3, "AGU", "La Aguada", 3452, 6590, TimeSpan.FromSeconds(15), 0.82, attractionFactor: 1.18));
        graph.AddStation(new Station(4, "LRE", "Loma Redonda", 4045, 9365, TimeSpan.FromSeconds(16), 0.76, attractionFactor: 1.28));
        graph.AddStation(new Station(5, "PES", "Pico Espejo", 4765, 12500, TimeSpan.FromSeconds(18), 0.68, isUpperTerminal: true, attractionFactor: 1.62));

        // Longitudes aproximadas que preservan un recorrido total cercano a 12.5 km.
        graph.AddSegment(new TrackSegment(1, "T1", "Barinitas - La Montaña", 1, 2, 3300, 5.0, 0.66, 0.76, 1.48, 180, 0.88, 1, allowsReverseTraversal: true, icingExposureFactor: 0.82));
        graph.AddSegment(new TrackSegment(2, "T2", "La Montaña - La Aguada", 2, 3, 3290, 5.0, 0.63, 0.75, 1.45, 180, 0.98, 2, allowsReverseTraversal: true, icingExposureFactor: 1.00));
        graph.AddSegment(new TrackSegment(3, "T3", "La Aguada - Loma Redonda", 3, 4, 2775, 4.8, 0.60, 0.72, 1.42, 170, 1.16, 3, allowsReverseTraversal: true, icingExposureFactor: 1.18));
        graph.AddSegment(new TrackSegment(4, "T4", "Loma Redonda - Pico Espejo", 4, 5, 3135, 4.6, 0.56, 0.70, 1.38, 170, 1.32, 4, allowsReverseTraversal: true, icingExposureFactor: 1.34));

        return graph;
    }

    private static Dictionary<int, SegmentFleet> BuildSegmentFleets(
        StationNetworkGraph network,
        Random random,
        OperationalDayProfile dayProfile,
        DemandSeasonalityProfile seasonalityProfile,
        SimulationOptions options)
    {
        var fleets = new Dictionary<int, SegmentFleet>();
        var nextCabinId = 1;
        var cabinsPerDirection = Math.Max(1, options.CabinsPerDirectionPerSegment);
        var overallLoadMultiplier = Math.Clamp(
            dayProfile.DemandMultiplier * seasonalityProfile.DemandMultiplier * Math.Max(0.5, options.DemandMultiplier),
            0.65,
            1.55);

        foreach (var segment in network.GetSegmentsOrderedByVisualOrder())
        {
            var fleet = new SegmentFleet(segment);

            foreach (var direction in new[] { TravelDirection.Ascending, TravelDirection.Descending })
            {
                for (var index = 0; index < cabinsPerDirection; index++)
                {
                    var position = ResolveInitialPosition(segment, direction, index, cabinsPerDirection);
                    var stationDwell = direction == TravelDirection.Ascending
                        ? network.GetStation(segment.StartStationId).DefaultDwellTime
                        : network.GetStation(segment.EndStationId).DefaultDwellTime;

                    var cabin = new Cabin(
                        nextCabinId++,
                        $"CAB-{segment.Id:00}-{(direction == TravelDirection.Ascending ? "A" : "D")}{index + 1}",
                        60,
                        segment.Id,
                        direction,
                        position,
                        stationDwell);

                    var seededPassengers = ResolveInitialPassengerLoad(segment, direction, position, overallLoadMultiplier, random);
                    cabin.SetPassengers(seededPassengers);

                    if (position > 0.5 && position < segment.LengthMeters - 0.5)
                    {
                        cabin.StartStationStop(TimeSpan.Zero);
                        cabin.UpdateMotion(position, segment.MaxOperationalSpeedMetersPerSecond * (0.52 + (random.NextDouble() * 0.12)), 0, CabinOperationalState.Cruising);
                    }
                    else
                    {
                        cabin.StartStationStop(TimeSpan.FromSeconds(stationDwell.TotalSeconds * (0.70 + (random.NextDouble() * 0.35))));
                    }

                    fleet.RegisterCabin(cabin);
                }
            }

            fleets[segment.Id] = fleet;
        }

        return fleets;
    }

    private static void SeedInitialQueues(
        StationNetworkGraph network,
        Random random,
        OperationalDayProfile dayProfile,
        DemandSeasonalityProfile seasonalityProfile,
        SimulationOptions options)
    {
        foreach (var station in network.GetStationsOrderedByRoutePosition())
        {
            station.ClearQueues();
        }

        var baseStation = network.GetStationsOrderedByRoutePosition().First(station => station.IsLowerTerminal);
        var openingBacklog = (int)Math.Round(
            (16 + (baseStation.DemandWeight * 10)) *
            dayProfile.DemandMultiplier *
            seasonalityProfile.DemandMultiplier *
            options.DemandMultiplier *
            (0.82 + (random.NextDouble() * 0.36)));

        baseStation.EnqueuePassengers(Math.Max(8, openingBacklog), 0);

        // Las estaciones intermedias y la cumbre arrancan sin colas artificiales.
        // Su crecimiento dependerá de llegadas reales de cabinas y decisiones posteriores.
    }

    private static WeatherState CreateInitialWeatherState(
        Random random,
        OperationalDayProfile dayProfile,
        DemandSeasonalityProfile seasonalityProfile)
    {
        var weather = new WeatherState();
        var roll = random.NextDouble();

        WeatherCondition condition;
        if (seasonalityProfile.Band == SeasonDemandBand.Peak)
        {
            condition = roll < 0.24 ? WeatherCondition.Windy : roll < 0.86 ? WeatherCondition.Cold : WeatherCondition.Clear;
        }
        else if (seasonalityProfile.Band == SeasonDemandBand.High)
        {
            condition = roll < 0.18 ? WeatherCondition.Windy : roll < 0.72 ? WeatherCondition.Cold : WeatherCondition.Clear;
        }
        else
        {
            condition = roll < 0.14 ? WeatherCondition.Windy : roll < 0.46 ? WeatherCondition.Cold : WeatherCondition.Clear;
        }

        var baseTemperature = condition switch
        {
            WeatherCondition.Clear => 13.8,
            WeatherCondition.Cold => 9.4,
            WeatherCondition.Windy => 8.8,
            _ => 11.0
        };

        var volatilityFactor = Math.Clamp(dayProfile.WeatherVolatility * seasonalityProfile.WeatherVolatilityMultiplier, 0.8, 1.5);

        switch (condition)
        {
            case WeatherCondition.Clear:
                weather.Apply(condition, 3.0 + (random.NextDouble() * 2.5), baseTemperature, 1.00, 1.00, 1.00, 0.45, 0.04, 0.00);
                break;
            case WeatherCondition.Cold:
                weather.Apply(condition, 5.0 + (random.NextDouble() * 2.2), baseTemperature, 0.96, 0.94, 1.06 * volatilityFactor, 0.62, 0.24, 0.08);
                break;
            case WeatherCondition.Windy:
                weather.Apply(condition, 8.0 + (random.NextDouble() * 4.5), baseTemperature, 0.88, 0.86, 1.18 * volatilityFactor, 0.58, 0.18, 0.06);
                break;
            default:
                weather.Apply(WeatherCondition.Clear, 4.2, 13.2, 1.0, 1.0, 1.0);
                break;
        }

        return weather;
    }

    private static double ResolveInitialPosition(TrackSegment segment, TravelDirection direction, int index, int cabinsPerDirection)
    {
        if (cabinsPerDirection <= 1)
        {
            return direction == TravelDirection.Ascending ? 0 : segment.LengthMeters;
        }

        var slotSpacing = segment.LengthMeters / cabinsPerDirection;
        var offset = Math.Min(segment.LengthMeters * 0.08, 120.0);
        var rawPosition = (slotSpacing * index) + offset;

        if (direction == TravelDirection.Ascending)
        {
            return Math.Clamp(index == 0 ? 0 : rawPosition, 0, segment.LengthMeters);
        }

        return Math.Clamp(index == 0 ? segment.LengthMeters : segment.LengthMeters - rawPosition, 0, segment.LengthMeters);
    }

    private static int ResolveInitialPassengerLoad(
        TrackSegment segment,
        TravelDirection direction,
        double position,
        double overallLoadMultiplier,
        Random random)
    {
        var normalizedPosition = segment.LengthMeters <= 0
            ? 0.0
            : position / segment.LengthMeters;

        var baseMin = direction == TravelDirection.Ascending ? 10 : 8;
        var baseMax = direction == TravelDirection.Ascending ? 26 : 24;

        if (segment.VisualOrder == 1 && direction == TravelDirection.Ascending)
        {
            baseMin += 8;
            baseMax += 16;
        }

        if (segment.VisualOrder == 4 && direction == TravelDirection.Descending)
        {
            baseMin += 5;
            baseMax += 10;
        }

        if (normalizedPosition > 0.15 && normalizedPosition < 0.85)
        {
            baseMin += 6;
            baseMax += 12;
        }

        var minValue = (int)Math.Round(baseMin * overallLoadMultiplier);
        var maxValue = Math.Max(minValue + 1, (int)Math.Round(baseMax * overallLoadMultiplier));
        return random.Next(minValue, maxValue + 1);
    }
}
