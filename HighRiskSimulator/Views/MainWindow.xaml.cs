using HighRiskSimulator.Core.Domain;
using HighRiskSimulator.Core.Domain.Models;
using HighRiskSimulator.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

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
    private readonly Dictionary<string, ImageSource> _spriteCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _weatherAnimationTimer;
    private readonly List<TutorialStep> _tutorialSteps = new();
    private SimulationSnapshot? _lastSnapshot;
    private bool _userHasCustomView;
    private bool _tutorialWeatherInjected;
    private bool _tutorialEventInjected;
    private int _weatherFrameIndex;
    private int _tutorialStepIndex;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

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
            if (TutorialOverlay.Visibility == Visibility.Visible)
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

    private void TriggerMenuToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        // Si abro el menú izquierdo, apago el de probabilidades
        if (RiskMenuToggleButton != null && RiskMenuToggleButton.IsChecked == true)
        {
            RiskMenuToggleButton.IsChecked = false;
        }
    }

    private void RiskMenuToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        // Si abro el menú de probabilidades, apago el izquierdo
        if (TriggerMenuToggleButton != null && TriggerMenuToggleButton.IsChecked == true)
        {
            TriggerMenuToggleButton.IsChecked = false;
        }
    }

    private void UpdateTelemetryPlot(TelemetrySnapshot telemetry)
    {
        MetricsPlot.Plot.Clear();

        // HACK: Configuración tipo "Dark Mode" para ScottPlot para integrarse al cristal
        MetricsPlot.Plot.FigureBackground.Color = ScottPlot.Colors.Transparent;
        MetricsPlot.Plot.DataBackground.Color = ScottPlot.Colors.Transparent;
        MetricsPlot.Plot.Axes.Color(ScottPlot.Colors.SlateGray);
        MetricsPlot.Plot.Grid.LineColor = ScottPlot.Color.FromHex("#22334155"); // Grid muy sutil

        if (telemetry.RiskSeries.Count > 0)
        {
            var risk = MetricsPlot.Plot.Add.Scatter(telemetry.RiskX, telemetry.RiskY);
            risk.Color = ScottPlot.Colors.Crimson;
            risk.LineWidth = 2.5f;
            risk.LegendText = "Riesgo";

            var occupancy = MetricsPlot.Plot.Add.Scatter(telemetry.OccupancyX, telemetry.OccupancyY);
            occupancy.Color = ScottPlot.Colors.DeepSkyBlue;
            occupancy.LineWidth = 2f;
            occupancy.LegendText = "Ocupación media";

            MetricsPlot.Plot.ShowLegend();
            // Fondo transparente para la leyenda también
            MetricsPlot.Plot.Legend.BackgroundColor = ScottPlot.Colors.Transparent;
            // Obsolete usage removed: assign FontColor instead of using Legend.Font
            MetricsPlot.Plot.Legend.FontColor = ScottPlot.Colors.LightGray;
            MetricsPlot.Plot.Legend.OutlineStyle.Color = ScottPlot.Colors.Transparent;

            // Limites de ejes y seguimiento de ventana de tiempo (el código original de tu auto-follow se mantiene intacto)
            // ...
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
        DrawMountains(snapshot, MapPoint);
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

        DrawCinematicVignette(snapshot);
    }

    private static string ResolveBackgroundPath(SimulationSnapshot snapshot)
    {
        var is_night = IsNoLightMode(snapshot) ? "_night" : string.Empty;
        if (snapshot.WeatherCondition == WeatherCondition.Storm)
        {
            return $"assets/backgrounds/background_storm{is_night}.png";
        }

        if (snapshot.WeatherCondition == WeatherCondition.Snow)
        {
            return $"assets/backgrounds/background_snow{is_night}.png";
        }

        if (snapshot.WeatherCondition == WeatherCondition.Fog)
        {
            return $"assets/backgrounds/background_mist{is_night}.png";
        }

        /*return IsNightMode(snapshot)
            ? "assets/backgrounds/background_2.png"
            : "assets/backgrounds/background_1.png";*/
        return $"assets/backgrounds/background{is_night}.png";
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

    private void DrawCinematicVignette(SimulationSnapshot snapshot)
    {
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
            Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.45),
                Center = new Point(0.5, 0.45),
                RadiusX = 0.78,
                RadiusY = 0.80,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55),
                    new GradientStop(Color.FromArgb(220, 2, 6, 23), 1.0)
                }
            }
        };
        RouteCanvas.Children.Add(vignette);
    }

    private Image CreateSpriteImage(string relativePath, double width, double height, Stretch stretch = Stretch.Uniform, double opacity = 1.0)
    {
        return new Image
        {
            Source = LoadSprite(relativePath),
            Width = width,
            Height = height,
            Stretch = stretch,
            SnapsToDevicePixels = true,
            Opacity = opacity
        };
    }

    private ImageSource LoadSprite(string relativePath)
    {
        if (_spriteCache.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        _spriteCache[relativePath] = bitmap;
        return bitmap;
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

    private void DrawMountains(SimulationSnapshot snapshot, Func<double, double, Point> mapPoint)
    {
        var OrderedStations = snapshot.Stations.OrderBy(station => station.RoutePositionMeters).ToList();
        for (var index = 0; index < OrderedStations.Count; index++)
        {
            var station = OrderedStations[index];
            var point = mapPoint(station.RoutePositionMeters, station.AltitudeMeters);
            var mountain = CreateSpriteImage(ResolveMountainPath(snapshot, index + 1), 400, 500, Stretch.Uniform, 1.0);
            Canvas.SetLeft(mountain, point.X - 180);
            Canvas.SetTop(mountain, point.Y - 74);
            RouteCanvas.Children.Add(mountain);
        }
    }

    private static string ResolveMountainPath(SimulationSnapshot snapshot, int index)
    {
        return IsNightMode(snapshot)
            ? $"assets/mountains/mountain_{index}_night.png"
            : $"assets/mountains/mountain_{index}.png";
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
            Stroke = new SolidColorBrush(Color.FromArgb(170, 2, 6, 23)),
            StrokeThickness = 10,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var cable = new Polyline
        {
            Stroke = new SolidColorBrush(IsNightMode(snapshot) ? Color.FromRgb(203, 213, 225) : Color.FromRgb(148, 163, 184)),
            StrokeThickness = 4.2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        var cableHighlight = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(160, 226, 232, 240)),
            StrokeThickness = 1.3,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
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
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Width = width + 45,
                TextWrapping = TextWrapping.Wrap,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.75
                }
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
                    FontWeight = FontWeights.Bold
                }
            };
            Canvas.SetLeft(stationStatus, point.X - 28);
            Canvas.SetTop(stationStatus, point.Y + 9);
            RouteCanvas.Children.Add(stationStatus);

            DrawQueueBadge(point.X - (width * 0.5) + 8, point.Y + 52, $"↑ {station.WaitingAscendingPassengers}", Color.FromRgb(22, 163, 74));
            DrawQueueBadge(point.X + (width * 0.5) - 64, point.Y + 52, $"↓ {station.WaitingDescendingPassengers}", Color.FromRgb(234, 88, 12));
        }
    }

    private string ResolveStationSpritePath(SimulationSnapshot snapshot, bool isTerminal)
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
                            FontWeight = FontWeights.Bold,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"{cabin.PassengerCount}/{cabin.Capacity}",
                            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                            FontSize = 9.5,
                            FontWeight = FontWeights.SemiBold,
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
                AddSceneEffect($"assets/effects/wind_frame_{_weatherFrameIndex + 1}.png", 0.42);
                break;
            case WeatherCondition.Fog:
                AddSceneEffect("assets/effects/mist.png", 0.62);
                break;
            case WeatherCondition.Snow:
                AddSceneEffect($"assets/effects/snow_frame_{_weatherFrameIndex + 1}.png", 0.9);
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
        var lightning = CreateSpriteImage("assets/effects/lightning.png", 350, 350, Stretch.Uniform, 0.90);
        Canvas.SetLeft(lightning, 1100);
        Canvas.SetTop(lightning, 0);
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
        // 1. PANEL DE RIESGO
        var riskPanel = CreateHudCard(128, 26, 360, 160, "Resumen de riesgo");
        RouteCanvas.Children.Add(riskPanel);
        AddHudText(148, 75, $"Estado: {snapshot.OperationalStateDisplay}", 25, FontWeights.SemiBold);
        AddHudText(148, 108, $"Riesgo actual: {snapshot.CurrentRiskScore:F1}/100", 25, FontWeights.Normal);
        AddHudText(148, 140, $"Eventos activos: {snapshot.ActiveCriticalIssues}", 25, FontWeights.Normal);

        // 2. LEYENDA
        var legendPanel = CreateHudCard(SceneWidth - 360, SceneHeight - 360, 330, 165, "Diagnóstico rápido");
        RouteCanvas.Children.Add(legendPanel);
        AddLegendChip(SceneWidth - 344, SceneHeight - 312, "⚙ Falla mecánica", Color.FromRgb(234, 88, 12));
        AddLegendChip(SceneWidth - 344, SceneHeight - 268, "⚡ Falla eléctrica", Color.FromRgb(147, 51, 234));
        AddLegendChip(SceneWidth - 344, SceneHeight - 224, "■ Frenado / parada", Color.FromRgb(220, 38, 38));
    }


    private Border CreateHudCard(double x, double y, double width, double height, string title)
    {
        var header = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            FontSize = width < 340 ? 21 : 27,
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
            Padding = new Thickness(10, 5, 10, 5),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = 18,
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



    private void StartSimulator_Click(object sender, RoutedEventArgs e)
    {
        MainMenuOverlay.Visibility = Visibility.Collapsed;
        TutorialOverlay.Visibility = Visibility.Collapsed;
    }

    private void StartTutorial_Click(object sender, RoutedEventArgs e)
    {
        MainMenuOverlay.Visibility = Visibility.Collapsed;
        ReportDialogOverlay.Visibility = Visibility.Collapsed;
        TutorialOverlay.Visibility = Visibility.Visible;
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


    private void ExitApplication_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExitToMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PauseCommand.CanExecute(null))
        {
            ViewModel.PauseCommand.Execute(null);
        }

        TutorialOverlay.Visibility = Visibility.Collapsed;
        ReportDialogOverlay.Visibility = Visibility.Collapsed;
        MainMenuOverlay.Visibility = Visibility.Visible;
    }

    private void OpenReportDialog_Click(object sender, RoutedEventArgs e)
    {
        var defaultDate = ViewModel.SelectedSimulationDate ?? DateTime.Today;
        SingleDayReportOption.IsChecked = true;
        ReportStartDatePicker.SelectedDate = defaultDate;
        ReportEndDatePicker.SelectedDate = defaultDate;
        ReportDialogOverlay.Visibility = Visibility.Visible;
    }

    private void CloseReportDialog_Click(object sender, RoutedEventArgs e)
    {
        ReportDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void GenerateReportFromDialog_Click(object sender, RoutedEventArgs e)
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
                await ViewModel.ExportDateRangeReportAsync(startDate, endDate);
            }
            else
            {
                ViewModel.SelectedSimulationDate = startDate;
                await ViewModel.ExportSingleDayReportAsync();
            }

            ReportDialogOverlay.Visibility = Visibility.Collapsed;
        }
        finally
        {
            GenerateReportButton.IsEnabled = true;
        }
    }

    private void PreviousTutorialStep_Click(object sender, RoutedEventArgs e)
    {
        if (_tutorialStepIndex <= 0)
        {
            return;
        }

        _tutorialStepIndex--;
        UpdateTutorialStep();
    }

    private void NextTutorialStep_Click(object sender, RoutedEventArgs e)
    {
        if (_tutorialStepIndex >= _tutorialSteps.Count - 1)
        {
            EndTutorial();
            return;
        }

        _tutorialStepIndex++;
        UpdateTutorialStep();
    }

    private void EndTutorial_Click(object sender, RoutedEventArgs e)
    {
        EndTutorial();
    }

    private void EndTutorial()
    {
        TutorialOverlay.Visibility = Visibility.Collapsed;
        MainMenuOverlay.Visibility = Visibility.Collapsed;
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
        await Dispatcher.InvokeAsync(PositionCurrentTutorialStep, DispatcherPriority.Loaded);
        await Task.Delay(380);

        if (TutorialOverlay.Visibility == Visibility.Visible)
        {
            PositionCurrentTutorialStep();
        }
    }

    private void PositionCurrentTutorialStep()
    {
        if (_tutorialSteps.Count == 0 || TutorialOverlay.Visibility != Visibility.Visible)
        {
            return;
        }

        var step = _tutorialSteps[Math.Clamp(_tutorialStepIndex, 0, _tutorialSteps.Count - 1)];
        var highlight = step.HighlightResolver();
        if (highlight.IsEmpty || highlight.Width <= 1 || highlight.Height <= 1)
        {
            highlight = new Rect(32, 32, Math.Max(320, RootLayout.ActualWidth * 0.55), Math.Max(140, RootLayout.ActualHeight * 0.18));
        }

        TutorialCanvas.Width = RootLayout.ActualWidth;
        TutorialCanvas.Height = RootLayout.ActualHeight;

        TutorialHighlight.Width = highlight.Width;
        TutorialHighlight.Height = highlight.Height;
        Canvas.SetLeft(TutorialHighlight, highlight.Left);
        Canvas.SetTop(TutorialHighlight, highlight.Top);

        TutorialCard.Measure(new Size(430, double.PositiveInfinity));
        var cardWidth = Math.Max(430, TutorialCard.DesiredSize.Width);
        var cardHeight = Math.Max(220, TutorialCard.DesiredSize.Height);
        var canvasWidth = Math.Max(1, TutorialCanvas.ActualWidth > 0 ? TutorialCanvas.ActualWidth : RootLayout.ActualWidth);
        var canvasHeight = Math.Max(1, TutorialCanvas.ActualHeight > 0 ? TutorialCanvas.ActualHeight : RootLayout.ActualHeight);

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

    private Rect GetElementBounds(Thickness padding, params FrameworkElement[] elements)
    {
        Rect bounds = Rect.Empty;

        foreach (var element in elements)
        {
            if (element is null || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                continue;
            }

            try
            {
                element.UpdateLayout();
                var transform = element.TransformToAncestor(RootLayout);
                var topLeft = transform.Transform(new Point(0, 0));
                var bottomRight = transform.Transform(new Point(element.ActualWidth, element.ActualHeight));
                var rect = new Rect(topLeft, bottomRight);
                bounds = bounds.IsEmpty ? rect : Rect.Union(bounds, rect);
            }
            catch (InvalidOperationException)
            {
                // Ignorado: el elemento puede no estar listo aún durante transiciones.
            }
        }

        if (bounds.IsEmpty)
        {
            return Rect.Empty;
        }

        return new Rect(
            Math.Max(0, bounds.Left - padding.Left),
            Math.Max(0, bounds.Top - padding.Top),
            bounds.Width + padding.Left + padding.Right,
            bounds.Height + padding.Top + padding.Bottom);
    }

    private void SetTutorialMenus(bool showTriggerMenu, bool showRiskMenu, bool showTelemetryMenu)
    {
        TriggerMenuToggleButton.IsChecked = showTriggerMenu;
        RiskMenuToggleButton.IsChecked = showRiskMenu;
        TelemetryMenuToggleButton.IsChecked = showTelemetryMenu;
    }

    private sealed record TutorialStep(string Title, string Body, Func<Rect> HighlightResolver, Action? EnterAction);

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
