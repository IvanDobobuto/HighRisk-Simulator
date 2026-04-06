# HighRisk Simulator

Simulador estadístico-operativo de teleférico construido sobre **.NET 8 + WPF**, rediseñado para representar de forma más realista una jornada del sistema Mukumbarí mediante:

- motor por ticks con **delta time escalable** (1x, 2x, 3x)
- **múltiples cabinas por tramo** configurables
- **lista circular doblemente enlazada** para la gestión cíclica de cabinas
- **grafo dirigido** para estaciones y conexiones
- **heap mínimo manual** para contingencias y acciones futuras
- **pila enlazada manual** para historial reciente de eventos
- **árbol causal interno** para memoria contextual de eventualidades
- clima, estacionalidad y demanda no forzada
- simulacro instantáneo con **exportación a PDF y JSON**
- base desacoplada para persistencia futura con **SQLite**

La prioridad de esta iteración fue acercar el simulador a una jornada creíble: no todos los días deben terminar en catástrofe, pero sí deben existir condiciones, memorias y combinaciones que permitan que los incidentes emerjan cuando el entorno realmente los favorece.

---

## Estado actual del proyecto

### Prioridad alta completada
- Motor principal con **tiempo fijo por ticks** y escalamiento 1x, 2x y 3x.
- Base física y lógica de movimiento para cabinas por tramo.
- Modelo dirigido de estaciones y segmentos usando **grafo manual**.
- Gestión cíclica de cabinas con **lista circular doblemente enlazada**.
- Configuración de **múltiples cabinas por sentido y por tramo**.
- Interfaz preparada para escenarios reales e intensificados.

### Prioridad media completada
- Sistema de eventos no forzado con:
  - sobrecarga
  - falla mecánica
  - falla eléctrica
  - clima extremo
  - frenado de emergencia
  - cabina fuera de servicio
  - pérdida de separación
  - accidente por escalamiento severo
- **cola de prioridad / heap** para incidentes programados, retornos y transferencias futuras.
- **árbol causal interno** para encadenamiento contextual de eventualidades.
- temporadas y feriados venezolanos con impacto sobre afluencia.
- dos perfiles de presión:
  - **Operación realista**
  - **Entrenamiento intensificado**

### Prioridad baja completada
- Interfaz WPF reorganizada con scroll horizontal/vertical para pantallas pequeñas.
- Panel de control con inyección manual de fallas.
- telemetría con **ScottPlot** y seguimiento automático de la ventana temporal.
- exportación de reporte estructurado en **PDF** y respaldo técnico en **JSON**.

---

## Qué cambió respecto a la versión anterior

### 1) Colas mucho más realistas
Ahora las colas respetan reglas operativas de borde:
- **Barinitas** no genera cola de descenso.
- **Pico Espejo** no genera cola de ascenso.
- las estaciones intermedias **no crecen artificialmente** por sí mismas.
- las colas intermedias y superiores nacen principalmente cuando una cabina **descarga pasajeros reales** y luego esos pasajeros deciden continuar, quedarse o retornar.

### 2) Misma semilla, corridas parecidas pero no clones exactos
Se separó la lógica en:
- **semilla base**: define el “día macro”
- **semilla de variación operacional**: introduce pequeñas diferencias entre corridas del mismo día

Con esto, dos simulaciones con la misma semilla conservan el mismo carácter general, pero no repiten exactamente el mismo log en cada ejecución.

### 3) Eventualidades con memoria interna
Ya no se fuerza que “algo grave” ocurra cada jornada. El motor acumula contexto en un **árbol causal** que considera:
- clima previo
- severidad de eventos pasados
- presión del día
- recencia de incidentes
- categoría de la eventualidad

Eso permite que una falla nueva no se evalúe aislada, sino en función de lo que el sistema ya venía sufriendo.

### 4) Simulacro instantáneo real
La interfaz ahora tiene dos caminos:
- **Simulacro instantáneo**: reconstruye una jornada desde cero y la completa al instante.
- **Finalizar y exportar**: toma la corrida actual, la acelera hasta el cierre y exporta el resultado.

Ambos generan:
- **PDF estructurado**
- **JSON técnico**

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
|   |-- Roadmap.md
|   |-- SimulationRealism.md
|   `-- Privado/
|       `-- IntegracionSQLite.md
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

---

## Estructuras y decisiones académicas principales

### Lista circular doblemente enlazada
Se usa porque la operación de cabinas por tramo es cíclica. La estructura expresa mejor la relación **siguiente / anterior** que una lista lineal o un arreglo convencional.

### Grafo dirigido
Se usa porque el sistema es una red de estaciones y tramos, no solo una lista de nombres. Aunque Mukumbarí hoy sea lineal, el modelo queda listo para rutas futuras, análisis de conectividad y crecimiento del simulador.

### Heap manual
Se usa para manejar acciones futuras con prioridad:
- incidentes guionizados
- retornos diferidos de pasajeros
- transferencias internas
- reacciones encadenadas

Un heap es mejor que una lista ordenada porque evita reordenar todo el conjunto en cada inserción.

### Pila enlazada manual
Se usa para mantener el historial reciente de eventos con acceso **LIFO**, ideal para UI, trazabilidad rápida y justificación de los últimos estados críticos.

### Árbol causal interno
Se eligió árbol porque permite representar relaciones de dependencia entre eventualidades. No se usa como visual público, sino como estructura interna para que el sistema recuerde el contexto de lo ya ocurrido antes de calcular la siguiente desviación.

---

## Escenario base actual

El escenario principal toma como referencia el teleférico Mukumbarí:
- **5 estaciones**
- **4 tramos**
- recorrido total cercano a **12.5 km**
- configuración base de **1 cabina por sentido por tramo**

Además, el simulador permite elevar la densidad de cabinas por sentido para validaciones académicas de separación segura y presión operacional, sin perder la configuración realista base como punto de referencia.

---

## Qué muestra la interfaz

### Encabezado
- estado operacional
- tiempo simulado
- clima actual
- pasajeros procesados
- ocupación media
- incidentes activos
- barra de riesgo agregado

### Panel de control
- modo de simulación
- escenario guionizado
- semilla base
- fecha simulada
- modo de presión
- velocidad 1x / 2x / 3x
- cabinas por sentido

### Inyección de fallas
- falla mecánica
- falla eléctrica
- sobrecarga
- tormenta
- parada de emergencia

### Pestañas
- **Operación**: perfil 1D y telemetría ScottPlot
- **Eventos y reportes**: línea de eventos y trazabilidad de exportación
- **Cabinas y estaciones**: tablas operativas del estado actual

---

## Cómo ejecutar el proyecto

### Requisitos
- **.NET 8 SDK**
- Visual Studio con workload **.NET desktop development**

### Pasos
1. Abrir `HighRiskSimulator.sln`.
2. Restaurar paquetes NuGet.
3. Establecer `HighRiskSimulator` como proyecto de inicio.
4. Ejecutar con `F5`.

### Pruebas
Desde la raíz de la solución:

```bash
dotnet test
```

---

## Dependencias relevantes

### En uso ahora
- `ScottPlot.WPF` para telemetría en tiempo real.
- `QuestPDF` para exportación estructurada de reportes.
- `xUnit` y `Microsoft.NET.Test.Sdk` para pruebas.

### Preparadas para después
- SQLite a través de `ISimulationSnapshotRepository` e `ISimulationRunRepository`.

---

## Documentación incluida

- `Docs/Architecture.md` - arquitectura y flujo técnico del sistema.
- `Docs/DataStructuresJustification.md` - justificación formal de estructuras manuales.
- `Docs/ManualDeUso.md` - guía de uso de la UI y de los reportes.
- `Docs/Roadmap.md` - siguientes fases sugeridas del proyecto.
- `Docs/SimulationRealism.md` - explicación de realismo operativo, colas, temporada y árbol causal.
- `Docs/RevisionEstabilidadUI.md` - notas de corrección sobre estabilidad, exportación y bindings de la interfaz.
- `Docs/Privado/IntegracionSQLite.md` - guía privada para tu equipo, no pensada para entrega académica.

---

## Nota importante sobre la fase actual

Esta versión deja una base mucho más sólida y más realista, pero sigue siendo un simulador académico. Aún no pretende reemplazar un modelo físico industrial completo del sistema real. La prioridad fue dejar:

- coherencia operacional
- crecimiento técnico correcto
- estructuras justificables
- interfaz usable
- reportes útiles

para que las siguientes fases puedan enfocarse en 2D, texturas, terreno, partículas, replay histórico y persistencia real sin rehacer el núcleo.
