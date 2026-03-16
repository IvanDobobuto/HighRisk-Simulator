using HighRiskSimulator.Models;

namespace HighRiskSimulator.Services;

public class SimulacionService
{
    private readonly Random _random = new();

    public ResultadoSimulacion EjecutarSimulacion(TelefericoSistema sistema)
    {
        ResultadoSimulacion resultado = new()
        {
            Iteracion = 1
        };

        double riesgoTotal = 0;
        int cantidadEventos = 0;

        foreach (var cabina in sistema.Cabinas)
        {
            if (cabina.EstaSobrecargada())
            {
                resultado.Eventos.Add(new EventoRiesgo
                {
                    Tipo = TipoEventoRiesgo.Sobrecarga,
                    Descripcion = $"La cabina {cabina.Id} supera la capacidad máxima.",
                    ProbabilidadBase = 0.80,
                    Impacto = 0.70
                });

                riesgoTotal += 0.70;
                cantidadEventos++;
            }

            int eventoAleatorio = _random.Next(0, 100);

            if (eventoAleatorio < 15)
            {
                resultado.Eventos.Add(new EventoRiesgo
                {
                    Tipo = TipoEventoRiesgo.FallaMecanica,
                    Descripcion = $"Se detectó una falla mecánica en la cabina {cabina.Id}.",
                    ProbabilidadBase = 0.15,
                    Impacto = 0.90
                });

                riesgoTotal += 0.90;
                cantidadEventos++;
            }
            else if (eventoAleatorio < 25)
            {
                resultado.Eventos.Add(new EventoRiesgo
                {
                    Tipo = TipoEventoRiesgo.FallaElectrica,
                    Descripcion = $"Se detectó una falla eléctrica en la cabina {cabina.Id}.",
                    ProbabilidadBase = 0.10,
                    Impacto = 0.80
                });

                riesgoTotal += 0.80;
                cantidadEventos++;
            }
        }

        resultado.CantidadEventos = cantidadEventos;
        resultado.RiesgoTotal = riesgoTotal;
        resultado.Resumen = cantidadEventos == 0
            ? "Simulación completada sin incidentes."
            : $"Simulación completada con {cantidadEventos} evento(s) de riesgo.";

        return resultado;
    }
}
