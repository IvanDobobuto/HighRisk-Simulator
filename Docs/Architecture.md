# Arquitectura actual del proyecto

## Objetivo de esta iteración

La arquitectura de esta versión se concentró en tres metas simultáneas:

- mejorar drásticamente la legibilidad de la interfaz
- ampliar el control académico de riesgo sin romper el motor
- dejar la persistencia futura en MySQL preparada desde contratos claros

La decisión rectora fue mantener desacopladas tres responsabilidades:
- el **núcleo de simulación**
- la **presentación WPF**
- la **infraestructura de persistencia futura**

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
Contiene estructuras manuales justificadas por dominio:
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
- `SimulationRiskTuningProfile`
- `SimulationEngine`
- `SimulationRunReport`

### `Factories/`
`MukumbariScenarioFactory` construye:
- la red de estaciones y segmentos
- el perfil del día
- la estacionalidad aplicada
- las cabinas iniciales
- las colas iniciales
- el catálogo de escenarios guionizados

### `Persistence/`
Aquí se dejaron los contratos desacoplados para persistencia:
- `ISimulationSnapshotRepository`
- `ISimulationRunRepository`
- `SimulationDatabaseSettings`
- `SimulationPersistenceEnvelope`
- implementaciones nulas para esta fase

La razón de este diseño es directa: el motor debe seguir produciendo snapshots y reportes aunque todavía no exista la conexión física a base de datos.

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
- aplica calibración de riesgo en caliente
- inyecta fallas manuales sin pausar
- mantiene toasts y colecciones observables

### `Views/`
`MainWindow` se rediseñó con una distribución tripartita:
- panel lateral de operador
- escena principal sandbox
- bloque lateral analítico

El code-behind quedó limitado a presentación:
- dibujo del sandbox 2D
- lectura visual de estados
- actualización de ScottPlot

### `Services/`
Contiene servicios de aplicación que no deben contaminar el motor:
- `SimulationSessionService`
- `SimulationReportExportService`

### `Models/` y `Helpers/`
Agrupan modelos de apoyo a UI y utilidades de comandos.

---

## 3. Capa de pruebas (`HighRiskSimulator.Tests`)

Mantiene verificación del comportamiento esencial del proyecto:
- estructuras de datos
- reglas base del motor
- consistencia del dominio

La decisión de conservar pruebas en una capa separada evita que la UI condicione la validación del núcleo.

---

## Decisiones arquitectónicas nuevas de esta iteración

## 1. Panel lateral colapsable

Se eligió `Expander` lateral en lugar de multiplicar paneles dispersos porque:
- agrupa todo el control manual en un punto fijo
- reduce ruido cognitivo en la escena central
- se puede contraer sin destruir funcionalidad
- conserva compatibilidad total con WPF nativo

Es mejor que un mosaico de controles distribuidos porque el usuario siempre sabe dónde intervenir.

---

## 2. Escena principal con `Canvas` lógico fijo y `ViewBox`

Se eligió una resolución lógica fija escalada por `ViewBox` porque:
- permite preservar proporciones en 800x600 y en resoluciones mayores
- evita recalcular toda la composición visual para cada tamaño real de ventana
- mantiene coordenadas de dibujo simples y deterministas
- reduce errores de layout en escenas dinámicas

Esta decisión es más estable que usar coordenadas dependientes del tamaño real de cada control y más óptima que introducir un motor externo cuando el problema central sigue siendo académico y operativo.

---

## 3. Toasts no bloqueantes

Se eligieron notificaciones tipo toast en lugar de diálogos modales porque:
- confirman acciones del operador
- no tapan la vista principal
- no detienen el flujo de la simulación
- reducen interrupciones innecesarias en pruebas intensivas

Es la opción correcta para una consola operacional donde las acciones deben confirmarse sin secuestrar la atención.

---

## 4. Perfil maestro de riesgo desacoplado

Se centralizó la calibración en `SimulationRiskTuningProfile` porque:
- reduce duplicación de parámetros
- hace serializable la configuración
- deja lista la persistencia histórica de perfiles
- evita que cada componente invente su propio criterio de ajuste

Esto es mejor que guardar sliders sueltos en la UI o multiplicadores dispersos en varios servicios.

---

## 5. Inyección de fallas sin pausa

La capacidad de intervenir en caliente se implementó directamente en el motor porque la corrida no debía depender de un estado intermedio de pausa para aceptar una contingencia.

La decisión es mejor que forzar pausa previa porque:
- conserva continuidad temporal
- representa mejor una operación real
- permite entrenamiento reactivo auténtico

---

## 6. Preparación explícita para MySQL

No se integró aún la infraestructura física, pero sí se definió la frontera correcta:
- settings de conexión
- contrato de persistencia
- envelope de corrida completa
- documento privado de integración

La razón es evitar una integración prematura y frágil dentro del motor. El núcleo produce información. La infraestructura futura la guarda.

---

## Flujo principal de la aplicación

1. La UI construye `SimulationSessionRequest`.
2. `SimulationSessionService` traduce la solicitud a `SimulationOptions`.
3. `MukumbariScenarioFactory` crea modelo, escenario y motor.
4. `SimulationEngine` produce snapshots y reportes.
5. `MainViewModel` sincroniza el snapshot con la UI.
6. `MainWindow.xaml.cs` representa visualmente la escena y la telemetría.
7. `SimulationReportExportService` exporta PDF y JSON.
8. La futura infraestructura MySQL podrá persistir el resultado sin modificar el motor.

---

## Criterio rector de la arquitectura

La base actual prioriza:
- **un motor determinista y explicable**
- **una UI clara y estable**
- **infraestructura persistente desacoplada**

Con esta arquitectura, avanzar hacia replay histórico, base de datos MySQL real, dashboards comparativos o una escena visual todavía más rica será una extensión coherente, no una reconstrucción completa.
