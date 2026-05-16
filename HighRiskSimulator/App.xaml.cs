using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HighRiskSimulator.Views;
using QuestPDF.Infrastructure;

namespace HighRiskSimulator;

/// <summary>
/// Punto de entrada Avalonia multiplataforma.
/// </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = true;
        QuestPDF.Settings.EnableDebugging = false;
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
