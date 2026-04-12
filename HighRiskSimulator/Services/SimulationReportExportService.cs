using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.Core.Simulation;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HighRiskSimulator.Services;

/// <summary>
/// Rutas de artefactos exportados para una jornada de simulacion.
/// </summary>
public sealed class ExportedSimulationArtifacts
{
    public string PdfPath { get; set; } = string.Empty;

    public string JsonPath { get; set; } = string.Empty;
}

/// <summary>
/// Servicio de exportacion de reportes a PDF y JSON.
/// Se prioriza un layout multipagina y se evita concentrar toda la jornada
/// en una sola pagina, para reducir bloqueos y errores de maquetacion.
/// </summary>
public sealed class SimulationReportExportService
{
    private const int TimelineRowsPerPage = 26;

    public ExportedSimulationArtifacts Export(SimulationRunReport report, string? outputDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var directory = ResolveOutputDirectory(report, outputDirectory);
        var baseName = BuildBaseFileName(report);
        var pdfPath = Path.Combine(directory, $"{baseName}.pdf");
        var jsonPath = Path.Combine(directory, $"{baseName}.json");

        var document = BuildDocument(report);
        document.GeneratePdf(pdfPath);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions));

        return new ExportedSimulationArtifacts
        {
            PdfPath = pdfPath,
            JsonPath = jsonPath
        };
    }

    private static IDocument BuildDocument(SimulationRunReport report)
    {
        var timelineChunks = report.Timeline.Count == 0
            ? new[] { Array.Empty<SimulationEvent>() }
            : report.Timeline
                .Chunk(TimelineRowsPerPage)
                .Select(chunk => chunk.ToArray())
                .ToArray();

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurePage(page, report, "Resumen ejecutivo");
                page.Content().PaddingVertical(8).Element(container => ComposeSummaryContent(container, report));
            });

            document.Page(page =>
            {
                ConfigurePage(page, report, "Desempeno por estacion");
                page.Content().PaddingVertical(8).Element(container => ComposeStationsContent(container, report));
            });

            document.Page(page =>
            {
                ConfigurePage(page, report, "Desempeno por cabina");
                page.Content().PaddingVertical(8).Element(container => ComposeCabinsContent(container, report));
            });

            for (var pageIndex = 0; pageIndex < timelineChunks.Length; pageIndex++)
            {
                var chunk = timelineChunks[pageIndex];
                var chunkNumber = pageIndex + 1;

                document.Page(page =>
                {
                    ConfigurePage(page, report, $"Linea de tiempo de eventos ({chunkNumber}/{timelineChunks.Length})");
                    page.Content().PaddingVertical(8).Element(container => ComposeTimelineContent(container, chunk, chunkNumber, timelineChunks.Length));
                });
            }
        });
    }

    private static void ConfigurePage(PageDescriptor page, SimulationRunReport report, string sectionTitle)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(20);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(text => text.FontSize(10).FontColor("#0F172A"));
        page.Header().Element(container => ComposePageHeader(container, report, sectionTitle));
        page.Footer().Element(container => ComposePageFooter(container, report));
    }

    private static void ComposePageHeader(IContainer container, SimulationRunReport report, string sectionTitle)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text($"Reporte de simulacion - {report.SystemName}").Bold().FontSize(18);
            column.Item().Text(sectionTitle).SemiBold().FontSize(11).FontColor("#1D4ED8");
            column.Item().Text(text =>
            {
                text.Span($"Fecha simulada: {report.SimulationDate:yyyy-MM-dd} | ");
                text.Span($"Escenario: {report.ScenarioName} | ");
                text.Span($"Perfil: {report.DayProfileName} | ");
                text.Span($"Temporada: {report.SeasonalityLabel}");
            });
            column.Item().DefaultTextStyle(style => style.FontSize(9).FontColor("#475569")).Text(text =>
            {
                text.Span($"Modo de presion: {report.PressureModeLabel} | ");
                text.Span($"Estado final: {report.FinalStateLabel} | ");
                text.Span($"Semilla base: {report.BaseSeed} | ");
                text.Span($"Variacion operacional: {report.OperationalVarianceSeed}");
            });
            column.Item().LineHorizontal(1).LineColor("#CBD5E1");
        });
    }

    private static void ComposePageFooter(IContainer container, SimulationRunReport report)
    {
        container.AlignCenter().DefaultTextStyle(style => style.FontSize(9).FontColor("#64748B")).Text(text =>
        {
            text.Span($"Generado UTC {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} | Pagina ");
            text.CurrentPageNumber();
            text.Span(" de ");
            text.TotalPages();
        });
    }

    private static void ComposeSummaryContent(IContainer container, SimulationRunReport report)
    {
        container.Column(column =>
        {
            column.Spacing(12);

            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Resumen ejecutivo").Bold().FontSize(14);
                section.Item().Text(report.ExecutiveSummary);
                section.Item().Text(report.Conclusions).FontColor("#334155");
            });

            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(8);
                section.Item().Text("Indicadores consolidados").Bold().FontSize(14);
                section.Item().Element(inner => ComposeMetricsTable(inner, report));
            });

            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Calibracion aplicada").Bold().FontSize(14);
                section.Item().Text(report.RiskCalibrationSummary);
            });

            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Lectura operacional").Bold().FontSize(14);
                section.Item().Text($"Estaciones registradas: {report.Stations.Count}. Cabinas registradas: {report.Cabins.Count}. Eventos totales: {report.TotalEvents}.");
                section.Item().Text($"Pasajeros procesados: {report.TotalProcessedPassengers}. Pasajeros diferidos: {report.TotalRejectedPassengers}. Huella causal: {report.EventualityFingerprint}.");
                section.Item().Text($"Ultimo punto de riesgo: {report.RiskSeries.LastOrDefault()?.Value.ToString("F1") ?? "0.0"}. Ultima ocupacion media: {report.OccupancySeries.LastOrDefault()?.Value.ToString("F1") ?? "0.0"}. Ultima presion climatica: {report.WeatherSeries.LastOrDefault()?.Value.ToString("F1") ?? "0.0"}.");
            });
        });
    }

    private static void ComposeMetricsTable(IContainer container, SimulationRunReport report)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.RelativeColumn();
                columns.ConstantColumn(130);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).Text("Indicador").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Valor").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Indicador").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Valor").FontColor(Colors.White).SemiBold();
            });

            AddMetricRow("Tiempo simulado", report.SimulatedElapsed.ToString(@"hh\:mm\:ss"), "Eventos totales", report.TotalEvents.ToString());
            AddMetricRow("Riesgo maximo", report.MaxRiskScore.ToString("F1"), "Riesgo promedio", report.AverageRiskScore.ToString("F1"));
            AddMetricRow("Ocupacion promedio", $"{report.AverageOccupancyPercent:F1} %", "Visibilidad promedio", $"{report.AverageVisibilityPercent:F1} %");
            AddMetricRow("Viento pico", $"{report.PeakWindSpeedMetersPerSecond:F1} m/s", "Hielo pico", $"{report.PeakIcingRiskPercent:F1} %");
            AddMetricRow("Temperatura minima", $"{report.LowestTemperatureCelsius:F1} C", "Huella causal", report.EventualityFingerprint);
            AddMetricRow("Pax procesados", report.TotalProcessedPassengers.ToString(), "Pax diferidos", report.TotalRejectedPassengers.ToString());
            AddMetricRow("Advertencias", report.WarningEvents.ToString(), "Criticos", report.CriticalEvents.ToString());
            AddMetricRow("Catastroficos", report.CatastrophicEvents.ToString(), "Cierre por emergencia", report.EndedByEmergencyStop ? "Si" : "No");

            void AddMetricRow(string leftLabel, string leftValue, string rightLabel, string rightValue)
            {
                table.Cell().Element(TableBodyCell).Text(leftLabel).SemiBold();
                table.Cell().Element(TableBodyCell).Text(leftValue);
                table.Cell().Element(TableBodyCell).Text(rightLabel).SemiBold();
                table.Cell().Element(TableBodyCell).Text(rightValue);
            }
        });
    }

    private static void ComposeStationsContent(IContainer container, SimulationRunReport report)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Resumen de estaciones").Bold().FontSize(14);
            column.Item().Text("Las colas terminales respetan reglas operativas del sistema y las estaciones intermedias acumulan flujo real de transferencia.").FontColor("#475569");
            column.Item().Element(SectionCard).Element(inner => ComposeStationsTable(inner, report.Stations));
        });
    }

    private static void ComposeStationsTable(IContainer container, IReadOnlyList<StationReportEntry> stations)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.7f);
                columns.ConstantColumn(82);
                columns.ConstantColumn(78);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(82);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(78);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).Text("Estacion").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Regla").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Altitud").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Suben").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Bajan").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Descargan").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Cola pico").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Final").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Pendientes").FontColor(Colors.White).SemiBold();
            });

            foreach (var station in stations)
            {
                table.Cell().Element(TableBodyCell).Text(station.Name);
                table.Cell().Element(TableBodyCell).Text(station.BoardingRules);
                table.Cell().Element(TableBodyCell).Text(station.AltitudeMeters.ToString("F0"));
                table.Cell().Element(TableBodyCell).Text(station.BoardedAscending.ToString());
                table.Cell().Element(TableBodyCell).Text(station.BoardedDescending.ToString());
                table.Cell().Element(TableBodyCell).Text(station.UnloadedPassengers.ToString());
                table.Cell().Element(TableBodyCell).Text(station.PeakQueue.ToString());
                table.Cell().Element(TableBodyCell).Text(station.FinalQueue.ToString());
                table.Cell().Element(TableBodyCell).Text(station.LeftWaitingPassengers.ToString());
            }
        });
    }

    private static void ComposeCabinsContent(IContainer container, SimulationRunReport report)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Resumen de cabinas").Bold().FontSize(14);
            column.Item().Text("Se registran viajes, cargas, salud remanente y activaciones de frenado de emergencia por unidad.").FontColor("#475569");
            column.Item().Element(SectionCard).Element(inner => ComposeCabinsTable(inner, report.Cabins));
        });
    }

    private static void ComposeCabinsTable(IContainer container, IReadOnlyList<CabinReportEntry> cabins)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(72);
                columns.RelativeColumn(1.6f);
                columns.ConstantColumn(70);
                columns.ConstantColumn(58);
                columns.ConstantColumn(72);
                columns.ConstantColumn(72);
                columns.ConstantColumn(72);
                columns.ConstantColumn(62);
                columns.ConstantColumn(62);
                columns.ConstantColumn(62);
                columns.ConstantColumn(68);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).Text("Codigo").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Tramo").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Alerta pico").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Viajes").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Pax subidos").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Pax bajados").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("% ocup.").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Salud M").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Salud E").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Salud F").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("F. emer.").FontColor(Colors.White).SemiBold();
            });

            foreach (var cabin in cabins)
            {
                table.Cell().Element(TableBodyCell).Text(cabin.Code);
                table.Cell().Element(TableBodyCell).Text(cabin.SegmentName);
                table.Cell().Element(TableBodyCell).Text(cabin.PeakAlertLevel);
                table.Cell().Element(TableBodyCell).Text(cabin.CompletedTrips.ToString());
                table.Cell().Element(TableBodyCell).Text(cabin.BoardedPassengers.ToString());
                table.Cell().Element(TableBodyCell).Text(cabin.UnloadedPassengers.ToString());
                table.Cell().Element(TableBodyCell).Text(cabin.PeakOccupancyPercent.ToString("F1"));
                table.Cell().Element(TableBodyCell).Text(cabin.MechanicalHealthPercent.ToString("F0"));
                table.Cell().Element(TableBodyCell).Text(cabin.ElectricalHealthPercent.ToString("F0"));
                table.Cell().Element(TableBodyCell).Text(cabin.BrakeHealthPercent.ToString("F0"));
                table.Cell().Element(TableBodyCell).Text(cabin.EmergencyBrakeActivations.ToString());
            }
        });
    }

    private static void ComposeTimelineContent(IContainer container, IReadOnlyList<SimulationEvent> timelineChunk, int chunkNumber, int totalChunks)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text($"Eventos del bloque {chunkNumber} de {totalChunks}").Bold().FontSize(14);

            if (timelineChunk.Count == 0)
            {
                column.Item().Element(SectionCard).Text("La corrida no registro eventos en la linea de tiempo exportable.");
                return;
            }

            column.Item().Element(SectionCard).Element(inner => ComposeTimelineTable(inner, timelineChunk));
        });
    }

    private static void ComposeTimelineTable(IContainer container, IReadOnlyList<SimulationEvent> timelineChunk)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(58);
                columns.ConstantColumn(86);
                columns.ConstantColumn(96);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(64);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).Text("Hora").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Severidad").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Tipo").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Titulo").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Ubicacion").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Riesgo").FontColor(Colors.White).SemiBold();
            });

            foreach (var item in timelineChunk)
            {
                table.Cell().Element(TableBodyCell).Text(item.OccurredAtDisplay);
                table.Cell().Element(TableBodyCell).Text(item.SeverityDisplay);
                table.Cell().Element(TableBodyCell).Text(item.TypeDisplay);
                table.Cell().Element(TableBodyCell).Text(item.Title);
                table.Cell().Element(TableBodyCell).Text(item.LocationDisplay);
                table.Cell().Element(TableBodyCell).Text(item.RiskDelta.ToString("F1"));
            }
        });
    }

    private static IContainer SectionCard(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor("#CBD5E1")
            .Background("#F8FAFC")
            .Padding(10);
    }

    private static IContainer TableHeaderCell(IContainer container)
    {
        return container
            .Background("#0F172A")
            .PaddingVertical(6)
            .PaddingHorizontal(5);
    }

    private static IContainer TableBodyCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor("#E2E8F0")
            .PaddingVertical(4)
            .PaddingHorizontal(5);
    }

    private static string ResolveOutputDirectory(SimulationRunReport report, string? outputDirectory)
    {
        var preferredBaseDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : outputDirectory!;

        var candidateDirectories = new[]
        {
            string.IsNullOrWhiteSpace(preferredBaseDirectory)
                ? string.Empty
                : Path.Combine(preferredBaseDirectory, "HighRiskSimulator", "Exports", report.SimulationDate.ToString("yyyyMMdd")),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HighRiskSimulator", "Exports", report.SimulationDate.ToString("yyyyMMdd")),
            Path.Combine(Path.GetTempPath(), "HighRiskSimulator", "Exports", report.SimulationDate.ToString("yyyyMMdd"))
        };

        foreach (var candidate in candidateDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch
            {
                // Se intenta la siguiente ruta valida.
            }
        }

        throw new IOException("No fue posible preparar un directorio de exportacion valido para los reportes.");
    }

    private static string BuildBaseFileName(SimulationRunReport report)
    {
        var scenarioPart = SanitizeFileName(report.ScenarioName);
        var variancePart = Math.Abs((long)report.OperationalVarianceSeed);
        var stamp = DateTime.UtcNow.ToString("HHmmssfff");
        return $"simulacion_{report.SimulationDate:yyyyMMdd}_{scenarioPart}_seed{report.BaseSeed}_var{variancePart}_{stamp}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Where(character => !invalidCharacters.Contains(character))
            .ToArray())
            .Replace(' ', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "reporte" : sanitized.ToLowerInvariant();
    }
}
