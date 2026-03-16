namespace HighRiskSimulator.Models;

public class EventoRiesgo
{
    public TipoEventoRiesgo Tipo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public double ProbabilidadBase { get; set; }
    public double Impacto { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
}
