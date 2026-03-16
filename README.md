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

## Cómo abrir el proyecto

1. Descomprime el archivo ZIP.
2. Abre `HighRiskSimulator.sln` en Visual Studio 2022.
3. Espera a que Visual Studio restaure los paquetes NuGet.
4. Verifica que el proyecto cargue sin advertencias críticas.
5. Ejecuta con `F5`.

## Primer flujo recomendado de trabajo

1. Ejecutar el proyecto tal como está.
2. Revisar la estructura de carpetas.
3. Entender los modelos dentro de `Models`.
4. Revisar `SimulacionService` para comprender la lógica inicial.
5. Revisar `MainViewModel` para ver cómo se conecta la simulación con la UI.
6. Probar cambios pequeños en probabilidades, capacidad o eventos.
7. Luego extender el proyecto con métricas y gráficos.

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

## Próximos pasos sugeridos

- Agregar configuración manual de parámetros desde la interfaz.
- Mostrar histórico de simulaciones.
- Integrar gráficos con ScottPlot.
- Añadir más variables: clima, velocidad, mantenimiento, tensión del cable.
- Mejorar el cálculo del riesgo total.
- Separar mejor la persistencia y la lógica estadística.

## Paquetes usados

- `Microsoft.Data.Sqlite` para persistencia con SQLite.

## Recomendación para Git

Haz tu primer commit apenas verifiques que el proyecto abre y compila:

```bash
git init
git add .
git commit -m "Base inicial del proyecto HighRisk Simulator"
```

## Nota

Este starter está pensado como una **base académica limpia y extensible**. No intenta resolver todo el simulador desde el inicio; solo dejar una estructura correcta para empezar a construirlo bien.
