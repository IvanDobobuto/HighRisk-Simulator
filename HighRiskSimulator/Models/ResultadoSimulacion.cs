using System.Collections.Generic;

namespace HighRiskSimulator.Models;

public class ResultadoSimulacion
{
    public int Iteracion { get; set; }
    public double RiesgoTotal { get; set; }
    public int CantidadEventos { get; set; }
    public string Resumen { get; set; } = string.Empty;

    public List<EventoRiesgo> Eventos { get; set; } = new();
}
