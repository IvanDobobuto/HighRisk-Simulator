using System;
using System.Collections.Generic;
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
/// Consolidado exportable para simulaciones de varios dias.
/// </summary>
public sealed class BatchSimulationReport
{
    public string SystemName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int DayCount { get; set; }

    public int BaseSeed { get; set; }

    public double AverageRiskScore { get; set; }

    public double MaxRiskScore { get; set; }

    public double AverageOccupancyPercent { get; set; }

    public double AverageVisibilityPercent { get; set; }

    public int TotalProcessedPassengers { get; set; }

    public int TotalRejectedPassengers { get; set; }

    public int TotalEvents { get; set; }

    public int WarningEvents { get; set; }

    public int CriticalEvents { get; set; }

    public int CatastrophicEvents { get; set; }

    public int EmergencyClosures { get; set; }

    public IReadOnlyList<SimulationRunReport> DailyReports { get; set; } = Array.Empty<SimulationRunReport>();
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

    public ExportedSimulationArtifacts ExportBatch(IReadOnlyList<SimulationRunReport> reports, string? outputDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(reports);

        var orderedReports = reports
            .OrderBy(report => report.SimulationDate)
            .ToList();

        if (orderedReports.Count == 0)
        {
            throw new ArgumentException("El lote de reportes no contiene dias simulados.", nameof(reports));
        }

        var batchReport = BuildBatchReport(orderedReports);
        var directory = ResolveBatchOutputDirectory(batchReport.StartDate, batchReport.EndDate, outputDirectory);
        var baseName = BuildBatchFileName(batchReport);
        var pdfPath = Path.Combine(directory, $"{baseName}.pdf");
        var jsonPath = Path.Combine(directory, $"{baseName}.json");

        var document = BuildBatchDocument(batchReport);
        document.GeneratePdf(pdfPath);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(batchReport, jsonOptions));

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
                ConfigurePage(page, report, "Guia de lectura del reporte");
                page.Content().PaddingVertical(8).Element(container => ComposeReadingGuideContent(container, report));
            });

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

    private static IDocument BuildBatchDocument(BatchSimulationReport batchReport)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigureBatchPage(page, batchReport, "Guia de lectura del reporte por lote");
                page.Content().PaddingVertical(8).Element(container => ComposeBatchReadingGuideContent(container, batchReport));
            });

            document.Page(page =>
            {
                ConfigureBatchPage(page, batchReport, "Resumen global por fechas");
                page.Content().PaddingVertical(8).Element(container => ComposeBatchSummaryContent(container, batchReport));
            });

            document.Page(page =>
            {
                ConfigureBatchPage(page, batchReport, "Tabla diaria de resultados");
                page.Content().PaddingVertical(8).Element(container => ComposeBatchDailyContent(container, batchReport));
            });

            document.Page(page =>
            {
                ConfigureBatchPage(page, batchReport, "Eventos relevantes del lote");
                page.Content().PaddingVertical(8).Element(container => ComposeBatchEventContent(container, batchReport));
            });

            foreach (var dailyReport in batchReport.DailyReports.OrderBy(report => report.SimulationDate))
            {
                document.Page(page =>
                {
                    ConfigureBatchDailyRunPage(page, batchReport, dailyReport, "Resumen diario");
                    page.Content().PaddingVertical(8).Element(container => ComposeBatchDayOverviewContent(container, dailyReport));
                });

                document.Page(page =>
                {
                    ConfigureBatchDailyRunPage(page, batchReport, dailyReport, "Estaciones del dia");
                    page.Content().PaddingVertical(8).Element(container => ComposeStationsContent(container, dailyReport));
                });

                document.Page(page =>
                {
                    ConfigureBatchDailyRunPage(page, batchReport, dailyReport, "Cabinas del dia");
                    page.Content().PaddingVertical(8).Element(container => ComposeCabinsContent(container, dailyReport));
                });

                var timelineChunks = dailyReport.Timeline.Count == 0
                    ? new[] { Array.Empty<SimulationEvent>() }
                    : dailyReport.Timeline
                        .Chunk(TimelineRowsPerPage)
                        .Select(chunk => chunk.ToArray())
                        .ToArray();

                for (var pageIndex = 0; pageIndex < timelineChunks.Length; pageIndex++)
                {
                    var chunk = timelineChunks[pageIndex];
                    var chunkNumber = pageIndex + 1;
                    document.Page(page =>
                    {
                        ConfigureBatchDailyRunPage(page, batchReport, dailyReport, $"Eventos del dia ({chunkNumber}/{timelineChunks.Length})");
                        page.Content().PaddingVertical(8).Element(container => ComposeTimelineContent(container, chunk, chunkNumber, timelineChunks.Length));
                    });
                }
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

    private static void ConfigureBatchPage(PageDescriptor page, BatchSimulationReport batchReport, string sectionTitle)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(20);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(text => text.FontSize(10).FontColor("#0F172A"));
        page.Header().Element(container => ComposeBatchPageHeader(container, batchReport, sectionTitle));
        page.Footer().Element(container => ComposeBatchPageFooter(container, batchReport));
    }

    private static void ConfigureBatchDailyRunPage(PageDescriptor page, BatchSimulationReport batchReport, SimulationRunReport dailyReport, string sectionTitle)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(20);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(text => text.FontSize(10).FontColor("#0F172A"));
        page.Header().Element(container => ComposeBatchDailyRunPageHeader(container, batchReport, dailyReport, sectionTitle));
        page.Footer().Element(container => ComposeBatchPageFooter(container, batchReport));
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

    private static void ComposeBatchPageHeader(IContainer container, BatchSimulationReport batchReport, string sectionTitle)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text($"Reporte por lotes - {batchReport.SystemName}").Bold().FontSize(18);
            column.Item().Text(sectionTitle).SemiBold().FontSize(11).FontColor("#1D4ED8");
            column.Item().Text($"Periodo simulado: {batchReport.StartDate:yyyy-MM-dd} a {batchReport.EndDate:yyyy-MM-dd} | Dias: {batchReport.DayCount} | Semilla base: {batchReport.BaseSeed}");
            column.Item().LineHorizontal(1).LineColor("#CBD5E1");
        });
    }

    private static void ComposeBatchDailyRunPageHeader(IContainer container, BatchSimulationReport batchReport, SimulationRunReport dailyReport, string sectionTitle)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text($"Reporte por lotes - {batchReport.SystemName}").Bold().FontSize(18);
            column.Item().Text($"{sectionTitle} | Dia {dailyReport.SimulationDate:yyyy-MM-dd}").SemiBold().FontSize(11).FontColor("#1D4ED8");
            column.Item().Text(text =>
            {
                text.Span($"Periodo: {batchReport.StartDate:yyyy-MM-dd} a {batchReport.EndDate:yyyy-MM-dd} | ");
                text.Span($"Escenario: {dailyReport.ScenarioName} | Perfil: {dailyReport.DayProfileName} | Estado: {dailyReport.FinalStateLabel}");
            });
            column.Item().LineHorizontal(1).LineColor("#CBD5E1");
        });
    }

    private static void ComposeBatchPageFooter(IContainer container, BatchSimulationReport batchReport)
    {
        container.AlignCenter().DefaultTextStyle(style => style.FontSize(9).FontColor("#64748B")).Text(text =>
        {
            text.Span($"Generado UTC {batchReport.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} | Pagina ");
            text.CurrentPageNumber();
            text.Span(" de ");
            text.TotalPages();
        });
    }

    private static void ComposeReadingGuideContent(IContainer container, SimulationRunReport report)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Como leer este reporte").Bold().FontSize(14);
                section.Item().Text("Esta pagina funciona como glosario y hoja de ruta del PDF. Resume el significado de los terminos, indicadores y tablas exportadas para que cualquier usuario pueda interpretar la simulacion sin revisar el codigo fuente.");
                section.Item().DefaultTextStyle(style => style.FontSize(9).FontColor("#475569")).Text(text =>
                {
                    text.Span("Alcance: ").SemiBold();
                    text.Span($"se reporta la jornada del sistema {report.SystemName} para la fecha {report.SimulationDate:yyyy-MM-dd}. Los valores son resultados consolidados de la corrida simulada.");
                });
            });

            column.Item().Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Element(SectionCard).Column(section =>
                {
                    section.Spacing(4);
                    section.Item().Text("Resumen e indicadores").Bold().FontSize(13);
                    AddGuideLine(section, "Tiempo simulado", "duracion total recorrida por el motor de simulacion.");
                    AddGuideLine(section, "Riesgo maximo / promedio", "pico y media del score de riesgo del sistema en escala 0 a 100.");
                    AddGuideLine(section, "Ocupacion promedio", "porcentaje medio de llenado de cabinas durante la jornada.");
                    AddGuideLine(section, "Visibilidad, viento, hielo y temperatura", "condiciones ambientales mas relevantes observadas.");
                    AddGuideLine(section, "Pax atendidos", "pasajeros de entrada al sistema que lograron embarcar desde la terminal base; evita contar el mismo pasajero varias veces por transbordos.");
                    AddGuideLine(section, "Pendientes cierre", "pasajeros que quedaron en cola al finalizar la simulacion.");
                    AddGuideLine(section, "Huella causal", "identificador de la cadena de eventualidades registrada por el arbol causal.");
                });

                row.RelativeItem().Element(SectionCard).Column(section =>
                {
                    section.Spacing(4);
                    section.Item().Text("Tablas exportadas").Bold().FontSize(13);
                    AddGuideLine(section, "Estaciones", "muestra pasajeros que ingresan a cola, embarcan, bajan de cabina, cola pico y pasajeros pendientes por estacion.");
                    AddGuideLine(section, "Ingresan", "pasajeros generados en la cola de la estacion por demanda, retorno o transferencia.");
                    AddGuideLine(section, "Embarcan", "pasajeros que subieron efectivamente a una cabina en esa estacion.");
                    AddGuideLine(section, "Bajan cab.", "pasajeros que llegaron y descendieron de cabina en esa estacion; puede incluir personas que abordaron en estaciones anteriores.");
                    AddGuideLine(section, "Cabinas", "registra viajes, pasajeros transportados por unidad, ocupacion pico y salud mecanica/electrica/frenos.");
                    AddGuideLine(section, "Linea de tiempo", "lista eventos con hora, severidad, tipo, ubicacion y delta de riesgo.");
                });
            });
        });
    }

    private static void ComposeBatchReadingGuideContent(IContainer container, BatchSimulationReport batchReport)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Como leer el reporte por lote").Bold().FontSize(14);
                section.Item().Text("Este PDF resume varias jornadas consecutivas. Primero muestra indicadores agregados del periodo y luego desglosa resultados diarios, eventos relevantes y el detalle de cada jornada del lote.");
                section.Item().DefaultTextStyle(style => style.FontSize(9).FontColor("#475569")).Text(text =>
                {
                    text.Span("Periodo: ").SemiBold();
                    text.Span($"desde {batchReport.StartDate:yyyy-MM-dd} hasta {batchReport.EndDate:yyyy-MM-dd}. Cada dia conserva escenario, perfil, estado final, estaciones, cabinas y eventos propios.");
                });
            });

            column.Item().Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Element(SectionCard).Column(section =>
                {
                    section.Spacing(4);
                    section.Item().Text("Indicadores globales del lote").Bold().FontSize(13);
                    AddGuideLine(section, "Dias simulados / semilla base", "cantidad de jornadas consolidadas y base pseudoaleatoria del conjunto.");
                    AddGuideLine(section, "Riesgo promedio / maximo", "media del periodo y peor pico observado entre todas las jornadas.");
                    AddGuideLine(section, "Pax atendidos", "suma de pasajeros de entrada atendidos por dia, sin multiplicar por transbordos internos.");
                    AddGuideLine(section, "Pendientes cierre", "suma de colas finales de cada jornada.");
                    AddGuideLine(section, "Eventos y cierres", "conteo total de incidentes y jornadas que terminaron en emergencia.");
                });

                row.RelativeItem().Element(SectionCard).Column(section =>
                {
                    section.Spacing(4);
                    section.Item().Text("Detalle diario").Bold().FontSize(13);
                    AddGuideLine(section, "Tabla diaria", "permite comparar fecha, perfil, estado, riesgo, pasajeros atendidos y eventos por dia.");
                    AddGuideLine(section, "Eventos relevantes", "ordena los incidentes mas importantes del lote completo.");
                    AddGuideLine(section, "Secciones por dia", "despues del resumen global, el PDF agrega resumen, estaciones, cabinas y linea de tiempo de cada jornada en orden cronologico.");
                    AddGuideLine(section, "Consejo", "use primero el resumen global para detectar tendencia y luego las paginas de cada dia para revisar causas puntuales.");
                });
            });
        });
    }

    private static void AddGuideLine(ColumnDescriptor column, string term, string description)
    {
        column.Item().DefaultTextStyle(style => style.FontSize(9.2f)).Text(text =>
        {
            text.Span($"{term}: ").SemiBold();
            text.Span(description);
        });
    }

    private static void ComposeBatchSummaryContent(IContainer container, BatchSimulationReport batchReport)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Resumen ejecutivo del lote").Bold().FontSize(14);
                section.Item().Text($"Se simularon {batchReport.DayCount} jornadas entre {batchReport.StartDate:yyyy-MM-dd} y {batchReport.EndDate:yyyy-MM-dd}. El riesgo promedio global fue {batchReport.AverageRiskScore:F1}/100 y el riesgo maximo observado fue {batchReport.MaxRiskScore:F1}/100.");
                section.Item().Text($"El lote atendio {batchReport.TotalProcessedPassengers} pasajeros de entrada al sistema y cerro con {batchReport.TotalRejectedPassengers} pasajeros pendientes y registro {batchReport.TotalEvents} eventos. Cierres por emergencia: {batchReport.EmergencyClosures}.").FontColor("#334155");
            });

            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(8);
                section.Item().Text("Indicadores globales").Bold().FontSize(14);
                section.Item().Element(inner => ComposeBatchMetricsTable(inner, batchReport));
            });
        });
    }

    private static void ComposeBatchMetricsTable(IContainer container, BatchSimulationReport batchReport)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(140);
                columns.RelativeColumn();
                columns.ConstantColumn(140);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).Text("Indicador").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Valor").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Indicador").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Valor").FontColor(Colors.White).SemiBold();
            });

            AddMetricRow("Dias simulados", batchReport.DayCount.ToString(), "Semilla base", batchReport.BaseSeed.ToString());
            AddMetricRow("Riesgo promedio", batchReport.AverageRiskScore.ToString("F1"), "Riesgo maximo", batchReport.MaxRiskScore.ToString("F1"));
            AddMetricRow("Ocupacion promedio", $"{batchReport.AverageOccupancyPercent:F1} %", "Visibilidad promedio", $"{batchReport.AverageVisibilityPercent:F1} %");
            AddMetricRow("Pax atendidos", batchReport.TotalProcessedPassengers.ToString(), "Pendientes cierre", batchReport.TotalRejectedPassengers.ToString());
            AddMetricRow("Eventos totales", batchReport.TotalEvents.ToString(), "Cierres emergencia", batchReport.EmergencyClosures.ToString());
            AddMetricRow("Advertencias", batchReport.WarningEvents.ToString(), "Criticos/Catastroficos", $"{batchReport.CriticalEvents}/{batchReport.CatastrophicEvents}");

            void AddMetricRow(string leftLabel, string leftValue, string rightLabel, string rightValue)
            {
                table.Cell().Element(TableBodyCell).Text(leftLabel).SemiBold();
                table.Cell().Element(TableBodyCell).Text(leftValue);
                table.Cell().Element(TableBodyCell).Text(rightLabel).SemiBold();
                table.Cell().Element(TableBodyCell).Text(rightValue);
            }
        });
    }

    private static void ComposeBatchDayOverviewContent(IContainer container, SimulationRunReport report)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text($"Resumen de jornada - {report.SimulationDate:yyyy-MM-dd}").Bold().FontSize(14);
                section.Item().Text(report.ExecutiveSummary);
                section.Item().Text(report.Conclusions).FontColor("#334155");
            });

            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(8);
                section.Item().Text("Indicadores del dia").Bold().FontSize(14);
                section.Item().Element(inner => ComposeMetricsTable(inner, report));
            });

            column.Item().Element(SectionCard).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Eventos del dia").Bold().FontSize(14);
                section.Item().Text($"Eventos totales: {report.TotalEvents}. Advertencias: {report.WarningEvents}. Criticos: {report.CriticalEvents}. Catastroficos: {report.CatastrophicEvents}. Cierre por emergencia: {(report.EndedByEmergencyStop ? "Si" : "No")}.");
                section.Item().Text($"Huella causal: {report.EventualityFingerprint}. Calibracion: {report.RiskCalibrationSummary}").FontColor("#475569");
            });
        });
    }

    private static void ComposeBatchDailyContent(IContainer container, BatchSimulationReport batchReport)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Resultados por jornada").Bold().FontSize(14);
            column.Item().Element(SectionCard).Element(inner => ComposeBatchDailyTable(inner, batchReport.DailyReports));
        });
    }

    private static void ComposeBatchDailyTable(IContainer container, IReadOnlyList<SimulationRunReport> reports)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(78);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(66);
                columns.ConstantColumn(66);
                columns.RelativeColumn(1.2f);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).Text("Fecha").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Perfil").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Estado").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("R. max").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("R. prom").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Pax atend.").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Eventos").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Crit.").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Emerg.").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Temporada").FontColor(Colors.White).SemiBold();
            });

            foreach (var report in reports)
            {
                table.Cell().Element(TableBodyCell).Text(report.SimulationDate.ToString("yyyy-MM-dd"));
                table.Cell().Element(TableBodyCell).Text(report.DayProfileName);
                table.Cell().Element(TableBodyCell).Text(report.FinalStateLabel);
                table.Cell().Element(TableBodyCell).Text(report.MaxRiskScore.ToString("F1"));
                table.Cell().Element(TableBodyCell).Text(report.AverageRiskScore.ToString("F1"));
                table.Cell().Element(TableBodyCell).Text(report.TotalProcessedPassengers.ToString());
                table.Cell().Element(TableBodyCell).Text(report.TotalEvents.ToString());
                table.Cell().Element(TableBodyCell).Text(report.CriticalEvents.ToString());
                table.Cell().Element(TableBodyCell).Text(report.EndedByEmergencyStop ? "Si" : "No");
                table.Cell().Element(TableBodyCell).Text(report.SeasonalityLabel);
            }
        });
    }

    private static void ComposeBatchEventContent(IContainer container, BatchSimulationReport batchReport)
    {
        var relevantEvents = batchReport.DailyReports
            .SelectMany(report => report.Timeline.Select(item => (Report: report, Event: item)))
            .OrderByDescending(item => item.Event.Severity)
            .ThenByDescending(item => Math.Abs(item.Event.RiskDelta))
            .Take(32)
            .ToList();

        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Eventos mas relevantes del periodo").Bold().FontSize(14);

            if (relevantEvents.Count == 0)
            {
                column.Item().Element(SectionCard).Text("El lote no registro eventos relevantes exportables.");
                return;
            }

            column.Item().Element(SectionCard).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(82);
                    columns.ConstantColumn(58);
                    columns.ConstantColumn(86);
                    columns.ConstantColumn(96);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.2f);
                    columns.ConstantColumn(64);
                });

                table.Header(header =>
                {
                    header.Cell().Element(TableHeaderCell).Text("Fecha").FontColor(Colors.White).SemiBold();
                    header.Cell().Element(TableHeaderCell).Text("Hora").FontColor(Colors.White).SemiBold();
                    header.Cell().Element(TableHeaderCell).Text("Severidad").FontColor(Colors.White).SemiBold();
                    header.Cell().Element(TableHeaderCell).Text("Tipo").FontColor(Colors.White).SemiBold();
                    header.Cell().Element(TableHeaderCell).Text("Titulo").FontColor(Colors.White).SemiBold();
                    header.Cell().Element(TableHeaderCell).Text("Ubicacion").FontColor(Colors.White).SemiBold();
                    header.Cell().Element(TableHeaderCell).Text("Riesgo").FontColor(Colors.White).SemiBold();
                });

                foreach (var item in relevantEvents)
                {
                    table.Cell().Element(TableBodyCell).Text(item.Report.SimulationDate.ToString("yyyy-MM-dd"));
                    table.Cell().Element(TableBodyCell).Text(item.Event.OccurredAtDisplay);
                    table.Cell().Element(TableBodyCell).Text(item.Event.SeverityDisplay);
                    table.Cell().Element(TableBodyCell).Text(item.Event.TypeDisplay);
                    table.Cell().Element(TableBodyCell).Text(item.Event.Title);
                    table.Cell().Element(TableBodyCell).Text(item.Event.LocationDisplay);
                    table.Cell().Element(TableBodyCell).Text(item.Event.RiskDelta.ToString("F1"));
                }
            });
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
                section.Item().Text($"Pasajeros atendidos: {report.TotalProcessedPassengers}. Pendientes al cierre: {report.TotalRejectedPassengers}. Huella causal: {report.EventualityFingerprint}.");
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
            AddMetricRow("Pax atendidos", report.TotalProcessedPassengers.ToString(), "Pendientes cierre", report.TotalRejectedPassengers.ToString());
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
            column.Item().Text("La tabla resume entradas a cola, embarques efectivos, llegadas a estación y colas pendientes al cierre. Las estaciones intermedias pueden recibir pasajeros que abordaron en estaciones anteriores.").FontColor("#475569");
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
                columns.ConstantColumn(86);
                columns.ConstantColumn(72);
                columns.ConstantColumn(86);
                columns.ConstantColumn(86);
                columns.ConstantColumn(90);
                columns.ConstantColumn(82);
                columns.ConstantColumn(86);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCell).Text("Estacion").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Regla").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Altitud").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Ingresan").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Embarcan").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Bajan cab.").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Cola pico").FontColor(Colors.White).SemiBold();
                header.Cell().Element(TableHeaderCell).Text("Pendientes").FontColor(Colors.White).SemiBold();
            });

            foreach (var station in stations)
            {
                var generated = station.GeneratedAscendingQueue + station.GeneratedDescendingQueue;
                var boarded = station.BoardedAscending + station.BoardedDescending;
                table.Cell().Element(TableBodyCell).Text(station.Name);
                table.Cell().Element(TableBodyCell).Text(station.BoardingRules);
                table.Cell().Element(TableBodyCell).Text(station.AltitudeMeters.ToString("F0"));
                table.Cell().Element(TableBodyCell).Text(generated.ToString());
                table.Cell().Element(TableBodyCell).Text(boarded.ToString());
                table.Cell().Element(TableBodyCell).Text(station.UnloadedPassengers.ToString());
                table.Cell().Element(TableBodyCell).Text(station.PeakQueue.ToString());
                table.Cell().Element(TableBodyCell).Text(station.FinalQueue.ToString());
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

    private static BatchSimulationReport BuildBatchReport(IReadOnlyList<SimulationRunReport> reports)
    {
        return new BatchSimulationReport
        {
            SystemName = reports.First().SystemName,
            StartDate = reports.First().SimulationDate.Date,
            EndDate = reports.Last().SimulationDate.Date,
            GeneratedAtUtc = DateTime.UtcNow,
            DayCount = reports.Count,
            BaseSeed = reports.First().BaseSeed,
            AverageRiskScore = reports.Average(report => report.AverageRiskScore),
            MaxRiskScore = reports.Max(report => report.MaxRiskScore),
            AverageOccupancyPercent = reports.Average(report => report.AverageOccupancyPercent),
            AverageVisibilityPercent = reports.Average(report => report.AverageVisibilityPercent),
            TotalProcessedPassengers = reports.Sum(report => report.TotalProcessedPassengers),
            TotalRejectedPassengers = reports.Sum(report => report.TotalRejectedPassengers),
            TotalEvents = reports.Sum(report => report.TotalEvents),
            WarningEvents = reports.Sum(report => report.WarningEvents),
            CriticalEvents = reports.Sum(report => report.CriticalEvents),
            CatastrophicEvents = reports.Sum(report => report.CatastrophicEvents),
            EmergencyClosures = reports.Count(report => report.EndedByEmergencyStop),
            DailyReports = reports.ToList()
        };
    }

    private static string ResolveBatchOutputDirectory(DateTime startDate, DateTime endDate, string? outputDirectory)
    {
        var preferredBaseDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : outputDirectory!;

        var rangeFolder = $"{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
        var candidateDirectories = new[]
        {
            string.IsNullOrWhiteSpace(preferredBaseDirectory)
                ? string.Empty
                : Path.Combine(preferredBaseDirectory, "HighRiskSimulator", "Exports", "Batch", rangeFolder),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HighRiskSimulator", "Exports", "Batch", rangeFolder),
            Path.Combine(Path.GetTempPath(), "HighRiskSimulator", "Exports", "Batch", rangeFolder)
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

        throw new IOException("No fue posible preparar un directorio de exportacion valido para el lote de reportes.");
    }

    private static string BuildBatchFileName(BatchSimulationReport report)
    {
        var stamp = DateTime.UtcNow.ToString("HHmmssfff");
        return $"simulation_batch_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}_seed{report.BaseSeed}_{stamp}";
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
        return $"simulation_{report.SimulationDate:yyyyMMdd}_{scenarioPart}_seed{report.BaseSeed}_var{variancePart}_{stamp}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Where(character => !invalidCharacters.Contains(character))
            .ToArray())
            .Replace(' ', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "report" : sanitized.ToLowerInvariant();
    }
}
