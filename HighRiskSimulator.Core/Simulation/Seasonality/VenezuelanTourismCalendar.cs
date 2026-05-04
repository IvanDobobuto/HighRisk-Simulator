using System;
using System.Collections.Generic;
using HighRiskSimulator.Core.Domain;

namespace HighRiskSimulator.Core.Simulation.Seasonality;

/// <summary>
/// Calendario turístico simple para Venezuela orientado a demanda del teleférico.
/// 
/// No intenta ser una agenda legal exhaustiva; su objetivo es acercar el simulador
/// a la realidad operacional considerando fines de semana, vacaciones de agosto,
/// Navidad/Año Nuevo, Carnaval, Semana Santa y feriados nacionales frecuentes.
/// </summary>
public static class VenezuelanTourismCalendar
{
    public static DemandSeasonalityProfile Resolve(DateTime simulationDate)
    {
        var date = simulationDate.Date;
        var fixedHolidayNames = BuildFixedHolidayMap(date.Year);
        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        var easterSunday = CalculateEasterSunday(date.Year);
        var carnivalMonday = easterSunday.AddDays(-48);
        var carnivalTuesday = easterSunday.AddDays(-47);
        var holyWeekStart = easterSunday.AddDays(-7);
        var holyWeekEnd = easterSunday;

        var holidayName = fixedHolidayNames.TryGetValue(date, out var namedHoliday)
            ? namedHoliday
            : null;

        var isCarnival = date == carnivalMonday || date == carnivalTuesday || date == carnivalMonday.AddDays(-1) || date == carnivalTuesday.AddDays(1);
        var isHolyWeek = date >= holyWeekStart && date <= holyWeekEnd;
        var isAugustVacation = date.Month == 8;
        var isChristmasSeason = (date.Month == 12 && date.Day >= 15) || (date.Month == 1 && date.Day <= 7);
        var isSchoolVacationShoulder = date.Month == 7 || date.Month == 9;

        var demandMultiplier = 1.0;
        var weatherVolatility = 1.0;
        var incidentPressure = 1.0;
        var band = SeasonDemandBand.Regular;
        var label = "Temporada regular";
        var description = "Jornada estándar de operación turística.";
        var isHoliday = false;

        if (isWeekend)
        {
            demandMultiplier += 0.14;
            incidentPressure += 0.05;
        }

        if (isSchoolVacationShoulder)
        {
            demandMultiplier += 0.08;
        }

        if (isAugustVacation)
        {
            demandMultiplier += 0.30;
            incidentPressure += 0.10;
            label = "Temporada vacacional de agosto";
            description = "Mayor afluencia por vacaciones escolares y turismo nacional.";
            band = SeasonDemandBand.High;
        }

        if (isChristmasSeason)
        {
            demandMultiplier += 0.34;
            incidentPressure += 0.12;
            label = "Temporada navideña";
            description = "Incremento fuerte de visitantes por vacaciones y actividades decembrinas.";
            band = SeasonDemandBand.High;
            isHoliday = true;
            holidayName ??= "Navidad / Año Nuevo";
        }

        if (isCarnival)
        {
            demandMultiplier += 0.40;
            incidentPressure += 0.15;
            weatherVolatility += 0.05;
            label = "Pico por Carnaval";
            description = "Alta afluencia por feriado largo de Carnaval.";
            band = SeasonDemandBand.Peak;
            isHoliday = true;
            holidayName = "Carnaval";
        }

        if (isHolyWeek)
        {
            demandMultiplier += 0.42;
            incidentPressure += 0.16;
            weatherVolatility += 0.08;
            label = "Pico por Semana Santa";
            description = "Máxima presión turística por asueto prolongado y horario extendido.";
            band = SeasonDemandBand.Peak;
            isHoliday = true;
            holidayName = "Semana Santa";
        }

        if (holidayName is not null && !isHoliday)
        {
            isHoliday = true;
            demandMultiplier += 0.12;
            incidentPressure += 0.05;
            label = "Feriado nacional";
            description = "Día festivo con incremento moderado de visitantes.";
            band = SeasonDemandBand.High;
        }

        if (date.Month is 1 or 2 && !isCarnival && !isHolyWeek && !isChristmasSeason && !isWeekend)
        {
            demandMultiplier -= 0.10;
            incidentPressure -= 0.05;
            label = "Baja afluencia";
            description = "Periodo de menor presión después del pico vacacional.";
            band = SeasonDemandBand.Low;
        }

        demandMultiplier = Math.Clamp(demandMultiplier, 0.70, 1.95);
        weatherVolatility = Math.Clamp(weatherVolatility, 0.85, 1.35);
        incidentPressure = Math.Clamp(incidentPressure, 0.80, 1.55);

        return new DemandSeasonalityProfile(
            label,
            description,
            band,
            demandMultiplier,
            weatherVolatility,
            incidentPressure,
            isHoliday,
            holidayName);
    }

    private static Dictionary<DateTime, string> BuildFixedHolidayMap(int year)
    {
        return new Dictionary<DateTime, string>
        {
            [new DateTime(year, 1, 1)] = "Año Nuevo",
            [new DateTime(year, 4, 19)] = "Declaración de Independencia",
            [new DateTime(year, 5, 1)] = "Día del Trabajador",
            [new DateTime(year, 6, 24)] = "Batalla de Carabobo",
            [new DateTime(year, 7, 5)] = "Firma del Acta de Independencia",
            [new DateTime(year, 7, 24)] = "Natalicio de Simón Bolívar",
            [new DateTime(year, 10, 12)] = "Resistencia Indígena",
            [new DateTime(year, 12, 24)] = "Nochebuena",
            [new DateTime(year, 12, 25)] = "Navidad",
            [new DateTime(year, 12, 31)] = "Fin de año"
        };
    }

    /// <summary>
    /// Algoritmo de Meeus/Jones/Butcher para el domingo de Pascua gregoriano.
    /// </summary>
    private static DateTime CalculateEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }
}
