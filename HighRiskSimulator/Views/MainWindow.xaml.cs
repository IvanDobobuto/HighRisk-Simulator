using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.ViewModels;

namespace HighRiskSimulator.Views;

/// <summary>
/// Ventana principal Avalonia. Mantiene la lógica visual separada del motor de simulación.
/// </summary>
public partial class MainWindow : Window
{
    private const double TelemetryLeadMarginSeconds = 30;
    private const double SceneWidth = 1600;
    private const double SceneHeight = 900;
    private readonly Dictionary<string, IImage> _spriteCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _weatherAnimationTimer;
    private readonly List<TutorialStep> _tutorialSteps = new();
    private SimulationSnapshot? _lastSnapshot;
    private double _telemetryWindowSeconds = 420;
    private bool _tutorialWeatherInjected;
    private bool _tutorialEventInjected;
    private int _weatherFrameIndex;
    private int _tutorialStepIndex;

    private MainViewModel ViewModel => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();
        viewModel.SnapshotUpdated += ViewModelOnSnapshotUpdated;
        DataContext = viewModel;

        MetricsPlot.PointerWheelChanged += (_, _) => UpdateTelemetryPlot(_lastSnapshot?.Telemetry);
        MetricsPlot.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(MetricsPlot).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
            {
                _telemetryWindowSeconds = 420;
                UpdateTelemetryPlot(_lastSnapshot?.Telemetry);
            }
        };

        _weatherAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _weatherAnimationTimer.Tick += (_, _) =>
        {
            _weatherFrameIndex = (_weatherFrameIndex + 1) % 3;
            if (_lastSnapshot is not null)
            {
                RenderSandboxScene(_lastSnapshot);
            }
        };
        _weatherAnimationTimer.Start();

        Loaded += (_, _) =>
        {
            ConfigureTutorialSteps();
            if (viewModel.LastSnapshot is not null)
            {
                ViewModelOnSnapshotUpdated(this, viewModel.LastSnapshot);
            }
        };

        SizeChanged += (_, _) =>
        {
            if (TutorialOverlay.IsVisible)
            {
                RefreshTutorialOverlayLayout();
            }
        };
    }

    private void ViewModelOnSnapshotUpdated(object? sender, SimulationSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        RenderSandboxScene(snapshot);
        UpdateTelemetryPlot(snapshot.Telemetry);
    }

    private void TriggerMenuToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TriggerMenuToggleButton.IsChecked == true)
        {
            RiskMenuToggleButton.IsChecked = false;
        }
    }

    private void RiskMenuToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        if (RiskMenuToggleButton.IsChecked == true)
        {
            TriggerMenuToggleButton.IsChecked = false;
        }
    }

    private void TelemetryZoomIn_Click(object? sender, RoutedEventArgs e)
    {
        _telemetryWindowSeconds = Math.Max(60, _telemetryWindowSeconds * 0.65);
        UpdateTelemetryPlot(_lastSnapshot?.Telemetry);
    }

    private void TelemetryZoomOut_Click(object? sender, RoutedEventArgs e)
    {
        _telemetryWindowSeconds = Math.Min(3600, _telemetryWindowSeconds * 1.55);
        UpdateTelemetryPlot(_lastSnapshot?.Telemetry);
    }

    private void UpdateTelemetryPlot(TelemetrySnapshot? telemetry)
    {
        MetricsPlot.Plot.Clear();
        MetricsPlot.Plot.FigureBackground.Color = ScottPlot.Colors.Transparent;
        MetricsPlot.Plot.DataBackground.Color = ScottPlot.Colors.Transparent;
        MetricsPlot.Plot.Axes.Color(ScottPlot.Colors.SlateGray);
        MetricsPlot.Plot.Grid.LineColor = ScottPlot.Color.FromHex("#22334155");
        MetricsPlot.Plot.Axes.Bottom.Label.Text = "Tiempo simulado (s)";
        MetricsPlot.Plot.Axes.Left.Label.Text = "Riesgo / ocupacion (%)";

        if (telemetry is not null && telemetry.RiskSeries.Count > 0)
        {
            var risk = MetricsPlot.Plot.Add.Scatter(telemetry.RiskX, telemetry.RiskY);
            risk.Color = ScottPlot.Colors.Crimson;
            risk.LineWidth = 2.5f;
            risk.LegendText = "Riesgo";

            var occupancy = MetricsPlot.Plot.Add.Scatter(telemetry.OccupancyX, telemetry.OccupancyY);
            occupancy.Color = ScottPlot.Colors.DeepSkyBlue;
            occupancy.LineWidth = 2f;
            occupancy.LegendText = "Ocupacion media";

            var latestX = telemetry.RiskX.Length > 0 ? telemetry.RiskX[^1] : 0;
            var minX = Math.Max(0, latestX - _telemetryWindowSeconds);
            var maxX = Math.Max(_telemetryWindowSeconds, latestX + TelemetryLeadMarginSeconds);
            var (minY, maxY) = ResolveTelemetryYRange(telemetry, minX, maxX);
            MetricsPlot.Plot.Axes.SetLimitsX(minX, maxX);
            MetricsPlot.Plot.Axes.SetLimitsY(minY, maxY);
            MetricsPlot.Plot.ShowLegend();
            MetricsPlot.Plot.Legend.BackgroundColor = ScottPlot.Colors.Transparent;
            MetricsPlot.Plot.Legend.FontColor = ScottPlot.Colors.LightGray;
            MetricsPlot.Plot.Legend.OutlineStyle.Color = ScottPlot.Colors.Transparent;
        }
        else
        {
            MetricsPlot.Plot.Axes.SetLimitsX(0, _telemetryWindowSeconds);
            MetricsPlot.Plot.Axes.SetLimitsY(0, 105);
        }

        MetricsPlot.Refresh();
    }

    private static (double MinY, double MaxY) ResolveTelemetryYRange(TelemetrySnapshot telemetry, double minX, double maxX)
    {
        var values = telemetry.RiskX
            .Zip(telemetry.RiskY, (x, y) => (X: x, Y: y))
            .Concat(telemetry.OccupancyX.Zip(telemetry.OccupancyY, (x, y) => (X: x, Y: y)))
            .Where(point => point.X >= minX && point.X <= maxX && !double.IsNaN(point.Y) && !double.IsInfinity(point.Y))
            .Select(point => point.Y)
            .ToList();

        if (values.Count == 0)
        {
            return (0, 105);
        }

        var minValue = Math.Min(0, values.Min());
        var maxValue = Math.Max(100, values.Max());
        var padding = Math.Max(5, (maxValue - minValue) * 0.10);
        return (Math.Floor(minValue - padding), Math.Ceiling(maxValue + padding));
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

        const double leftMargin = 115;
        const double rightMargin = 150;
        const double topMargin = 130;
        const double bottomMargin = 210;

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

        DrawEnvironmentBackdrop(snapshot);
        DrawAltitudeReference(MapPoint, minAltitude, maxAltitude, minRoute);
        DrawCableRoute(snapshot, MapPoint);
        DrawStations(snapshot, MapPoint);
        DrawCabins(snapshot, MapPoint);
        DrawWeatherOverlay(snapshot);
        DrawSceneHud(snapshot);
    }

    private void DrawEnvironmentBackdrop(SimulationSnapshot snapshot)
    {
        var background = CreateSpriteImage(ResolveBackgroundPath(snapshot), SceneWidth, SceneHeight, Stretch.Fill, 0.98);
        Canvas.SetLeft(background, 0);
        Canvas.SetTop(background, 0);
        RouteCanvas.Children.Add(background);

        var opacity = IsNightMode(snapshot) ? 0.28 : 0.15;
        if (IsNoLightMode(snapshot))
        {
            opacity += 0.14;
        }

        var vignette = new Rectangle
        {
            Width = SceneWidth,
            Height = SceneHeight,
            Opacity = opacity,
            Fill = new SolidColorBrush(Color.FromArgb(185, 2, 6, 23))
        };
        RouteCanvas.Children.Add(vignette);
    }

    private static string ResolveBackgroundPath(SimulationSnapshot snapshot)
    {
        if (snapshot.WeatherCondition == WeatherCondition.Storm || snapshot.WeatherCondition == WeatherCondition.Snow)
        {
            return "assets/backgrounds/background_3.png";
        }

        if (snapshot.WeatherCondition == WeatherCondition.Fog)
        {
            return "assets/backgrounds/background_4.png";
        }

        return IsNightMode(snapshot)
            ? "assets/backgrounds/background_2.png"
            : "assets/backgrounds/background_1.png";
    }

    private static bool IsNightMode(SimulationSnapshot snapshot)
    {
        return snapshot.Elapsed.TotalHours >= 9.5;
    }

    private static bool IsNoLightMode(SimulationSnapshot snapshot)
    {
        return snapshot.Cabins.Any(cabin => cabin.HasElectricalFailure)
            || snapshot.OperationalState == SystemOperationalState.EmergencyStop;
    }

    private Image CreateSpriteImage(string relativePath, double width, double height, Stretch stretch = Stretch.Uniform, double opacity = 1.0)
    {
        return new Image
        {
            Source = LoadSprite(relativePath),
            Width = width,
            Height = height,
            Stretch = stretch,
            Opacity = opacity
        };
    }

    private IImage LoadSprite(string relativePath)
    {
        if (_spriteCache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        var uri = new Uri($"avares://HighRiskSimulator/{relativePath}", UriKind.Absolute);
        using var stream = AssetLoader.Open(uri);
        var bitmap = new Bitmap(stream);
        _spriteCache[relativePath] = bitmap;
        return bitmap;
    }

    private void DrawAltitudeReference(Func<double, double, Point> mapPoint, double minAltitude, double maxAltitude, double minRoute)
    {
        for (var index = 0; index <= 4; index++)
        {
            var altitude = minAltitude + ((maxAltitude - minAltitude) / 4.0 * index);
            var point = mapPoint(minRoute, altitude);

            var line = new Line
            {
                StartPoint = new Point(56, point.Y),
                EndPoint = new Point(SceneWidth - 44, point.Y),
                Stroke = new SolidColorBrush(Color.FromArgb(36, 226, 232, 240)),
                StrokeThickness = 1
            };
            RouteCanvas.Children.Add(line);

            var altitudeText = new TextBlock
            {
                Text = $"{altitude:F0} m",
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold
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
            Stroke = new SolidColorBrush(Color.FromArgb(170, 2, 6, 23)),
            StrokeThickness = 10,
            Points = new Points()
        };

        var cable = new Polyline
        {
            Stroke = new SolidColorBrush(IsNightMode(snapshot) ? Color.FromRgb(203, 213, 225) : Color.FromRgb(148, 163, 184)),
            StrokeThickness = 4.2,
            Points = new Points()
        };

        var cableHighlight = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(160, 226, 232, 240)),
            StrokeThickness = 1.3,
            Points = new Points()
        };

        foreach (var station in orderedStations)
        {
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            guideShadow.Points.Add(point);
            cable.Points.Add(point);
            cableHighlight.Points.Add(new Point(point.X, point.Y - 4));
        }

        RouteCanvas.Children.Add(guideShadow);
        RouteCanvas.Children.Add(cable);
        RouteCanvas.Children.Add(cableHighlight);

        foreach (var station in orderedStations)
        {
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            var mast = new Rectangle
            {
                Width = 8,
                Height = 58,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(Color.FromArgb(190, 51, 65, 85))
            };
            Canvas.SetLeft(mast, point.X - 4);
            Canvas.SetTop(mast, point.Y - 18);
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
            var isTerminal = index == 0 || index == orderedStations.Count - 1;
            var width = isTerminal ? 154 : 118;
            var height = isTerminal ? 70 : 82;
            var stationTop = point.Y - (isTerminal ? 22 : 34);

            var platform = new Rectangle
            {
                Width = width + 42,
                Height = 18,
                Fill = new SolidColorBrush(Color.FromArgb(185, 30, 41, 59))
            };
            Canvas.SetLeft(platform, point.X - ((width + 42) * 0.5));
            Canvas.SetTop(platform, point.Y + 32);
            RouteCanvas.Children.Add(platform);

            var sprite = CreateSpriteImage(ResolveStationSpritePath(snapshot, isTerminal), width, height, Stretch.Uniform, 1.0);
            Canvas.SetLeft(sprite, point.X - (width * 0.5));
            Canvas.SetTop(sprite, stationTop);
            RouteCanvas.Children.Add(sprite);

            var nameBlock = new TextBlock
            {
                Text = station.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = isTerminal ? 12.5 : 11.5,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
                Width = width + 45,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(nameBlock, point.X - ((width + 45) * 0.5));
            Canvas.SetTop(nameBlock, stationTop - 28);
            RouteCanvas.Children.Add(nameBlock);

            var stationStatus = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 2, 7, 2),
                Child = new TextBlock
                {
                    Text = station.Code,
                    Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                    FontSize = 9,
                    FontWeight = FontWeight.Bold
                }
            };
            Canvas.SetLeft(stationStatus, point.X - 28);
            Canvas.SetTop(stationStatus, point.Y + 9);
            RouteCanvas.Children.Add(stationStatus);

            DrawQueueBadge(point.X - (width * 0.5) + 8, point.Y + 52, $"↑ {station.WaitingAscendingPassengers}", Color.FromRgb(22, 163, 74));
            DrawQueueBadge(point.X + (width * 0.5) - 64, point.Y + 52, $"↓ {station.WaitingDescendingPassengers}", Color.FromRgb(234, 88, 12));
        }
    }

    private static string ResolveStationSpritePath(SimulationSnapshot snapshot, bool isTerminal)
    {
        var baseFolder = isTerminal ? "main_stations" : "stations";
        var baseName = isTerminal ? "main_station" : "station";

        if (IsNightMode(snapshot))
        {
            return IsNoLightMode(snapshot)
                ? $"assets/{baseFolder}/{baseName}_night_no_light.png"
                : $"assets/{baseFolder}/{baseName}_night.png";
        }

        return IsNoLightMode(snapshot)
            ? $"assets/{baseFolder}/{baseName}_no_light.png"
            : $"assets/{baseFolder}/{baseName}.png";
    }

    private void DrawCabins(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        const double cabinWidth = 66.0;
        const double cabinHeight = 66.0;
        const double cableAnchorYOffset = 6.0;

        foreach (var cabin in snapshot.Cabins.OrderBy(cabin => cabin.GlobalRoutePositionMeters))
        {
            var point = mapPoint(cabin.GlobalRoutePositionMeters, cabin.AltitudeMeters);
            var x = point.X - (cabinWidth * 0.5);
            var y = point.Y - cableAnchorYOffset;

            var sprite = CreateSpriteImage(ResolveCabinSpritePath(cabin, snapshot), cabinWidth, cabinHeight, Stretch.Uniform, cabin.IsOutOfService ? 0.78 : 1.0);
            Canvas.SetLeft(sprite, x);
            Canvas.SetTop(sprite, y);
            RouteCanvas.Children.Add(sprite);

            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(215, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(170, 148, 163, 184)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = cabin.Code,
                            Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                            FontSize = 9.5,
                            FontWeight = FontWeight.Bold,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"{cabin.PassengerCount}/{cabin.Capacity}",
                            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                            FontSize = 9.5,
                            FontWeight = FontWeight.SemiBold,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                }
            };
            Canvas.SetLeft(label, point.X - 26);
            Canvas.SetTop(label, y + cabinHeight - 10);
            RouteCanvas.Children.Add(label);

            DrawStatusIcons(cabin, point.X + 34, y + 10);
        }
    }

    private static string ResolveCabinSpritePath(CabinSnapshot cabin, SimulationSnapshot snapshot)
    {
        var noLightSuffix = IsNoLightMode(snapshot) ? "_no_light" : string.Empty;

        if (cabin.IsOutOfService)
        {
            return $"assets/cabins/cabin_inactive{noLightSuffix}.png";
        }

        if (cabin.HasEmergencyBrake || cabin.OperationalState is CabinOperationalState.Braking or CabinOperationalState.EmergencyBraking)
        {
            return $"assets/cabins/cabin_emergency{noLightSuffix}.png";
        }

        if (cabin.HasMechanicalFailure || cabin.HasElectricalFailure || cabin.AlertLevel == CabinAlertLevel.Critical)
        {
            return $"assets/cabins/cabin_critical{noLightSuffix}.png";
        }

        return $"assets/cabins/cabin_operative{noLightSuffix}.png";
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
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(icons[index].Color),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
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
                AddSceneEffect($"assets/effects/wind_frame_{_weatherFrameIndex + 1}.png", 0.42);
                break;
            case WeatherCondition.Fog:
                AddSceneEffect("assets/effects/mist.png", 0.62);
                break;
            case WeatherCondition.Snow:
                AddSceneEffect($"assets/effects/rain_frame_{_weatherFrameIndex + 1}.png", 0.18);
                DrawSnowOverlay();
                break;
            case WeatherCondition.Storm:
                AddSceneEffect($"assets/effects/rain_frame_{_weatherFrameIndex + 1}.png", 0.48);
                AddSceneEffect($"assets/effects/wind_frame_{_weatherFrameIndex + 1}.png", 0.24);
                DrawLightningFlash();
                break;
        }
    }

    private void AddSceneEffect(string relativePath, double opacity)
    {
        var effect = CreateSpriteImage(relativePath, SceneWidth, SceneHeight, Stretch.Fill, opacity);
        Canvas.SetLeft(effect, 0);
        Canvas.SetTop(effect, 0);
        RouteCanvas.Children.Add(effect);
    }

    private void DrawLightningFlash()
    {
        if (_weatherFrameIndex != 1)
        {
            return;
        }

        var lightning = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(225, 250, 204, 21)),
            Points = new Points
            {
                new(1210, 110), new(1170, 220), new(1210, 220), new(1178, 330), new(1280, 190), new(1230, 190)
            }
        };
        RouteCanvas.Children.Add(lightning);
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

    private void DrawSceneHud(SimulationSnapshot snapshot)
    {
        var riskPanel = CreateHudCard(230, 76, 450, 180, "Resumen de riesgo");
        RouteCanvas.Children.Add(riskPanel);
        AddHudText(258, 130, $"Estado: {snapshot.OperationalStateDisplay}", 22, FontWeight.SemiBold, 380);
        AddHudText(258, 170, $"Riesgo actual: {snapshot.CurrentRiskScore:F1}/100", 22, FontWeight.Normal, 380);
        AddHudText(258, 208, $"Eventos activos: {snapshot.ActiveCriticalIssues}", 22, FontWeight.Normal, 380);

        const double diagnosisPanelWidth = 430;
        const double diagnosisPanelHeight = 220;
        var diagnosisPanelX = SceneWidth - diagnosisPanelWidth - 26;
        var diagnosisPanelY = SceneHeight - diagnosisPanelHeight - 195;
        var legendPanel = CreateHudCard(diagnosisPanelX, diagnosisPanelY, diagnosisPanelWidth, diagnosisPanelHeight, "Diagnóstico rápido");
        RouteCanvas.Children.Add(legendPanel);
        AddLegendChip(diagnosisPanelX + 24, diagnosisPanelY + 64, "⚙ Falla mecánica", Color.FromRgb(234, 88, 12));
        AddLegendChip(diagnosisPanelX + 24, diagnosisPanelY + 116, "⚡ Falla eléctrica", Color.FromRgb(147, 51, 234));
        AddLegendChip(diagnosisPanelX + 24, diagnosisPanelY + 168, "■ Frenado / parada", Color.FromRgb(220, 38, 38));
    }

    private Border CreateHudCard(double x, double y, double width, double height, string title)
    {
        var header = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            FontSize = width < 420 ? 23 : 27,
            FontWeight = FontWeight.Bold,
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

        ((Grid)card.Child).Children.Add(header);
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        return card;
    }

    private void AddHudText(double x, double y, string text, double fontSize, FontWeight weight, double width)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Width = width
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
            Width = 270,
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.NoWrap
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
                FontWeight = FontWeight.SemiBold
            }
        };
        Canvas.SetLeft(badge, x);
        Canvas.SetTop(badge, y);
        RouteCanvas.Children.Add(badge);
    }

    private void StartSimulator_Click(object? sender, RoutedEventArgs e)
    {
        MainMenuOverlay.IsVisible = false;
        TutorialOverlay.IsVisible = false;
    }

    private void StartTutorial_Click(object? sender, RoutedEventArgs e)
    {
        MainMenuOverlay.IsVisible = false;
        ReportDialogOverlay.IsVisible = false;
        TutorialOverlay.IsVisible = true;
        _tutorialStepIndex = 0;
        _tutorialWeatherInjected = false;
        _tutorialEventInjected = false;
        PrepareTutorialSimulation();
        ConfigureTutorialSteps();
        UpdateTutorialStep();
    }

    private void PrepareTutorialSimulation()
    {
        ViewModel.RandomSeed = "9051976";
        ViewModel.SelectedSimulationDate = DateTime.Today;
        ViewModel.ManualTimeScale = 10.0;
        ViewModel.ServiceDurationHours = 10.0;
        ViewModel.PassengerArrivalMultiplier = 1.15;

        if (ViewModel.ResetCommand.CanExecute(null))
        {
            ViewModel.ResetCommand.Execute(null);
        }

        if (ViewModel.StartCommand.CanExecute(null))
        {
            ViewModel.StartCommand.Execute(null);
        }
    }

    private void InjectTutorialEvent()
    {
        if (_tutorialEventInjected || !ViewModel.InjectOverloadCommand.CanExecute(null))
        {
            return;
        }

        ViewModel.InjectOverloadCommand.Execute(null);
        _tutorialEventInjected = true;
    }

    private void InjectTutorialWeather()
    {
        if (_tutorialWeatherInjected || !ViewModel.InjectFogCommand.CanExecute(null))
        {
            return;
        }

        ViewModel.InjectFogCommand.Execute(null);
        _tutorialWeatherInjected = true;
    }

    private void ExitApplication_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExitToMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.PauseCommand.CanExecute(null))
        {
            ViewModel.PauseCommand.Execute(null);
        }

        TutorialOverlay.IsVisible = false;
        ReportDialogOverlay.IsVisible = false;
        MainMenuOverlay.IsVisible = true;
    }

    private void OpenReportDialog_Click(object? sender, RoutedEventArgs e)
    {
        var defaultDate = ViewModel.SelectedSimulationDate ?? DateTime.Today;
        SingleDayReportOption.IsChecked = true;
        ReportStartDatePicker.SelectedDate = defaultDate;
        ReportEndDatePicker.SelectedDate = defaultDate;
        ReportDialogOverlay.IsVisible = true;
    }

    private void CloseReportDialog_Click(object? sender, RoutedEventArgs e)
    {
        ReportDialogOverlay.IsVisible = false;
    }

    private async void GenerateReportFromDialog_Click(object? sender, RoutedEventArgs e)
    {
        GenerateReportButton.IsEnabled = false;
        try
        {
            var startDate = ReportStartDatePicker.SelectedDate ?? DateTime.Today;
            var endDate = DateRangeReportOption.IsChecked == true
                ? ReportEndDatePicker.SelectedDate ?? startDate
                : startDate;

            if (DateRangeReportOption.IsChecked == true)
            {
                await ViewModel.ExportDateRangeReportAsync(startDate.Date, endDate.Date);
            }
            else
            {
                ViewModel.SelectedSimulationDate = startDate.Date;
                await ViewModel.ExportSingleDayReportAsync();
            }

            ReportDialogOverlay.IsVisible = false;
        }
        finally
        {
            GenerateReportButton.IsEnabled = true;
        }
    }

    private void PreviousTutorialStep_Click(object? sender, RoutedEventArgs e)
    {
        if (_tutorialStepIndex <= 0)
        {
            return;
        }

        _tutorialStepIndex--;
        UpdateTutorialStep();
    }

    private void NextTutorialStep_Click(object? sender, RoutedEventArgs e)
    {
        if (_tutorialStepIndex >= _tutorialSteps.Count - 1)
        {
            EndTutorial();
            return;
        }

        _tutorialStepIndex++;
        UpdateTutorialStep();
    }

    private void EndTutorial_Click(object? sender, RoutedEventArgs e)
    {
        EndTutorial();
    }

    private void EndTutorial()
    {
        TutorialOverlay.IsVisible = false;
        MainMenuOverlay.IsVisible = false;
    }

    private void ConfigureTutorialSteps()
    {
        if (_tutorialSteps.Count > 0)
        {
            return;
        }

        _tutorialSteps.Add(new TutorialStep(
            "Controles principales",
            "Aquí se inicia, pausa, reinicia, acelera y exporta la jornada. El botón Salir vuelve al menú principal sin cerrar el programa.",
            () => GetElementBounds(new Thickness(14), TopControlBar),
            () => SetTutorialMenus(false, false, false)));

        _tutorialSteps.Add(new TutorialStep(
            "Configuración de sesión",
            "Este panel define modo, semilla, fecha, aceleración, duración de la jornada y volumen de pasajeros antes de correr el motor.",
            () => GetElementBounds(new Thickness(16), SessionConfigurationSection),
            () => SetTutorialMenus(true, false, false)));

        _tutorialSteps.Add(new TutorialStep(
            "Inyección de eventos",
            "Los botones fuerzan clima, fallas, sobrecarga o emergencia controlada. En este tour se activa una sobrecarga global controlada para mostrar el registro de eventos.",
            () => GetElementBounds(new Thickness(16), InjectionSection),
            () =>
            {
                SetTutorialMenus(true, false, false);
                InjectTutorialEvent();
            }));

        _tutorialSteps.Add(new TutorialStep(
            "Riesgos calibrables",
            "El motor de riesgo permite aumentar o reducir probabilidades ambientales y técnicas para generar escenarios de entrenamiento.",
            () => GetElementBounds(new Thickness(16), ProbabilityPanel),
            () => SetTutorialMenus(false, true, false)));

        _tutorialSteps.Add(new TutorialStep(
            "Telemetría en vivo",
            "La gráfica resume riesgo y ocupación. Las tarjetas de estación muestran pasajeros esperando para identificar cuellos de botella.",
            () => GetElementBounds(new Thickness(18), RightPanel),
            () => SetTutorialMenus(false, false, true)));

        _tutorialSteps.Add(new TutorialStep(
            "Terminal operativa",
            "La zona inferior lista eventos, cabinas y estaciones. Es el tablero de lectura rápida para justificar resultados en el reporte.",
            () => GetElementBounds(new Thickness(10), BottomTerminalPanel),
            () => SetTutorialMenus(false, false, false)));

        _tutorialSteps.Add(new TutorialStep(
            "Visual Sandbox",
            "La escena muestra estaciones, cabinas, clima y modo día/noche. En este punto se activa neblina para enseñar cómo los frames climáticos se repiten en bucle.",
            () => GetElementBounds(new Thickness(18), SceneViewbox),
            () =>
            {
                SetTutorialMenus(false, false, false);
                InjectTutorialWeather();
            }));
    }

    private void UpdateTutorialStep()
    {
        if (_tutorialSteps.Count == 0)
        {
            return;
        }

        var step = _tutorialSteps[Math.Clamp(_tutorialStepIndex, 0, _tutorialSteps.Count - 1)];
        step.EnterAction?.Invoke();

        TutorialTitleText.Text = step.Title;
        TutorialBodyText.Text = step.Body;
        TutorialStepText.Text = $"Paso {_tutorialStepIndex + 1} de {_tutorialSteps.Count}";
        PreviousTutorialButton.IsEnabled = _tutorialStepIndex > 0;
        NextTutorialButton.Content = _tutorialStepIndex >= _tutorialSteps.Count - 1 ? "Finalizar" : "Siguiente";

        RefreshTutorialOverlayLayout();
    }

    private async void RefreshTutorialOverlayLayout()
    {
        await Dispatcher.UIThread.InvokeAsync(PositionCurrentTutorialStep, DispatcherPriority.Loaded);
        await Task.Delay(360);

        if (TutorialOverlay.IsVisible)
        {
            PositionCurrentTutorialStep();
        }
    }

    private void PositionCurrentTutorialStep()
    {
        if (_tutorialSteps.Count == 0 || !TutorialOverlay.IsVisible)
        {
            return;
        }

        var step = _tutorialSteps[Math.Clamp(_tutorialStepIndex, 0, _tutorialSteps.Count - 1)];
        var highlight = step.HighlightResolver();
        if (!IsUsableRect(highlight))
        {
            highlight = new Rect(32, 32, Math.Max(320, RootLayout.Bounds.Width * 0.55), Math.Max(140, RootLayout.Bounds.Height * 0.18));
        }

        TutorialCanvas.Width = RootLayout.Bounds.Width;
        TutorialCanvas.Height = RootLayout.Bounds.Height;

        TutorialHighlight.Width = highlight.Width;
        TutorialHighlight.Height = highlight.Height;
        Canvas.SetLeft(TutorialHighlight, highlight.Left);
        Canvas.SetTop(TutorialHighlight, highlight.Top);

        TutorialCard.Measure(new Size(430, double.PositiveInfinity));
        var cardWidth = Math.Max(430, TutorialCard.DesiredSize.Width);
        var cardHeight = Math.Max(220, TutorialCard.DesiredSize.Height);
        var canvasWidth = Math.Max(1, TutorialCanvas.Bounds.Width > 0 ? TutorialCanvas.Bounds.Width : RootLayout.Bounds.Width);
        var canvasHeight = Math.Max(1, TutorialCanvas.Bounds.Height > 0 ? TutorialCanvas.Bounds.Height : RootLayout.Bounds.Height);

        var cardLeft = highlight.Right + 24;
        if (cardLeft + cardWidth > canvasWidth - 24)
        {
            cardLeft = highlight.Left - cardWidth - 24;
        }
        if (cardLeft < 24)
        {
            cardLeft = Math.Max(24, canvasWidth - cardWidth - 24);
        }

        var cardTop = highlight.Top;
        if (cardTop + cardHeight > canvasHeight - 24)
        {
            cardTop = Math.Max(24, highlight.Bottom - cardHeight);
        }
        cardTop = Math.Min(cardTop, Math.Max(24, canvasHeight - cardHeight - 24));

        Canvas.SetLeft(TutorialCard, cardLeft);
        Canvas.SetTop(TutorialCard, cardTop);
    }

    private Rect GetElementBounds(Thickness padding, params Control[] elements)
    {
        Rect? bounds = null;

        foreach (var element in elements)
        {
            if (element is null || element.Bounds.Width <= 0 || element.Bounds.Height <= 0)
            {
                continue;
            }

            var topLeft = element.TranslatePoint(new Point(0, 0), RootLayout);
            if (topLeft is null)
            {
                continue;
            }

            var rect = new Rect(topLeft.Value, element.Bounds.Size);
            bounds = bounds.HasValue ? UnionRects(bounds.Value, rect) : rect;
        }

        if (!bounds.HasValue)
        {
            return default;
        }

        return new Rect(
            Math.Max(0, bounds.Value.Left - padding.Left),
            Math.Max(0, bounds.Value.Top - padding.Top),
            bounds.Value.Width + padding.Left + padding.Right,
            bounds.Value.Height + padding.Top + padding.Bottom);
    }

    private static bool IsUsableRect(Rect rect)
    {
        return rect.Width > 1
            && rect.Height > 1
            && !double.IsNaN(rect.X)
            && !double.IsNaN(rect.Y)
            && !double.IsNaN(rect.Width)
            && !double.IsNaN(rect.Height);
    }

    private static Rect UnionRects(Rect first, Rect second)
    {
        var left = Math.Min(first.Left, second.Left);
        var top = Math.Min(first.Top, second.Top);
        var right = Math.Max(first.Right, second.Right);
        var bottom = Math.Max(first.Bottom, second.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    private void SetTutorialMenus(bool showTriggerMenu, bool showRiskMenu, bool showTelemetryMenu)
    {
        TriggerMenuToggleButton.IsChecked = showTriggerMenu;
        RiskMenuToggleButton.IsChecked = showRiskMenu;
        TelemetryMenuToggleButton.IsChecked = showTelemetryMenu;
    }

    private sealed record TutorialStep(string Title, string Body, Func<Rect> HighlightResolver, Action? EnterAction);
}
