using System.Windows;
using HighRiskSimulator.ViewModels;

namespace HighRiskSimulator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
