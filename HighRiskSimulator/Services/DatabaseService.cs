using Microsoft.Data.Sqlite;
using HighRiskSimulator.Models;

namespace HighRiskSimulator.Services;

public class DatabaseService
{
    private const string ConnectionString = "Data Source=highrisk.db";

    public void InicializarBaseDeDatos()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Simulaciones (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Fecha TEXT NOT NULL,
                RiesgoTotal REAL NOT NULL,
                CantidadEventos INTEGER NOT NULL,
                Resumen TEXT NOT NULL
            );";

        command.ExecuteNonQuery();
    }

    public void GuardarResultado(ResultadoSimulacion resultado)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Simulaciones (Fecha, RiesgoTotal, CantidadEventos, Resumen)
            VALUES ($fecha, $riesgoTotal, $cantidadEventos, $resumen);";

        command.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("s"));
        command.Parameters.AddWithValue("$riesgoTotal", resultado.RiesgoTotal);
        command.Parameters.AddWithValue("$cantidadEventos", resultado.CantidadEventos);
        command.Parameters.AddWithValue("$resumen", resultado.Resumen);

        command.ExecuteNonQuery();
    }
}
