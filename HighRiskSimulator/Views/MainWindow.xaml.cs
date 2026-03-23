using System;
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
/// El code-behind se usa solo para tareas de presentación dura:
/// - dibujar el perfil del sistema en Canvas;
/// - refrescar ScottPlot.
/// 
/// La lógica del simulador sigue residiendo fuera de la vista.
/// </summary>
public partial class MainWindow : Window
{
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

            MetricsPlot.Plot.ShowLegend();
            MetricsPlot.Plot.Title("Telemetría operacional");
            MetricsPlot.Plot.XLabel("Tiempo (s)");
            MetricsPlot.Plot.YLabel("Valor normalizado");
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

        var width = RouteCanvas.ActualWidth;
        var height = RouteCanvas.ActualHeight;

        const double leftMargin = 88;
        const double rightMargin = 96;
        const double topMargin = 48;
        const double bottomMargin = 86;

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

        DrawAltitudeGrid(MapPoint, minAltitude, maxAltitude, minRoute, maxRoute, width);
        DrawRoutePolyline(snapshot, MapPoint);
        DrawStations(snapshot, MapPoint);
        DrawCabins(snapshot, MapPoint);
        DrawBadges(snapshot, width);
    }

    private void DrawAltitudeGrid(
        Func<double, double, Point> mapPoint,
        double minAltitude,
        double maxAltitude,
        double minRoute,
        double maxRoute,
        double width)
    {
        for (var index = 0; index <= 4; index++)
        {
            var altitude = minAltitude + ((maxAltitude - minAltitude) / 4.0 * index);
            var point = mapPoint(minRoute, altitude);

            var line = new Line
            {
                X1 = 72,
                X2 = width - 36,
                Y1 = point.Y,
                Y2 = point.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(48, 148, 163, 184)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 5 }
            };

            RouteCanvas.Children.Add(line);

            var altitudeText = new TextBlock
            {
                Text = $"{altitude:F0} m",
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                FontSize = 11
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
            Stroke = new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)),
            StrokeThickness = 10,
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

            var label = new Border
            {
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
                            FontSize = 12
                        },
                        new TextBlock
                        {
                            Text = $"{station.AltitudeMeters:F0} m | cola {station.TotalWaitingPassengers}",
                            Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                            FontSize = 11
                        }
                    }
                }
            };

            var verticalOffset = index % 2 == 0 ? -72 : 18;
            Canvas.SetLeft(label, point.X - 48);
            Canvas.SetTop(label, point.Y + verticalOffset);
            RouteCanvas.Children.Add(label);
        }
    }

    private void DrawCabins(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        foreach (var cabin in snapshot.Cabins.OrderBy(item => item.Id))
        {
            var point = mapPoint(cabin.GlobalRoutePositionMeters, cabin.AltitudeMeters);
            var cabinBrush = ResolveCabinBrush(cabin);

            var card = new Border
            {
                Width = 102,
                Height = 46,
                Background = cabinBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 15, 23, 42)),
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 5, 8, 5),
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
                Text = $"{cabin.PassengerCount}/{cabin.Capacity} pax",
                Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                FontSize = 11
            });

            contentGrid.Children.Add(details);

            Canvas.SetLeft(card, point.X - (card.Width / 2));
            Canvas.SetTop(card, point.Y - 24);
            RouteCanvas.Children.Add(card);
        }
    }

    private void DrawBadges(SimulationSnapshot snapshot, double width)
    {
        var weatherBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(210, 15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new TextBlock
            {
                Text = $"Clima: {snapshot.WeatherCondition} | Riesgo {snapshot.CurrentRiskScore:F1}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            }
        };

        Canvas.SetLeft(weatherBadge, Math.Max(16, width - 290));
        Canvas.SetTop(weatherBadge, 16);
        RouteCanvas.Children.Add(weatherBadge);

        var footer = new TextBlock
        {
            Text = "Perfil 1D: Barinitas -> La Montaña -> La Aguada -> Loma Redonda -> Pico Espejo",
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            FontSize = 12
        };

        Canvas.SetLeft(footer, 20);
        Canvas.SetTop(footer, Math.Max(0, RouteCanvas.ActualHeight - 26));
        RouteCanvas.Children.Add(footer);

        if (snapshot.OperationalState == SystemOperationalState.EmergencyStop)
        {
            var overlay = new Border
            {
                Width = Math.Max(260, width * 0.45),
                Background = new SolidColorBrush(Color.FromArgb(230, 127, 29, 29)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 254, 202, 202)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Child = new TextBlock
                {
                    Text = "PROTOCOLO DE EMERGENCIA ACTIVO",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };

            Canvas.SetLeft(overlay, Math.Max(16, (width - overlay.Width) / 2));
            Canvas.SetTop(overlay, 18);
            RouteCanvas.Children.Add(overlay);
        }
    }

    private static Brush ResolveCabinBrush(CabinSnapshot cabin)
    {
        if (cabin.IsOutOfService || cabin.HasMechanicalFailure || cabin.HasElectricalFailure)
        {
            return new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        if (cabin.HasEmergencyBrake || cabin.OperationalState == CabinOperationalState.EmergencyBraking || cabin.OperationalState == CabinOperationalState.Braking)
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
