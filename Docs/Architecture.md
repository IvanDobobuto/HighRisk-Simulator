# Project Architecture

## Propósito

HighRisk Simulator es un simulador estadístico-operativo para el teleférico Mukumbarí. La arquitectura se divide para proteger el motor de simulación frente a cambios de interfaz: la lógica del dominio vive en `HighRiskSimulator.Core`, mientras que Avalonia UI solo presenta snapshots, controles y reportes.

## Capas principales

### `HighRiskSimulator.Core`

Contiene el dominio y el motor. No depende de Avalonia UI.

- `Domain/`: entidades del sistema, enums, eventos y snapshots.
- `Simulation/`: `SimulationEngine`, opciones, perfiles de riesgo, reportes y modelos de jornada.
- `DataStructures/`: estructuras manuales exigidas por la asignatura y justificadas por el dominio.
- `Factories/`: construcción del escenario Mukumbarí.
- `Persistence/`: contratos preparados para almacenamiento futuro sin acoplar el motor a una base de datos concreta.

### `HighRiskSimulator`

Contiene la aplicación Avalonia UI.

- `Views/`: ventana principal, visual sandbox, menú, tutorial, paneles y diálogos.
- `ViewModels/`: estado observable, comandos, coordinación de simulación y reportes.
- `Services/`: creación de sesiones y exportación PDF/JSON.
- `assets/`: sprites pixel art organizados por cabinas, estaciones, fondos, escenas prearmadas, efectos y título.

### `HighRiskSimulator.Tests`

Contiene pruebas unitarias para estructuras y reglas críticas del motor.

## Flujo de ejecución

1. La interfaz crea un `SimulationSessionRequest` con modo, fecha, semilla, duración, demanda y perfil de riesgo.
2. `SimulationSessionService` traduce esa solicitud a `SimulationOptions` y construye un `SimulationEngine`.
3. El motor avanza por ticks y emite `SimulationSnapshot`.
4. `MainViewModel` sincroniza colecciones observables para eventos, cabinas y estaciones.
5. `MainWindow` dibuja la escena con sprites, animaciones climáticas y overlays de diagnóstico.
6. `SimulationReportExportService` exporta reportes diarios o por lotes en PDF y JSON.

## Decisiones de arquitectura

- **Snapshots inmutables para la UI:** evitan que Avalonia UI modifique entidades internas del motor.
- **Servicios de sesión y reporte separados:** la ventana no construye motores ni PDF directamente.
- **Escena con Canvas lógico y Viewbox:** permite escalar la visualización sin recalcular todo el layout por resolución.
- **Sprites como recursos Avalonia:** se cargan desde `assets/` y se cachean en memoria para no reabrir imágenes en cada frame.
- **Reportes por lotes fuera del hilo de UI:** las simulaciones largas se ejecutan con `Task.Run` para evitar bloqueos visuales.

## Cambios visuales recientes

- Menú de inicio con opciones Tutorial, Simulador y Salir.
- Tutorial tipo feature tour con resaltado por zonas.
- Visual sandbox con escenas día/noche, sprites de cabinas y estaciones, y frames animados para lluvia, viento y neblina.
- Terminal inferior más alta para lectura de eventos, cabinas y estaciones.
- Diagnóstico rápido reducido para no cubrir información de la escena.
