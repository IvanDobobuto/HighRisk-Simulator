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
/// - dibujar la escena sandbox 2D escalable;
/// - actualizar ScottPlot con seguimiento automático de la ventana temporal;
/// - soporte de zoom interactivo con auto-follow configurable.
/// </summary>
public partial class MainWindow : Window
{
    private const double TelemetryWindowSeconds = 420;
    private const double TelemetryLeadMarginSeconds = 30;
    private const double SceneWidth = 1600;
    private const double SceneHeight = 900;
    private SimulationSnapshot? _lastSnapshot;
    private bool _userHasCustomView;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();
        viewModel.SnapshotUpdated += ViewModelOnSnapshotUpdated;
        DataContext = viewModel;

        // ScottPlot.WPF v5 does not expose an `Interaction` property on `WpfPlot`.
        // Interactive behavior is handled via the control's events (MouseWheel, MouseDown, etc.).
        // The explicit call to `MetricsPlot.Interaction.Enable()` was removed to fix CS1061.

        MetricsPlot.MouseWheel += (_, _) => _userHasCustomView = true;
        MetricsPlot.MouseDown += (_, args) =>
        {
            if (args.ChangedButton == System.Windows.Input.MouseButton.Right)
            {
                _userHasCustomView = false;
            }
        };

        Loaded += (_, _) =>
        {
            if (viewModel.LastSnapshot is not null)
            {
                ViewModelOnSnapshotUpdated(this, viewModel.LastSnapshot);
            }
        };
    }

    private void ViewModelOnSnapshotUpdated(object? sender, SimulationSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        RenderSandboxScene(snapshot);
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

            if (!_userHasCustomView)
            {
                var latestTime = new[]
                {
                    telemetry.RiskX.LastOrDefault(),
                    telemetry.OccupancyX.LastOrDefault(),
                    telemetry.WeatherX.LastOrDefault()
                }.Max();

                var xMax = latestTime + TelemetryLeadMarginSeconds;
                var xMin = xMax - TelemetryWindowSeconds;

                if (xMin < 0)
                {
                    xMin = 0;
                    xMax = TelemetryWindowSeconds;
                }

                MetricsPlot.Plot.Axes.SetLimits(xMin, xMax, -2, 102);
            }
        }

        MetricsPlot.Refresh();
    }

    private void RenderSandboxScene(SimulationSnapshot snapshot)
    {
        RouteCanvas.Children.Clear();
        RouteCanvas.Width = SceneWidth;
        RouteCanvas.Height = SceneHeight;

        if (snapshot.Stations.Count == 0)
        {
            return;
        }

        const double leftMargin = 120;
        const double rightMargin = 160;
        const double topMargin = 120;
        const double bottomMargin = 170;

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

            var x = leftMargin + (xRatio * (SceneWidth - leftMargin - rightMargin));
            var y = SceneHeight - bottomMargin - (yRatio * (SceneHeight - topMargin - bottomMargin));
            return new Point(x, y);
        }

        DrawSkyBackdrop(snapshot);
        DrawBackgroundMountains(snapshot, MapPoint);
        DrawAltitudeReference(MapPoint, minAltitude, maxAltitude, minRoute);
        DrawCableRoute(snapshot, MapPoint);
        DrawStations(snapshot, MapPoint);
        DrawCabins(snapshot, MapPoint);
        DrawWeatherOverlay(snapshot);
        DrawSceneHud(snapshot);
    }

    private void DrawSkyBackdrop(SimulationSnapshot snapshot)
    {
        var skyColor = snapshot.WeatherCondition switch
        {
            WeatherCondition.Storm => Color.FromRgb(30, 41, 59),
            WeatherCondition.Snow => Color.FromRgb(71, 85, 105),
            WeatherCondition.Fog => Color.FromRgb(100, 116, 139),
            WeatherCondition.Windy => Color.FromRgb(15, 23, 42),
            _ => Color.FromRgb(12, 74, 110)
        };

        var horizonColor = snapshot.WeatherCondition switch
        {
            WeatherCondition.Storm => Color.FromRgb(15, 23, 42),
            WeatherCondition.Snow => Color.FromRgb(51, 65, 85),
            WeatherCondition.Fog => Color.FromRgb(71, 85, 105),
            _ => Color.FromRgb(30, 64, 175)
        };

        var background = new Rectangle
        {
            Width = SceneWidth,
            Height = SceneHeight,
            Fill = new LinearGradientBrush(skyColor, horizonColor, new Point(0.5, 0), new Point(0.5, 1))
        };
        RouteCanvas.Children.Add(background);

        for (var index = 0; index < 28; index++)
        {
            var star = new Ellipse
            {
                Width = index % 3 == 0 ? 3 : 2,
                Height = index % 3 == 0 ? 3 : 2,
                Fill = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255))
            };

            Canvas.SetLeft(star, 60 + ((index * 53) % 1500));
            Canvas.SetTop(star, 40 + ((index * 37) % 180));
            RouteCanvas.Children.Add(star);
        }
    }

    private void DrawBackgroundMountains(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        var orderedStations = snapshot.Stations.OrderBy(station => station.RoutePositionMeters).ToList();
        var farMountain = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Opacity = 0.70,
            Points = new PointCollection
            {
                new(0, SceneHeight),
                new(0, 520),
                new(220, 380),
                new(410, 470),
                new(620, 310),
                new(820, 430),
                new(1070, 270),
                new(1320, 420),
                new(1600, 250),
                new(1600, SceneHeight)
            }
        };
        RouteCanvas.Children.Add(farMountain);

        var nearPoints = new PointCollection { new(0, SceneHeight), new(0, 720) };
        foreach (var station in orderedStations)
        {
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            nearPoints.Add(new Point(point.X, Math.Min(SceneHeight - 160, point.Y + 120 + ((station.Id % 2) * 18))));
        }

        nearPoints.Add(new Point(SceneWidth, 760));
        nearPoints.Add(new Point(SceneWidth, SceneHeight));

        var nearMountain = new Polygon
        {
            Fill = new LinearGradientBrush(Color.FromRgb(15, 23, 42), Color.FromRgb(22, 101, 52), new Point(0.5, 0), new Point(0.5, 1)),
            Opacity = 0.94,
            Points = nearPoints
        };
        RouteCanvas.Children.Add(nearMountain);

        for (var treeIndex = 0; treeIndex < 22; treeIndex++)
        {
            var x = 40 + (treeIndex * 70);
            var canopy = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(16, 85, 55)),
                Points = new PointCollection
                {
                    new(x, 760),
                    new(x + 18, 720),
                    new(x + 36, 760)
                }
            };
            RouteCanvas.Children.Add(canopy);

            var trunk = new Rectangle
            {
                Width = 7,
                Height = 24,
                Fill = new SolidColorBrush(Color.FromRgb(101, 67, 33))
            };
            Canvas.SetLeft(trunk, x + 14);
            Canvas.SetTop(trunk, 760);
            RouteCanvas.Children.Add(trunk);
        }
    }

    private void DrawAltitudeReference(Func<double, double, Point> mapPoint, double minAltitude, double maxAltitude, double minRoute)
    {
        for (var index = 0; index <= 4; index++)
        {
            var altitude = minAltitude + ((maxAltitude - minAltitude) / 4.0 * index);
            var point = mapPoint(minRoute, altitude);

            var line = new Line
            {
                X1 = 56,
                X2 = SceneWidth - 44,
                Y1 = point.Y,
                Y2 = point.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(36, 226, 232, 240)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 6, 6 }
            };
            RouteCanvas.Children.Add(line);

            var altitudeText = new TextBlock
            {
                Text = $"{altitude:F0} m",
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(altitudeText, 18);
            Canvas.SetTop(altitudeText, point.Y - 10);
            RouteCanvas.Children.Add(altitudeText);
        }
    }

    private void DrawCableRoute(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        var orderedStations = snapshot.Stations.OrderBy(station => station.RoutePositionMeters).ToList();

        var guideShadow = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(140, 15, 23, 42)),
            StrokeThickness = 10,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var cable = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            StrokeThickness = 3.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        foreach (var station in orderedStations)
        {
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            guideShadow.Points.Add(point);
            cable.Points.Add(point);
        }

        RouteCanvas.Children.Add(guideShadow);
        RouteCanvas.Children.Add(cable);

        foreach (var station in orderedStations)
        {
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            var mast = new Rectangle
            {
                Width = 8,
                Height = 54,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(Color.FromRgb(71, 85, 105))
            };
            Canvas.SetLeft(mast, point.X - 4);
            Canvas.SetTop(mast, point.Y - 24);
            RouteCanvas.Children.Add(mast);
        }
    }

    private void DrawStations(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        var orderedStations = snapshot.Stations.OrderBy(station => station.RoutePositionMeters).ToList();

        for (var index = 0; index < orderedStations.Count; index++)
        {
            var station = orderedStations[index];
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            var width = index == 0 || index == orderedStations.Count - 1 ? 150 : 128;
            var height = index == 0 || index == orderedStations.Count - 1 ? 84 : 74;

            var pad = new Rectangle
            {
                Width = width + 26,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            };
            Canvas.SetLeft(pad, point.X - ((width + 26) * 0.5));
            Canvas.SetTop(pad, point.Y + 34);
            RouteCanvas.Children.Add(pad);

            var body = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(index == 0 ? Color.FromRgb(191, 219, 254) : index == orderedStations.Count - 1 ? Color.FromRgb(196, 181, 253) : Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(8)
            };
            Canvas.SetLeft(body, point.X - (width * 0.5));
            Canvas.SetTop(body, point.Y - 6);
            RouteCanvas.Children.Add(body);

            var roof = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Points = new PointCollection
                {
                    new(point.X - (width * 0.55), point.Y + 2),
                    new(point.X, point.Y - 26),
                    new(point.X + (width * 0.55), point.Y + 2)
                }
            };
            RouteCanvas.Children.Add(roof);

            for (var windowIndex = 0; windowIndex < 4; windowIndex++)
            {
                var windowRect = new Rectangle
                {
                    Width = 22,
                    Height = 18,
                    RadiusX = 3,
                    RadiusY = 3,
                    Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(windowRect, point.X - (width * 0.5) + 16 + (windowIndex * 26));
                Canvas.SetTop(windowRect, point.Y + 16);
                RouteCanvas.Children.Add(windowRect);
            }

            var nameBlock = new TextBlock
            {
                Text = station.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Width = width + 36,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(nameBlock, point.X - ((width + 36) * 0.5));
            Canvas.SetTop(nameBlock, point.Y - 58);
            RouteCanvas.Children.Add(nameBlock);

            DrawQueueBadge(point.X - (width * 0.5) + 12, point.Y + 54, $"↑ {station.WaitingAscendingPassengers}", Color.FromRgb(22, 163, 74));
            DrawQueueBadge(point.X + (width * 0.5) - 64, point.Y + 54, $"↓ {station.WaitingDescendingPassengers}", Color.FromRgb(234, 88, 12));
        }
    }

    private void DrawCabins(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        foreach (var cabin in snapshot.Cabins.OrderBy(cabin => cabin.GlobalRoutePositionMeters))
        {
            var point = mapPoint(cabin.GlobalRoutePositionMeters, cabin.AltitudeMeters);
            var bodyBrush = ResolveCabinBrush(cabin);
            var y = point.Y - 20;
            var x = point.X - 18;

            var support = new Line
            {
                X1 = point.X,
                X2 = point.X,
                Y1 = point.Y - 28,
                Y2 = point.Y - 6,
                Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                StrokeThickness = 3
            };
            RouteCanvas.Children.Add(support);

            var cabinBody = new Rectangle
            {
                Width = 36,
                Height = 28,
                RadiusX = 6,
                RadiusY = 6,
                Fill = bodyBrush,
                Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                StrokeThickness = 3
            };
            Canvas.SetLeft(cabinBody, x);
            Canvas.SetTop(cabinBody, y);
            RouteCanvas.Children.Add(cabinBody);

            var roof = new Rectangle
            {
                Width = 28,
                Height = 8,
                RadiusX = 3,
                RadiusY = 3,
                Fill = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };
            Canvas.SetLeft(roof, point.X - 14);
            Canvas.SetTop(roof, y - 6);
            RouteCanvas.Children.Add(roof);

            for (var windowIndex = 0; windowIndex < 2; windowIndex++)
            {
                var windowRect = new Rectangle
                {
                    Width = 10,
                    Height = 8,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = new SolidColorBrush(Color.FromRgb(224, 242, 254))
                };
                Canvas.SetLeft(windowRect, x + 7 + (windowIndex * 12));
                Canvas.SetTop(windowRect, y + 8);
                RouteCanvas.Children.Add(windowRect);
            }

            var label = new TextBlock
            {
                Text = cabin.Code,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, point.X - 16);
            Canvas.SetTop(label, y + 31);
            RouteCanvas.Children.Add(label);

            var occupancy = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = $"{cabin.PassengerCount}/{cabin.Capacity}",
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                }
            };
            Canvas.SetLeft(occupancy, point.X - 24);
            Canvas.SetTop(occupancy, y + 48);
            RouteCanvas.Children.Add(occupancy);

            DrawStatusIcons(cabin, point.X + 22, y - 12);
        }
    }

    private void DrawStatusIcons(CabinSnapshot cabin, double startX, double y)
    {
        var icons = new List<(string Text, Color Color)>();

        if (cabin.HasMechanicalFailure)
        {
            icons.Add(("⚙", Color.FromRgb(234, 88, 12)));
        }
        else if (cabin.MechanicalHealthPercent < 70 || cabin.BrakeHealthPercent < 70)
        {
            icons.Add(("⌁", Color.FromRgb(245, 158, 11)));
        }

        if (cabin.HasElectricalFailure)
        {
            icons.Add(("⚡", Color.FromRgb(147, 51, 234)));
        }
        else if (cabin.ElectricalHealthPercent < 70)
        {
            icons.Add(("ϟ", Color.FromRgb(167, 139, 250)));
        }

        if (cabin.HasEmergencyBrake)
        {
            icons.Add(("■", Color.FromRgb(220, 38, 38)));
        }

        if (cabin.IsOutOfService)
        {
            icons.Add(("×", Color.FromRgb(153, 27, 27)));
        }

        for (var index = 0; index < icons.Count; index++)
        {
            var badge = new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(Color.FromArgb(228, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(icons[index].Color),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Child = new TextBlock
                {
                    Text = icons[index].Text,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(icons[index].Color),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            Canvas.SetLeft(badge, startX + (index * 22));
            Canvas.SetTop(badge, y);
            RouteCanvas.Children.Add(badge);
        }
    }

    private void DrawWeatherOverlay(SimulationSnapshot snapshot)
    {
        switch (snapshot.WeatherCondition)
        {
            case WeatherCondition.Windy:
                DrawWindOverlay();
                break;
            case WeatherCondition.Fog:
                DrawFogOverlay();
                break;
            case WeatherCondition.Snow:
                DrawSnowOverlay();
                break;
            case WeatherCondition.Storm:
                DrawStormOverlay();
                break;
        }
    }

    private void DrawWindOverlay()
    {
        for (var index = 0; index < 18; index++)
        {
            var baseY = 120 + (index * 34);
            var line = new Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(90, 186, 230, 253)),
                StrokeThickness = 3,
                Data = Geometry.Parse($"M {60 + (index * 36)} {baseY} C {120 + (index * 36)} {baseY - 16}, {160 + (index * 36)} {baseY + 16}, {220 + (index * 36)} {baseY}")
            };
            RouteCanvas.Children.Add(line);
        }
    }

    private void DrawFogOverlay()
    {
        for (var index = 0; index < 5; index++)
        {
            var fogBand = new Rectangle
            {
                Width = SceneWidth,
                Height = 90,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(54 + (index * 14)), 241, 245, 249))
            };
            Canvas.SetLeft(fogBand, 0);
            Canvas.SetTop(fogBand, 240 + (index * 70));
            RouteCanvas.Children.Add(fogBand);
        }
    }

    private void DrawSnowOverlay()
    {
        for (var index = 0; index < 90; index++)
        {
            var flake = new Ellipse
            {
                Width = index % 3 == 0 ? 4 : 3,
                Height = index % 3 == 0 ? 4 : 3,
                Fill = new SolidColorBrush(Color.FromArgb(190, 248, 250, 252))
            };
            Canvas.SetLeft(flake, 30 + ((index * 17) % 1520));
            Canvas.SetTop(flake, 70 + ((index * 29) % 720));
            RouteCanvas.Children.Add(flake);
        }
    }

    private void DrawStormOverlay()
    {
        for (var index = 0; index < 90; index++)
        {
            var drop = new Line
            {
                X1 = 20 + ((index * 17) % 1540),
                X2 = 28 + ((index * 17) % 1540),
                Y1 = 80 + ((index * 23) % 720),
                Y2 = 98 + ((index * 23) % 720),
                Stroke = new SolidColorBrush(Color.FromArgb(165, 125, 211, 252)),
                StrokeThickness = 2
            };
            RouteCanvas.Children.Add(drop);
        }

        var lightning = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(230, 250, 204, 21)),
            Points = new PointCollection
            {
                new(1210, 110),
                new(1170, 220),
                new(1210, 220),
                new(1178, 330),
                new(1280, 190),
                new(1230, 190)
            }
        };
        RouteCanvas.Children.Add(lightning);
    }

    private void DrawSceneHud(SimulationSnapshot snapshot)
    {
        // 1. PANEL DE RIESGO 
        var riskPanel = CreateHudCard(28, 26, 360, 160, "Resumen de riesgo");
        RouteCanvas.Children.Add(riskPanel);
        AddHudText(48, 75, $"Estado: {snapshot.OperationalStateDisplay}", 25, FontWeights.SemiBold);
        AddHudText(48, 110, $"Riesgo actual: {snapshot.CurrentRiskScore:F1}/100", 25, FontWeights.Normal);
        AddHudText(48, 140, $"Eventos activos: {snapshot.ActiveCriticalIssues}", 25, FontWeights.Normal);

        /*/ 2. PANEL DE CLIMA
        var weatherPanel = CreateHudCard(SceneWidth - 390, 26, 360, 180, "Ambiente y visualización");
        RouteCanvas.Children.Add(weatherPanel);
        AddHudText(SceneWidth - 370, 68, snapshot.WeatherSummary, 25, FontWeights.SemiBold);
        AddHudText(SceneWidth - 370, 100, $"Visibilidad: {snapshot.VisibilityPercent:F1}%", 25, FontWeights.Normal);
        AddHudText(SceneWidth - 370, 128, $"Engelamiento: {snapshot.IcingRiskPercent:F1}%", 25, FontWeights.Normal);
        */

        // 3. LEYENDA
        var legendPanel = CreateHudCard(SceneWidth - 450, SceneHeight - 250, 420, 220, "Diagnóstico rápido");
        RouteCanvas.Children.Add(legendPanel);
        AddLegendChip(SceneWidth - 435, SceneHeight - 195, "⚙ Falla mecánica", Color.FromRgb(234, 88, 12));
        AddLegendChip(SceneWidth - 435, SceneHeight - 140, "⚡ Falla eléctrica", Color.FromRgb(147, 51, 234));
        AddLegendChip(SceneWidth - 435, SceneHeight - 85, "■ Frenado / parada", Color.FromRgb(220, 38, 38));
    }

    private Border CreateHudCard(double x, double y, double width, double height, string title)
    {
        var header = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            FontSize = 27,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(14, 10, 14, 0)
        };

        var card = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(210, 15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(18),
            Child = new Grid()
        };

        var grid = (Grid)card.Child;
        grid.Children.Add(header);
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        return card;
    }

    private void AddHudText(double x, double y, string text, double fontSize, FontWeight weight)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Width = 280
        };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        RouteCanvas.Children.Add(block);
    }

    private void AddLegendChip(double x, double y, string text, Color accent)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(215, 30, 41, 59)),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 6, 12, 6),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = 25,
                FontWeight = FontWeights.SemiBold
            }
        };
        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);
        RouteCanvas.Children.Add(border);
    }

    private void DrawQueueBadge(double x, double y, string text, Color accent)
    {
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(225, 15, 23, 42)),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1.4),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 8, 3),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        };
        Canvas.SetLeft(badge, x);
        Canvas.SetTop(badge, y);
        RouteCanvas.Children.Add(badge);
    }

    private static Brush ResolveCabinBrush(CabinSnapshot cabin)
    {
        if (cabin.IsOutOfService)
        {
            return new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }

        if (cabin.HasMechanicalFailure || cabin.HasElectricalFailure)
        {
            return new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        if (cabin.HasEmergencyBrake || cabin.OperationalState is CabinOperationalState.Braking or CabinOperationalState.EmergencyBraking)
        {
            return new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }

        if (cabin.AlertLevel == CabinAlertLevel.Alert)
        {
            return new SolidColorBrush(Color.FromRgb(96, 165, 250));
        }

        return new SolidColorBrush(Color.FromRgb(34, 197, 94));
    }
}
