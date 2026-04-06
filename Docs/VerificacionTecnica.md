# Verificación técnica aplicada a esta iteración

## Alcance de la revisión

En esta iteración se hizo una revisión técnica **estática** del proyecto para atacar los puntos que estaban generando más fricción:

- bloqueos al usar **Simulacro instantáneo**, **Finalizar y exportar** y **Exportar reporte actual**;
- bindings de WPF con propiedades de solo lectura o con setters privados;
- lectura deficiente en las pestañas **Operación** y **Eventos y reportes**;
- estabilidad de la exportación PDF/JSON;
- compatibilidad de DTOs y modelos de reporte con serialización e inicialización.

## Ajustes aplicados

### 1. Motor de simulación
- `RunToEndOfService()` quedó en modo acelerado **sin publicar snapshots intermedios** en cada tick.
- ahora genera el snapshot final al cierre, en lugar de reconstruir toda la UI durante toda la jornada acelerada.
- este cambio reduce el costo de CPU y memoria en simulacros instantáneos y en el cierre acelerado.

### 2. Exportación de reportes
- `SimulationReportExportService` conserva exportación a **PDF** y **JSON**.
- la resolución del directorio de salida intenta varias rutas válidas de usuario antes de fallar.
- la exportación sigue ejecutándose fuera del hilo principal desde `MainViewModel`.

### 3. ViewModel principal
- se reforzó el control de estado ocupado (`_isBusy`) para evitar reentradas de exportación o simulación acelerada.
- las acciones pesadas continúan ejecutándose mediante `Task.Run`, y luego solo actualizan la UI al finalizar.

### 4. DTOs y modelos de intercambio
- se sustituyeron varias propiedades `init` por propiedades `set` en configuraciones y reportes.
- esto reduce fricción con serialización, inicialización y tooling, y evita errores innecesarios en escenarios WPF.

### 5. Interfaz WPF
- se ajustó el binding del `ProgressBar` de riesgo a `Mode=OneWay` para evitar intentos de escritura desde WPF sobre una propiedad expuesta solo para lectura visual.
- se añadieron estilos y tamaños mínimos para los `DataGrid`.
- se mejoró la legibilidad de la pestaña **Eventos y reportes**.
- se añadieron áreas con desplazamiento para pantallas pequeñas y para la vista operativa del trayecto.

## Verificación estática realizada

- revisión estructural de archivos `.cs` para detectar problemas evidentes de delimitadores y consistencia general;
- validación XML de `App.xaml` y `MainWindow.xaml`;
- revisión manual de bindings WPF sensibles y de los puntos de exportación/reportes.

## Validación pendiente en Windows

Todavía es necesario ejecutar en una máquina Windows con SDK de .NET instalado:

```bash
dotnet restore
dotnet build HighRiskSimulator.sln
dotnet test HighRiskSimulator.sln
```

Ese paso sigue siendo obligatorio para la validación final del proyecto porque la aplicación es WPF y esta revisión no sustituye una compilación real del entorno de escritorio de Windows.
