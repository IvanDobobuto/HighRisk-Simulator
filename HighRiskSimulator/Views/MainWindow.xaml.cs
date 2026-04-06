using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.ViewModels;

namespace HighRiskSimulator.Views;

/// <summary>
/// Ventana principal.
///
/// El code-behind se utiliza únicamente para tareas de presentación:
/// - dibujar la vista 1D del recorrido;
/// - actualizar ScottPlot con seguimiento automático de la ventana temporal.
/// </summary>
public partial class MainWindow : Window
{
    private const double TelemetryWindowSeconds = 420;
    private SimulationSnapshot? _lastSnapshot;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();
        viewModel.SnapshotUpdated += ViewModelOnSnapshotUpdated;
        DataContext = viewModel;

        Loaded += (_, _) =>
        {
            if (viewModel.LastSnapshot is not null)
            {
                ViewModelOnSnapshotUpdated(this, viewModel.LastSnapshot);
            }
        };

        RouteCanvas.SizeChanged += (_, _) =>
        {
            if (_lastSnapshot is not null)
            {
                RenderRouteScene(_lastSnapshot);
            }
        };
    }

    private void ViewModelOnSnapshotUpdated(object? sender, SimulationSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        RenderRouteScene(snapshot);
        UpdateTelemetryPlot(snapshot.Telemetry);
    }

    private void UpdateTelemetryPlot(TelemetrySnapshot telemetry)
    {
        MetricsPlot.Plot.Clear();

        if (telemetry.RiskSeries.Count > 0)
        {
            var risk = MetricsPlot.Plot.Add.Scatter(telemetry.RiskX, telemetry.RiskY);
            risk.LegendText = "Riesgo";

            var occupancy = MetricsPlot.Plot.Add.Scatter(telemetry.OccupancyX, telemetry.OccupancyY);
            occupancy.LegendText = "Ocupación media";

            var weather = MetricsPlot.Plot.Add.Scatter(telemetry.WeatherX, telemetry.WeatherY);
            weather.LegendText = "Presión climática";

            var latestTime = new[]
            {
                telemetry.RiskX.LastOrDefault(),
                telemetry.OccupancyX.LastOrDefault(),
                telemetry.WeatherX.LastOrDefault()
            }.Max();

            var xMin = Math.Max(0, latestTime - TelemetryWindowSeconds);
            var xMax = Math.Max(TelemetryWindowSeconds, latestTime + 12);

            MetricsPlot.Plot.ShowLegend();
            MetricsPlot.Plot.Title("Telemetría operacional");
            MetricsPlot.Plot.XLabel("Tiempo (s)");
            MetricsPlot.Plot.YLabel("Valor normalizado");
            MetricsPlot.Plot.Axes.SetLimits(xMin, xMax, -2, 102);
        }

        MetricsPlot.Refresh();
    }

    private void RenderRouteScene(SimulationSnapshot snapshot)
    {
        RouteCanvas.Children.Clear();

        if (RouteCanvas.ActualWidth < 100 || RouteCanvas.ActualHeight < 100 || snapshot.Stations.Count == 0)
        {
            return;
        }

        var viewportWidth = RouteCanvasScrollViewer.ViewportWidth > 0 ? RouteCanvasScrollViewer.ViewportWidth : RouteCanvas.ActualWidth;
        var viewportHeight = RouteCanvasScrollViewer.ViewportHeight > 0 ? RouteCanvasScrollViewer.ViewportHeight : RouteCanvas.ActualHeight;

        RouteCanvas.Width = Math.Max(viewportWidth, 1520);
        RouteCanvas.Height = Math.Max(viewportHeight, 540);

        var width = RouteCanvas.Width;
        var height = RouteCanvas.Height;

        const double leftMargin = 100;
        const double rightMargin = 210;
        const double topMargin = 72;
        const double bottomMargin = 96;

        var minRoute = snapshot.Stations.Min(station => station.RoutePositionMeters);
        var maxRoute = snapshot.Stations.Max(station => station.RoutePositionMeters);
        var minAltitude = snapshot.Stations.Min(station => station.AltitudeMeters);
        var maxAltitude = snapshot.Stations.Max(station => station.AltitudeMeters);

        Point MapPoint(double routePosition, double altitude)
        {
            var xRatio = maxRoute - minRoute <= 0
                ? 0
                : (routePosition - minRoute) / (maxRoute - minRoute);

            var yRatio = maxAltitude - minAltitude <= 0
                ? 0
                : (altitude - minAltitude) / (maxAltitude - minAltitude);

            var x = leftMargin + (xRatio * (width - leftMargin - rightMargin));
            var y = height - bottomMargin - (yRatio * (height - topMargin - bottomMargin));
            return new Point(x, y);
        }

        DrawAltitudeGrid(MapPoint, minAltitude, maxAltitude, minRoute, width);
        DrawRoutePolyline(snapshot, MapPoint);
        DrawCabins(snapshot, MapPoint);
        DrawStations(snapshot, MapPoint);
        DrawBadges(snapshot, width, height);
    }

    private void DrawAltitudeGrid(
        Func<double, double, Point> mapPoint,
        double minAltitude,
        double maxAltitude,
        double minRoute,
        double width)
    {
        for (var index = 0; index <= 4; index++)
        {
            var altitude = minAltitude + ((maxAltitude - minAltitude) / 4.0 * index);
            var point = mapPoint(minRoute, altitude);

            var line = new Line
            {
                X1 = 74,
                X2 = width - 38,
                Y1 = point.Y,
                Y2 = point.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(42, 148, 163, 184)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 5, 5 }
            };

            RouteCanvas.Children.Add(line);

            var altitudeText = new TextBlock
            {
                Text = $"{altitude:F0} m",
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };

            Canvas.SetLeft(altitudeText, 16);
            Canvas.SetTop(altitudeText, point.Y - 10);
            RouteCanvas.Children.Add(altitudeText);
        }
    }

    private void DrawRoutePolyline(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        var shadowPolyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(82, 56, 189, 248)),
            StrokeThickness = 11,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var routePolyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            StrokeThickness = 4,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        foreach (var station in snapshot.Stations.OrderBy(item => item.RoutePositionMeters))
        {
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            shadowPolyline.Points.Add(point);
            routePolyline.Points.Add(point);
        }

        RouteCanvas.Children.Add(shadowPolyline);
        RouteCanvas.Children.Add(routePolyline);
    }

    private void DrawStations(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        var orderedStations = snapshot.Stations.OrderBy(station => station.RoutePositionMeters).ToList();

        for (var index = 0; index < orderedStations.Count; index++)
        {
            var station = orderedStations[index];
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            var isFirstStation = index == 0;
            var isLastStation = index == orderedStations.Count - 1;

            var halo = new Ellipse
            {
                Width = 28,
                Height = 28,
                Fill = new SolidColorBrush(Color.FromArgb(40, 59, 130, 246)),
                StrokeThickness = 0
            };
            Canvas.SetLeft(halo, point.X - 14);
            Canvas.SetTop(halo, point.Y - 14);
            RouteCanvas.Children.Add(halo);

            var marker = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                StrokeThickness = 3
            };

            Canvas.SetLeft(marker, point.X - 9);
            Canvas.SetTop(marker, point.Y - 9);
            RouteCanvas.Children.Add(marker);

            const double labelWidth = 162;
            var label = new Border
            {
                Width = labelWidth,
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 6, 10, 6),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = station.Name,
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"↑ {station.WaitingAscendingPassengers} | ↓ {station.WaitingDescendingPassengers} | {station.BoardingRulesDisplay}",
                            Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                            FontSize = 10.5,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };

            double desiredLeft;
            double desiredTop;

            if (isFirstStation)
            {
                desiredLeft = point.X - 34;
                desiredTop = point.Y - 100;
            }
            else if (isLastStation)
            {
                desiredLeft = point.X - labelWidth - 26;
                desiredTop = point.Y + 22;
            }
            else
            {
                desiredLeft = point.X - (labelWidth / 2);
                desiredTop = point.Y + (index % 2 == 0 ? -110 : 30);
            }

            desiredLeft = Math.Max(10, Math.Min(RouteCanvas.Width - labelWidth - 10, desiredLeft));
            desiredTop = Math.Max(52, Math.Min(RouteCanvas.Height - 88, desiredTop));
            Canvas.SetLeft(label, desiredLeft);
            Canvas.SetTop(label, desiredTop);
            RouteCanvas.Children.Add(label);
        }
    }

    private void DrawCabins(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        var segmentIndexes = snapshot.Cabins
            .GroupBy(cabin => cabin.SegmentName)
            .ToDictionary(group => group.Key, group => 0);

        foreach (var cabin in snapshot.Cabins.OrderBy(item => item.SegmentName).ThenBy(item => item.Id))
        {
            var point = mapPoint(cabin.GlobalRoutePositionMeters, cabin.AltitudeMeters);
            var segmentIndex = segmentIndexes[cabin.SegmentName]++;
            var directionOffset = cabin.Direction == TravelDirection.Ascending ? -18 : 18;
            var staggerOffset = ((segmentIndex % 3) - 1) * 10;
            var visualY = point.Y + directionOffset + staggerOffset;
            var cabinBrush = ResolveCabinBrush(cabin);

            var card = new Border
            {
                Width = 112,
                Height = 50,
                Background = cabinBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 15, 23, 42)),
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 6, 8, 6),
                Child = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(22) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    }
                }
            };

            var contentGrid = (Grid)card.Child;

            var arrow = new TextBlock
            {
                Text = cabin.Direction == TravelDirection.Ascending ? "▲" : "▼",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            contentGrid.Children.Add(arrow);

            var details = new StackPanel
            {
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(details, 1);

            details.Children.Add(new TextBlock
            {
                Text = cabin.Code,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12
            });

            details.Children.Add(new TextBlock
            {
                Text = cabin.CompactOccupancyLabel,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                FontSize = 11
            });

            contentGrid.Children.Add(details);

            var cardLeft = Math.Max(8, Math.Min(RouteCanvas.Width - card.Width - 8, point.X - (card.Width / 2)));
            var cardTop = Math.Max(82, Math.Min(RouteCanvas.Height - card.Height - 42, visualY - (card.Height / 2)));
            Canvas.SetLeft(card, cardLeft);
            Canvas.SetTop(card, cardTop);
            RouteCanvas.Children.Add(card);
        }
    }

    private void DrawBadges(SimulationSnapshot snapshot, double width, double height)
    {
        var weatherBadge = CreateBadge($"Clima: {snapshot.WeatherCondition.ToDisplayText()} | Riesgo {snapshot.CurrentRiskScore:F1} | Visibilidad {snapshot.VisibilityPercent:F0}%");
        Canvas.SetLeft(weatherBadge, Math.Max(18, width - 410));
        Canvas.SetTop(weatherBadge, 18);
        RouteCanvas.Children.Add(weatherBadge);

        var profileBadge = CreateBadge($"Perfil: {snapshot.DayProfileName} | Temporada: {snapshot.SeasonalityLabel}");
        Canvas.SetLeft(profileBadge, 18);
        Canvas.SetTop(profileBadge, 16);
        RouteCanvas.Children.Add(profileBadge);

        var footer = new TextBlock
        {
            Text = "Ruta 1D: Barinitas - La Montaña - La Aguada - Loma Redonda - Pico Espejo",
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };

        Canvas.SetLeft(footer, 20);
        Canvas.SetTop(footer, Math.Max(0, height - 28));
        RouteCanvas.Children.Add(footer);

        if (snapshot.OperationalState == SystemOperationalState.EmergencyStop)
        {
            RouteCanvas.Children.Add(CreateOverlay(width, "PROTOCOLO DE EMERGENCIA ACTIVO", Color.FromArgb(232, 127, 29, 29), Color.FromArgb(190, 254, 202, 202)));
        }
        else if (snapshot.OperationalState == SystemOperationalState.Completed)
        {
            RouteCanvas.Children.Add(CreateOverlay(width, "JORNADA COMPLETADA", Color.FromArgb(228, 21, 128, 61), Color.FromArgb(180, 187, 247, 208)));
        }
    }

    private static Border CreateBadge(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(210, 15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Border CreateOverlay(double width, string text, Color backgroundColor, Color borderColor)
    {
        var overlay = new Border
        {
            Width = Math.Max(280, width * 0.46),
            Background = new SolidColorBrush(backgroundColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };

        Canvas.SetLeft(overlay, Math.Max(16, (width - overlay.Width) / 2));
        Canvas.SetTop(overlay, 58);
        return overlay;
    }

    private static Brush ResolveCabinBrush(CabinSnapshot cabin)
    {
        if (cabin.IsOutOfService || cabin.AlertLevel == CabinAlertLevel.Critical || cabin.HasMechanicalFailure || cabin.HasElectricalFailure)
        {
            return new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        if (cabin.HasEmergencyBrake || cabin.AlertLevel == CabinAlertLevel.Alert || cabin.OperationalState == CabinOperationalState.EmergencyBraking || cabin.OperationalState == CabinOperationalState.Braking)
        {
            return new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }

        if (cabin.OperationalState == CabinOperationalState.IdleAtStation)
        {
            return new SolidColorBrush(Color.FromRgb(56, 189, 248));
        }

        return new SolidColorBrush(Color.FromRgb(34, 197, 94));
    }
}
