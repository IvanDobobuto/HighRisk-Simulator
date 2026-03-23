# HighRisk Simulator - Professional Base

Base profesional del simulador de análisis de riesgo para teleféricos, rediseñada sobre **.NET 8 + WPF** para dejar un núcleo de simulación sólido, extensible y listo para crecer hacia un entorno visual **Sandbox 2D** en futuras iteraciones.

Esta iteración reemplaza el starter original por una arquitectura por capas, un motor determinista por ticks, estructuras de datos manuales propias y una interfaz WPF mucho más robusta, manteniendo la esencia académica del proyecto y preservando el contenido del README original al final de este archivo.

---

## Estado actual del proyecto

### Prioridad alta completada
- Motor de simulación con **tiempo fijo por ticks**.
- Física cinemática base: posición, velocidad, aceleración, frenado de servicio y frenado de emergencia.
- Soporte para **estaciones conectadas por grafo**.
- Implementación manual de **lista circular doblemente enlazada** para modelar orden cíclico de cabinas.
- Soporte para operación por tramos con cabinas, estaciones, colas de pasajeros y reglas de seguridad.

### Prioridad media completada
- Sistema de eventos con:
  - sobrecarga
  - falla mecánica
  - falla eléctrica
  - frenado de emergencia
  - cabina fuera de servicio
  - pérdida de separación segura
  - accidente por escalamiento de severidad
  - clima extremo
- Modo **aleatorio inteligente** reproducible por semilla.
- Modo **escenario específico** con incidentes programados.

### UI completada
- Interfaz WPF reorganizada profesionalmente.
- Visualización 1D sobre perfil altimétrico inspirado en Mukumbarí.
- Telemetría en tiempo real con **ScottPlot**.
- Log de eventos, panel de cabinas, panel de estaciones y tarjetas de estado.

### Calidad de base completada
- Solución separada en múltiples proyectos.
- Persistencia desacoplada mediante interfaz lista para SQLite futura.
- Conjunto inicial de pruebas unitarias para motor y estructuras de datos.

---

## Arquitectura actual

```text
HighRisk-Simulator/
|-- HighRiskSimulator.sln
|-- Directory.Build.props
|-- README.md
|-- Docs/
|   |-- Architecture.md
|   |-- DataStructuresJustification.md
|   |-- ManualDeUso.md
|   `-- Roadmap.md
|-- HighRiskSimulator.Core/
|   |-- Domain/
|   |-- DataStructures/
|   |-- Simulation/
|   |-- Factories/
|   `-- Persistence/
|-- HighRiskSimulator/
|   |-- Views/
|   |-- ViewModels/
|   |-- Services/
|   |-- Models/
|   `-- Helpers/
`-- HighRiskSimulator.Tests/
    |-- DataStructures/
    `-- Simulation/
```

### Proyectos

#### `HighRiskSimulator.Core`
Contiene el corazón del sistema:
- dominio
- motor de simulación
- estructuras de datos manuales
- escenarios
- contratos de persistencia futura

#### `HighRiskSimulator`
Aplicación WPF:
- ViewModels
- vistas
- renderizado 1D en `Canvas`
- telemetría con ScottPlot

#### `HighRiskSimulator.Tests`
Pruebas unitarias para:
- lista circular
- grafo de estaciones
- comportamiento base del motor

---

## Decisiones técnicas importantes

### 1) Motor determinista por ticks
Se eligió una simulación de **paso fijo** porque es la mejor base para:
- reproducibilidad por semilla
- depuración seria
- pruebas unitarias
- futura migración a render 2D
- desacople entre tiempo visual y tiempo lógico

### 2) Lista circular propia
Se implementó una **lista circular doblemente enlazada manual** porque el problema tiene naturaleza cíclica: las cabinas repiten indefinidamente un circuito operativo y se necesita poder razonar sobre el siguiente y el anterior elemento sin lógica manual de wrap-around.

### 3) Grafo de estaciones
Aunque hoy el escenario principal sigue una secuencia casi lineal, se eligió un **grafo** porque el proyecto crecerá por semanas y eventualmente puede requerir:
- ramificaciones
- rutas alternativas
- mantenimiento por segmentos
- análisis de conectividad
- caminos mínimos
- futuros modos de rescate o evacuación

### 4) Persistencia desacoplada
SQLite quedó **preparado pero no acoplado**. El motor no depende de infraestructura, y la futura persistencia podrá conectarse a través de `ISimulationSnapshotRepository`.

---

## Escenario base actual

El sistema base está inspirado en el teleférico Mukumbarí y modela estas estaciones:
- Barinitas
- La Montaña
- La Aguada
- Loma Redonda
- Pico Espejo

Se usan altitudes y un recorrido total aproximado de ~12.5 km para conseguir una simulación pedagógica consistente. En esta fase se modela **una cabina por tramo**, decisión que además aproxima el comportamiento operativo descrito para el sistema real, donde cada tramo funciona de forma independiente.

> Importante: esta versión no pretende ser todavía un gemelo digital exacto del sistema real. Es una **base académica sólida y extensible** para evolucionar hacia una simulación más profunda.

---

## Cómo ejecutar el proyecto

### Requisitos
- **.NET 8 SDK**
- **Visual Studio 2026** recomendado
- Workload **.NET desktop development**

### Pasos
1. Abrir `HighRiskSimulator.sln`.
2. Restaurar paquetes NuGet.
3. Establecer `HighRiskSimulator` como proyecto de inicio.
4. Ejecutar con `F5`.

### Pruebas
Desde terminal en la raíz de la solución:

```bash
dotnet test
```

---

## Qué probar primero

### Modo aleatorio inteligente
- Ejecuta la simulación con la semilla por defecto.
- Observa cómo cambian demanda, clima, riesgo y eventos.
- Reinicia con la misma semilla para verificar reproducibilidad.

### Escenarios específicos
- `Sobrecarga en temporada alta`
- `Falla eléctrica general`
- `Tormenta andina en cotas altas`

### Interfaz
- Revisa el perfil altimétrico 1D.
- Observa los colores de las cabinas según estado.
- Mira la evolución del riesgo, ocupación media y presión climática en ScottPlot.
- Verifica el log de eventos y el cambio de estado operacional.

---

## Documentación incluida

- `Docs/Architecture.md` - explicación técnica de la arquitectura.
- `Docs/DataStructuresJustification.md` - justificación formal de lista circular y grafo.
- `Docs/ManualDeUso.md` - guía de uso y validación manual.
- `Docs/Roadmap.md` - próximos pasos sugeridos del proyecto.

---

## Dependencias relevantes

### En uso ahora
- `ScottPlot.WPF` para telemetría visual.
- `xUnit` y `Microsoft.NET.Test.Sdk` para pruebas.

### Preparadas para después
- SQLite a través de una implementación futura de `ISimulationSnapshotRepository`.

---

## Notas de diseño

- El código está en **inglés** para mantener convención profesional.
- Los **comentarios y documentación** están en **español** para facilitar defensa académica.
- La UI consume **snapshots inmutables** del motor; esto simplifica pruebas, renderizado y futura evolución a un sandbox 2D.
- La estructura actual ya separa con claridad **dominio**, **motor**, **infraestructura futura** y **presentación**.

---

## README original del starter (preservado)

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
|-- HighRiskSimulator.sln
|-- .gitignore
|-- README.md
`-- HighRiskSimulator/
    |-- App.xaml
    |-- App.xaml.cs
    |-- HighRiskSimulator.csproj
    |-- Models/
    |-- ViewModels/
    |-- Views/
    |-- Services/
    `-- Helpers/
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
