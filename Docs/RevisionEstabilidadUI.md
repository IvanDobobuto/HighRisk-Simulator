# Revisión de estabilidad, exportación y UI

Esta iteración se centró en corregir los problemas reportados al compilar y al usar la interfaz.

## Cambios aplicados

### 1. Exportación y simulacro instantáneo
- El cierre acelerado de jornada dejó de generar un snapshot completo en cada tick durante `RunToEndOfService()`.
- La exportación a PDF y JSON se mantiene fuera del hilo principal desde `MainViewModel`.
- El servicio de exportación ahora fija la licencia de QuestPDF también desde su propio contexto y tiene ruta de respaldo si falla la carpeta principal de salida.

### 2. Errores de binding / PropertyPathWorker
- Las tablas de WPF y la barra de riesgo quedaron con `Mode=OneWay` en los bindings de solo lectura.
- Se eliminaron `init` setters de los DTO/configuraciones para reducir problemas de compatibilidad y de edición/binding.

### 3. Rendimiento de UI
- La lista de objetivos de inyección ya no se reconstruye innecesariamente en cada snapshot si la topología de cabinas no cambió.
- Se mejoró el layout visual de la pestaña **Eventos y reportes** con estilos de `DataGrid` más legibles y columnas estables.
- La sección **Operación** ahora usa una vista de ruta con scroll propio y con posicionamiento visual más controlado para estaciones y cabinas.

## Archivos intervenidos
- `HighRiskSimulator.Core/Simulation/SimulationEngine.cs`
- `HighRiskSimulator/Services/SimulationReportExportService.cs`
- `HighRiskSimulator/Services/SimulationSessionRequest.cs`
- `HighRiskSimulator.Core/Simulation/SimulationOptions.cs`
- `HighRiskSimulator.Core/Simulation/SimulationRunReport.cs`
- `HighRiskSimulator.Core/Persistence/ISimulationRunRepository.cs`
- `HighRiskSimulator/ViewModels/MainViewModel.cs`
- `HighRiskSimulator/Views/MainWindow.xaml`
- `HighRiskSimulator/Views/MainWindow.xaml.cs`
- `HighRiskSimulator/App.xaml`
