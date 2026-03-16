using System.Collections.ObjectModel;
using System.Windows.Input;
using HighRiskSimulator.Helpers;
using HighRiskSimulator.Models;
using HighRiskSimulator.Services;

namespace HighRiskSimulator.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly SimulacionService _simulacionService;
    private readonly DatabaseService _databaseService;

    private string _estadoGeneral = "Sistema listo.";
    private string _riesgoTotalTexto = "Riesgo total: 0";
    private string _cantidadEventosTexto = "Eventos detectados: 0";

    public string EstadoGeneral
    {
        get => _estadoGeneral;
        set
        {
            _estadoGeneral = value;
            OnPropertyChanged();
        }
    }

    public string RiesgoTotalTexto
    {
        get => _riesgoTotalTexto;
        set
        {
            _riesgoTotalTexto = value;
            OnPropertyChanged();
        }
    }

    public string CantidadEventosTexto
    {
        get => _cantidadEventosTexto;
        set
        {
            _cantidadEventosTexto = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<EventoRiesgo> Eventos { get; } = new();

    public ICommand EjecutarSimulacionCommand { get; }

    public MainViewModel()
    {
        _simulacionService = new SimulacionService();
        _databaseService = new DatabaseService();
        _databaseService.InicializarBaseDeDatos();

        EjecutarSimulacionCommand = new RelayCommand(EjecutarSimulacion);
    }

    private void EjecutarSimulacion()
    {
        TelefericoSistema sistema = CrearSistemaInicial();
        ResultadoSimulacion resultado = _simulacionService.EjecutarSimulacion(sistema);

        _databaseService.GuardarResultado(resultado);

        EstadoGeneral = resultado.Resumen;
        RiesgoTotalTexto = $"Riesgo total: {resultado.RiesgoTotal:F2}";
        CantidadEventosTexto = $"Eventos detectados: {resultado.CantidadEventos}";

        Eventos.Clear();
        foreach (var evento in resultado.Eventos)
        {
            Eventos.Add(evento);
        }
    }

    private TelefericoSistema CrearSistemaInicial()
    {
        TelefericoSistema sistema = new()
        {
            Nombre = "Teleférico Universitario",
            LongitudRecorrido = 1200,
            VelocidadActual = 4.5,
            EnergiaActiva = true
        };

        sistema.Estaciones.Add(new Estacion { Id = 1, Nombre = "Estación A", Posicion = 0 });
        sistema.Estaciones.Add(new Estacion { Id = 2, Nombre = "Estación B", Posicion = 1200 });

        sistema.Cabinas.Add(new Cabina
        {
            Id = 1,
            CapacidadMaxima = 8,
            PasajerosActuales = 7,
            Posicion = 150,
            Estado = EstadoCabina.Operativa
        });

        sistema.Cabinas.Add(new Cabina
        {
            Id = 2,
            CapacidadMaxima = 8,
            PasajerosActuales = 10,
            Posicion = 600,
            Estado = EstadoCabina.Operativa
        });

        return sistema;
    }
}
