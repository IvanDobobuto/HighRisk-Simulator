using System.Windows;
using QuestPDF.Infrastructure;

namespace HighRiskSimulator;

/// <summary>
/// Punto de entrada WPF.
/// </summary>
public partial class App : Application
{
    public App()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = true;
        QuestPDF.Settings.EnableDebugging = false;
    }
}
