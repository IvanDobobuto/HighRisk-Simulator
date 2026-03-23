# Arquitectura actual del proyecto

## Objetivo de esta iteración

Esta iteración no busca cerrar el simulador final, sino dejar una **base de ingeniería correcta** sobre la que sea seguro seguir creciendo. La idea es que cada semana puedas agregar nuevas capacidades sin romper lo ya construido.

Los objetivos logrados aquí fueron:
- separar motor y UI
- evitar lógica de simulación dentro de WPF
- usar datos y estructuras que puedan escalar
- preparar persistencia futura sin acoplar el núcleo
- dejar pruebas automáticas sobre la base crítica

---

## Estructura por capas

## 1. Capa Core (`HighRiskSimulator.Core`)

Es la capa más importante. Aquí vive toda la lógica que debe seguir funcionando aunque mañana cambies WPF por otra tecnología.

### `Domain/`
Contiene las entidades y enums del dominio:
- `Station`
- `TrackSegment`
- `Cabin`
- `WeatherState`
- `SimulationEvent`
- snapshots inmutables
- enums de estados, severidad, clima y modo de simulación

### `DataStructures/`
Contiene las estructuras manuales requeridas por el proyecto:
- `CircularLinkedList<T>`
- `CabinRing`
- `StationNetworkGraph`

### `Simulation/`
Contiene el corazón del motor:
- `SimulationOptions`
- `ScenarioDefinition`
- `SimulationModel`
- `RollingMetricSeries`
- `SimulationEngine`

### `Factories/`
`MukumbariScenarioFactory` construye el escenario base, los perfiles de día y el catálogo de escenarios guionizados.

### `Persistence/`
Contiene `ISimulationSnapshotRepository` y su implementación nula. Aquí se conectará SQLite más adelante.

---

## 2. Capa UI (`HighRiskSimulator`)

Esta capa solo presenta información y dispara acciones del usuario.

### `ViewModels/`
`MainViewModel` coordina la simulación desde la interfaz.

Responsabilidades:
- crear/reiniciar el motor
- iniciar, pausar y avanzar la simulación
- exponer propiedades para binding
- transformar snapshots en colecciones observables para la UI

### `Views/`
`MainWindow.xaml` define la interfaz.

`MainWindow.xaml.cs` se usa únicamente para:
- dibujar el perfil 1D en el `Canvas`
- refrescar ScottPlot

Es decir: el code-behind no contiene lógica de negocio, solo lógica de presentación.

### `Services/`
`SimulationSessionService` actúa como punto de entrada para crear sesiones de simulación desde la UI sin acoplarla directamente a todos los detalles del núcleo.

---

## 3. Capa de pruebas (`HighRiskSimulator.Tests`)

Esta capa valida la base del proyecto.

Actualmente cubre:
- topología básica de la lista circular
- consulta de caminos del grafo
- avance del motor
- generación de eventos por sobrecarga
- inyección de eventos de escenarios guionizados

---

## Ciclo de simulación por tick

Cada llamada a `SimulationEngine.Step()` ejecuta una secuencia fija:

1. avanzar el tiempo simulado
2. actualizar estado de energía
3. actualizar clima
4. actualizar demanda de pasajeros
5. procesar incidentes guionizados
6. procesar incidentes aleatorios
7. actualizar movimiento de cabinas
8. recalcular riesgo y telemetría
9. evaluar reglas de seguridad
10. recalcular estado operacional
11. construir snapshot inmutable
12. enviar snapshot a repositorio desacoplado

Este pipeline es importante porque ordena el motor y evita efectos secundarios caóticos.

---

## Por qué el motor es determinista

La simulación es determinista por tres razones:

### 1) Reproducibilidad
Si usas la misma semilla y las mismas opciones, el motor debe generar la misma secuencia de eventos.

### 2) Depuración
Cuando aparezca un bug, puedes repetir el caso exacto.

### 3) Escalabilidad
Cuando llegues al sandbox 2D, el render puede correr a una tasa visual distinta, pero el motor seguirá teniendo una base estable.

---

## Por qué usar snapshots inmutables

El motor genera `SimulationSnapshot` y la UI solo consume esos datos.

Ventajas:
- la interfaz no manipula directamente el dominio
- pruebas más simples
- persistencia más simple
- futura exportación a JSON o base de datos más sencilla
- posibilidad de reproducir estados históricos del sistema

---

## Decisiones de modelado actuales

### Sistema por tramos
Cada segmento opera como un bloque entre dos estaciones. Esto aproxima bien el caso Mukumbarí para esta fase y permite que luego agregues reglas específicas por tramo.

### Una cabina por tramo
Se eligió esta aproximación porque:
- respeta mejor el comportamiento operativo descrito del sistema real
- reduce complejidad innecesaria en la primera base seria
- no impide que más adelante agregues múltiples cabinas por tramo

### Pasajeros agregados
No se modelan aún como individuos, sino como conteos por estación y por cabina.

Esto es correcto para esta fase porque:
- reduce complejidad
- permite construir estadísticas y presión operativa
- deja la puerta abierta a un modelo individual futuro

---

## Extensibilidad prevista

La arquitectura ya quedó lista para crecer en estas líneas:
- persistencia SQLite real
- escenarios históricos guardados y replay
- configuración de parámetros desde la UI
- múltiples cabinas por tramo
- mallas de estaciones no lineales
- sandbox 2D
- física más avanzada
- mantenimiento preventivo y correctivo
- evacuación, rescate y protocolos operativos

---

## Resumen arquitectónico

La base actual cumple una meta importante: el proyecto dejó de ser un experimento WPF y pasó a ser un **simulador con núcleo propio**, lo cual era el paso correcto antes de intentar una visualización 2D más ambiciosa.
