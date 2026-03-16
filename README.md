# HighRisk Simulator - Starter

Proyecto base de **C# + WPF** para iniciar el simulador de análisis de riesgo de teleféricos.

## Qué incluye este paquete

- Solución de Visual Studio 2022 (`HighRiskSimulator.sln`)
- Proyecto WPF con **.NET 8**
- Estructura principal de carpetas
- Clases base del dominio del problema
- `MainViewModel` con una simulación inicial funcional
- Servicio de base de datos con **SQLite**
- `README.md`
- `.gitignore` para no subir archivos innecesarios a GitHub

## Estructura del proyecto

```text
HighRiskSimulatorStarter/
├── HighRiskSimulator.sln
├── .gitignore
├── README.md
└── HighRiskSimulator/
    ├── App.xaml
    ├── App.xaml.cs
    ├── HighRiskSimulator.csproj
    ├── Models/
    ├── ViewModels/
    ├── Views/
    ├── Services/
    └── Helpers/
```

## Requisitos

- **Visual Studio 2022**
- Workload **.NET desktop development**
- SDK de **.NET 8** instalado

## Clases principales incluidas

### Models
- `Cabina`
- `Estacion`
- `TelefericoSistema`
- `EventoRiesgo`
- `ResultadoSimulacion`
- `EstadoCabina`
- `TipoEventoRiesgo`

### ViewModels
- `BaseViewModel`
- `MainViewModel`

### Services
- `SimulacionService`
- `DatabaseService`

### Helpers
- `RelayCommand`

## Qué hace la versión inicial

- Crea un sistema de teleférico simple en 1D.
- Genera una simulación básica con eventos de sobrecarga y fallas aleatorias.
- Muestra el resumen en la interfaz.
- Guarda cada resultado en una base SQLite (`highrisk.db`).

## Paquetes usados

- `Microsoft.Data.Sqlite` para persistencia con SQLite.

## Nota

Este starter está pensado como una **base académica limpia y extensible**. No intenta resolver todo el simulador desde el inicio; solo dejar una estructura correcta para empezar a construirlo bien.
