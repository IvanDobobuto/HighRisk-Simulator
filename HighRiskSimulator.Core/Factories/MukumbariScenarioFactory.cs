using System;
using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.DataStructures;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Persistence;
using HighRiskSimulator.Core.Simulation;

namespace HighRiskSimulator.Core.Factories;

/// <summary>
/// Fábrica principal del escenario inspirado en el teleférico Mukumbarí.
/// 
/// Los valores de altitud siguen referencias públicas del sistema real.
/// Las longitudes de tramo usadas aquí son aproximadas y están calibradas
/// para respetar el total global cercano a 12.5 km, priorizando un comportamiento
/// pedagógico consistente dentro del simulador.
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
                "Reproduce una jornada de alta afluencia donde Barinitas y La Montaña disparan la demanda y se fuerza una sobrecarga en el tramo inicial.",
                new List<ScheduledIncident>
                {
                    new(TimeSpan.FromSeconds(12), SimulationEventType.Overload, EventSeverity.Warning, "Pico de carga", "Se fuerza una sobrecarga controlada en la cabina del tramo Barinitas - La Montaña.", 14, "Escenario", cabinId: 1),
                    new(TimeSpan.FromSeconds(28), SimulationEventType.MechanicalFailure, EventSeverity.Critical, "Fatiga por uso intensivo", "El sistema introduce una falla mecánica breve en la segunda cabina.", 22, "Escenario", cabinId: 2),
                }),

            new(
                "electrical-blackout",
                "Falla eléctrica general",
                "Simula una caída eléctrica de red que obliga a ejecutar frenado de emergencia y degradación temporal del servicio.",
                new List<ScheduledIncident>
                {
                    new(TimeSpan.FromSeconds(15), SimulationEventType.ElectricalFailure, EventSeverity.Critical, "Corte eléctrico", "Se simula una pérdida general de energía en el sistema.", 28, "Escenario", requiresEmergencyStop: false),
                    new(TimeSpan.FromSeconds(18), SimulationEventType.EmergencyBrake, EventSeverity.Critical, "Frenado de protección", "El sistema activa frenado de emergencia para estabilizar la operación.", 18, "Escenario"),
                }),

            new(
                "andes-storm",
                "Tormenta andina en cotas altas",
                "Introduce viento fuerte y tormenta sobre los tramos altos, seguido por una degradación mecánica de la cabina superior.",
                new List<ScheduledIncident>
                {
                    new(TimeSpan.FromSeconds(10), SimulationEventType.ExtremeWeather, EventSeverity.Critical, "Entrada de tormenta", "Las condiciones meteorológicas empeoran bruscamente sobre La Aguada, Loma Redonda y Pico Espejo.", 20, "Escenario", forcedWeather: WeatherCondition.Storm),
                    new(TimeSpan.FromSeconds(22), SimulationEventType.EmergencyBrake, EventSeverity.Warning, "Reducción inmediata", "La cabina del tramo final ejecuta frenado por viento cruzado.", 12, "Escenario", cabinId: 4),
                    new(TimeSpan.FromSeconds(34), SimulationEventType.MechanicalFailure, EventSeverity.Critical, "Golpe de carga en polea", "Se induce una falla mecánica temporal en la cabina del tramo final.", 24, "Escenario", cabinId: 4),
                }),
        };
    }

    public static SimulationEngine CreateEngine(
        SimulationOptions options,
        string? scenarioId = null,
        ISimulationSnapshotRepository? repository = null)
    {
        var bootstrapRandom = new Random(options.RandomSeed);
        var dayProfile = ChooseDayProfile(bootstrapRandom);
        var network = BuildNetwork();
        SeedInitialQueues(network, bootstrapRandom, dayProfile, options);
        var weather = CreateInitialWeatherState(dayProfile);
        var segmentFleets = BuildSegmentFleets(network, bootstrapRandom, dayProfile);

        var model = new SimulationModel(
            "Mukumbarí Digital Twin",
            "Base 1D profesional para el simulador de teleférico inspirado en Mukumbarí.",
            network,
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
                "Modo estocástico reproducible por semilla, con clima, demanda, fallas y escalamiento de seguridad.",
                Array.Empty<ScheduledIncident>());
        }

        var scenario = CreateScenarioCatalog().FirstOrDefault(item => item.Id == scenarioId);
        return scenario ?? CreateScenarioCatalog().First();
    }

    private static OperationalDayProfile ChooseDayProfile(Random random)
    {
        var profiles = new List<OperationalDayProfile>
        {
            new("Baja afluencia", "Demanda turística moderada, clima más estable y menor presión operativa.", 0.75, 0.85, 0.90),
            new("Operación regular", "Jornada normal con comportamiento balanceado.", 1.00, 1.00, 1.00),
            new("Temporada alta", "Más pasajeros, más presión operativa y mayor probabilidad de desviaciones.", 1.35, 1.15, 1.20),
        };

        return profiles[random.Next(profiles.Count)];
    }

    private static StationNetworkGraph BuildNetwork()
    {
        var graph = new StationNetworkGraph();

        // Altitudes inspiradas en referencias públicas del sistema Mukumbarí.
        graph.AddStation(new Station(1, "BAR", "Barinitas", 1577, 0, TimeSpan.FromSeconds(10), 1.55));
        graph.AddStation(new Station(2, "MON", "La Montaña", 2436, 3300, TimeSpan.FromSeconds(12), 1.30));
        graph.AddStation(new Station(3, "AGU", "La Aguada", 3452, 6590, TimeSpan.FromSeconds(14), 1.10));
        graph.AddStation(new Station(4, "LRE", "Loma Redonda", 4045, 9365, TimeSpan.FromSeconds(15), 0.95));
        graph.AddStation(new Station(5, "PES", "Pico Espejo", 4765, 12500, TimeSpan.FromSeconds(18), 0.80));

        // Longitudes aproximadas que preservan un recorrido total de ~12.5 km.
        graph.AddSegment(new TrackSegment(1, "T1", "Barinitas - La Montaña", 1, 2, 3300, 5.0, 0.65, 0.75, 1.45, 160, 0.85, 1));
        graph.AddSegment(new TrackSegment(2, "T2", "La Montaña - La Aguada", 2, 3, 3290, 5.0, 0.62, 0.75, 1.45, 160, 1.00, 2));
        graph.AddSegment(new TrackSegment(3, "T3", "La Aguada - Loma Redonda", 3, 4, 2775, 4.8, 0.58, 0.72, 1.40, 150, 1.18, 3));
        graph.AddSegment(new TrackSegment(4, "T4", "Loma Redonda - Pico Espejo", 4, 5, 3135, 4.6, 0.55, 0.70, 1.35, 150, 1.30, 4));

        return graph;
    }

    private static Dictionary<int, SegmentFleet> BuildSegmentFleets(
        StationNetworkGraph network,
        Random random,
        OperationalDayProfile dayProfile)
    {
        var fleets = new Dictionary<int, SegmentFleet>();

        foreach (var segment in network.GetSegmentsOrderedByVisualOrder())
        {
            var fleet = new SegmentFleet(segment);

            // Se crea una cabina por tramo para aproximar el comportamiento descrito del Mukumbarí.
            var cabin = new Cabin(
                segment.Id,
                $"CAB-{segment.Id:00}",
                60,
                segment.Id,
                TravelDirection.Ascending,
                0,
                network.GetStation(segment.StartStationId).DefaultDwellTime);

            // La carga inicial depende del perfil del día para que el sistema sea diferente en cada reinicio.
            var lowerBound = (int)Math.Round(8 * dayProfile.DemandMultiplier);
            var upperBound = (int)Math.Round(28 * dayProfile.DemandMultiplier);
            cabin.SetPassengers(random.Next(lowerBound, Math.Max(lowerBound + 1, upperBound)));
            fleet.RegisterCabin(cabin);

            fleets[segment.Id] = fleet;
        }

        return fleets;
    }

    private static void SeedInitialQueues(
        StationNetworkGraph network,
        Random random,
        OperationalDayProfile dayProfile,
        SimulationOptions options)
    {
        foreach (var station in network.GetStationsOrderedByRoutePosition())
        {
            var baseLoad = station.DemandWeight * dayProfile.DemandMultiplier * options.DemandMultiplier;
            var ascending = random.Next((int)Math.Round(baseLoad * 4), (int)Math.Round(baseLoad * 11) + 1);
            var descending = random.Next((int)Math.Round(baseLoad * 2), (int)Math.Round(baseLoad * 8) + 1);

            // En estaciones bajas domina la demanda ascendente; en cotas altas la descendente.
            if (station.RoutePositionMeters <= 3300)
            {
                ascending = (int)Math.Round(ascending * 1.4);
            }
            else if (station.RoutePositionMeters >= 9365)
            {
                descending = (int)Math.Round(descending * 1.5);
            }

            station.EnqueuePassengers(ascending, descending);
        }
    }

    private static WeatherState CreateInitialWeatherState(OperationalDayProfile dayProfile)
    {
        var weather = new WeatherState();

        if (dayProfile.Name == "Temporada alta")
        {
            weather.Apply(WeatherCondition.Windy, 9.0, 8.0, 0.85, 0.88, 1.18);
        }
        else if (dayProfile.Name == "Baja afluencia")
        {
            weather.Apply(WeatherCondition.Clear, 4.0, 13.0, 1.0, 1.0, 1.0);
        }
        else
        {
            weather.Apply(WeatherCondition.Cold, 6.0, 10.0, 0.95, 0.95, 1.08);
        }

        return weather;
    }
}
