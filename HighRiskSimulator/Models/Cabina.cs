namespace HighRiskSimulator.Models;

public class Cabina
{
    public int Id { get; set; }
    public int CapacidadMaxima { get; set; }
    public int PasajerosActuales { get; set; }
    public double Posicion { get; set; }
    public EstadoCabina Estado { get; set; }

    public bool EstaSobrecargada()
    {
        return PasajerosActuales > CapacidadMaxima;
    }
}
