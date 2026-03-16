using System.Collections.Generic;

namespace HighRiskSimulator.Models;

public class TelefericoSistema
{
    public string Nombre { get; set; } = "Sistema Principal";
    public double LongitudRecorrido { get; set; }
    public double VelocidadActual { get; set; }
    public bool EnergiaActiva { get; set; }

    public List<Cabina> Cabinas { get; set; } = new();
    public List<Estacion> Estaciones { get; set; } = new();
}
