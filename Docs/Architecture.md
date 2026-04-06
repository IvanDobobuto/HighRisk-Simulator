# Arquitectura actual del proyecto

## Objetivo de la iteración

Esta versión busca consolidar una base seria para el simulador del teleférico. Ya no se trata solo de “mover cabinas”, sino de representar una jornada con:

- demanda turística variable
- clima cambiante
- múltiples cabinas
- eventualidades condicionadas por contexto
- exportación de reportes
- base lista para persistencia futura

La idea arquitectónica central fue desacoplar al máximo:
- el **núcleo de simulación**
- la **UI WPF**
- la **infraestructura futura**

---

## Estructura por capas

## 1. Capa Core (`HighRiskSimulator.Core`)

Es la capa de dominio y simulación. Aquí vive todo lo que debe seguir funcionando aunque mañana la interfaz deje de ser WPF.

### `Domain/`
Contiene:
- entidades principales (`Station`, `TrackSegment`, `Cabin`, `WeatherState`, `SimulationEvent`)
- enums técnicos y traducciones visibles en español
- snapshots inmutables consumidos por UI, pruebas y persistencia futura

### `DataStructures/`
Contiene estructuras manuales obligatorias o justificadas por dominio:
- `CircularLinkedList<T>`
- `CabinRing`
- `StationNetworkGraph`
- `BinaryMinHeap<T>`
- `LinkedStack<T>`

### `Simulation/`
Contiene el flujo del motor:
- `SimulationOptions`
- `ScenarioDefinition`
- `SimulationModel`
- `RollingMetricSeries`
- `EventualityTree`
- `SimulationEngine`
- `SimulationRunReport`
- calendario de estacionalidad venezolana

### `Factories/`
`MukumbariScenarioFactory` construye:
- la red de estaciones y segmentos
- el perfil del día
- la estacionalidad aplicada
- las cabinas iniciales
- las colas iniciales realistas
- el catálogo de escenarios guionizados

### `Persistence/`
Aquí se dejan los contratos desacoplados para persistencia:
- `ISimulationSnapshotRepository`
- `ISimulationRunRepository`
- implementaciones nulas para esta fase

---

## 2. Capa UI (`HighRiskSimulator`)

Presenta datos y ejecuta acciones del usuario.

### `ViewModels/`
`MainViewModel` coordina la sesión completa:
- crea o reinicia corridas
- controla inicio, pausa y paso
- corre simulacros instantáneos
- acelera la corrida actual hasta el cierre
- dispara exportación PDF/JSON
- inyecta fallas manuales
- transforma snapshots en colecciones observables

### `Views/`
`MainWindow.xaml` define una interfaz con:
- scroll vertical/horizontal para equipos pequeños
- panel de control y de inyección
- pestañas de operación, eventos y tablas
- canvas 1D del sistema
- ScottPlot para telemetría

`MainWindow.xaml.cs` se usa solo para:
- dibujo del perfil de ruta
- representación visual de cabinas y estaciones
- ajuste automático de la ventana de ScottPlot

### `Services/`
Contiene lógica de aplicación, no de dominio:
- `SimulationSessionService`
- `SimulationReportExportService`
- request de sesión

---

## Flujo principal del motor

Cada tick lógico sigue una secuencia clara:

1. ajustar delta time efectivo según 1x/2x/3x
2. actualizar red eléctrica si estaba degradada
3. actualizar clima en función de estacionalidad y volatilidad
4. crear demanda exógena principal en Barinitas
5. procesar acciones pendientes desde el heap
6. mover cabinas y resolver paradas en estación
7. programar transferencias y retornos diferidos
8. evaluar incidentes aleatorios no forzados
9. recalcular riesgo y telemetría
10. aplicar reglas de seguridad
11. producir snapshot inmutable
12. guardar snapshot mediante repositorio desacoplado

---

## Justificación del delta time escalable

La velocidad visual de la simulación se desacopló del motor WPF mediante un factor 1x / 2x / 3x aplicado al paso lógico. Esto permite:

- acelerar análisis sin reescribir física
- mantener un motor consistente por ticks
- permitir simulacro interactivo o corridas más rápidas
- soportar el modo instantáneo sin depender del temporizador visual

---

## Modelado de demanda y realismo operacional

La demanda ya no aparece en todas las estaciones de forma arbitraria.

### Ahora ocurre así
- Barinitas recibe la demanda turística principal desde fuera del sistema.
- estaciones intermedias crecen por **descarga real de cabinas**.
- después de bajar, una parte de esos pasajeros:
  - continúa la ruta
  - permanece un tiempo y retorna
  - simplemente sale del sistema
- Pico Espejo solo genera retorno descendente.
- Barinitas nunca genera cola de descenso.

Esta decisión mueve el simulador hacia un comportamiento más cercano a una jornada real.

---

## Árbol causal interno

El motor usa `EventualityTree` para registrar memoria del día. No es un árbol “decorativo”. Se usa para:

- sembrar el contexto inicial del día
- registrar clima relevante
- registrar incidentes y severidad
- calcular presión causal futura

Con esto una posible falla futura puede volverse más o menos probable según lo ocurrido antes.

---

## Heap y pila en el motor

### Heap manual
`BinaryMinHeap<T>` gestiona acciones con prioridad temporal:
- incidentes de escenarios
- decisiones diferidas de pasajeros
- reacciones futuras del sistema

### Pila manual
`LinkedStack<T>` mantiene historial reciente de eventos para:
- narrativa actual
- UI de eventos recientes
- snapshots
- futura persistencia rápida

---

## Exportación de reportes

La generación de reportes quedó fuera del motor. La UI solicita un `SimulationRunReport` y el servicio de exportación se encarga de producir:

- **PDF** con tablas y resumen ejecutivo
- **JSON** como respaldo técnico completo

Esto mantiene la responsabilidad bien separada:
- el motor **simula**
- el servicio **formatea y exporta**

---

## Persistencia desacoplada

SQLite no se integró todavía al flujo principal para no contaminar el núcleo con infraestructura. En su lugar se dejaron contratos listos para:

- guardar snapshots
- guardar metadatos de corrida
- guardar reporte consolidado
- consultar historial

Eso facilita una implementación futura sin rediseñar el motor.

---

## Conclusión arquitectónica

La base actual deja al proyecto en un punto mucho más seguro para crecer. El sistema ya tiene:

- dominio claro
- estructuras justificables
- simulación desacoplada de la UI
- exportación estructurada
- espacio limpio para persistencia futura

Con esta arquitectura, avanzar hacia 2D, replay histórico o SQLite real será una extensión, no una reconstrucción completa.
