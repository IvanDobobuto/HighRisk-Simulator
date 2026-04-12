# Justificación formal de estructuras de datos y contratos de soporte

## Objetivo de este documento

Este documento explica por qué se eligieron determinadas estructuras manuales y por qué ciertos contratos complementarios se consideran la opción más correcta para el contexto del simulador.

La idea no fue usar complejidad por apariencia. La selección se hizo porque cada estructura expresa mejor el problema real y deja mejor base para crecer.

---

## 1. Lista circular doblemente enlazada

## Problema del dominio

Las cabinas de un tramo siguen un comportamiento naturalmente cíclico:
- salen de una estación
- recorren el tramo
- llegan a la estación opuesta
- cambian de dirección
- repiten el ciclo

Eso no es una secuencia lineal simple. Es una operación circular.

---

## Estructura elegida

Se implementó manualmente:
- `CircularLinkedList<T>`
- `CabinRing`

---

## ¿Por qué no un `List<T>`?

Porque aunque una lista lineal almacena elementos, no expresa bien la semántica de “siguiente cabina” y “cabina anterior” cuando el problema es cíclico.

Con `List<T>` habría que resolver wrap-around y vecindad manualmente. Eso agrega lógica accidental y eleva la posibilidad de errores.

La lista circular describe el dominio con menos traducción conceptual.

---

## ¿Por qué doblemente enlazada y no solo enlazada?

Porque la operación real necesita razonar tanto sobre la siguiente como sobre la anterior cabina del ciclo.

Ventajas:
- siguiente cabina: O(1)
- cabina anterior: O(1)
- inserciones y reordenamientos locales: O(1) con nodo conocido

---

## 2. Grafo dirigido para estaciones y tramos

## Problema del dominio

Aunque Mukumbarí siga una línea principal, conceptualmente el sistema es una red de nodos y aristas.

Se necesita poder:
- consultar conexiones
- mantener un modelo extensible
- representar crecimiento futuro
- defender un diseño correcto de origen-destino

---

## Estructura elegida

Se implementó `StationNetworkGraph` usando:
- diccionario de estaciones
- diccionario de segmentos
- listas de adyacencia salientes y entrantes

---

## ¿Por qué un grafo y no una lista simple?

Porque una lista de estaciones solo sirve para un mundo estrictamente lineal.

El grafo permite:
- ramificaciones futuras
- rutas alternativas
- consultas de conectividad
- evolución del modelo sin rediseñar la base

---

## ¿Por qué dirigido?

Porque la red física tiene sentido operacional por dirección y porque el modelo gana claridad cuando la dirección forma parte explícita de la estructura.

---

## 3. Heap mínimo manual

## Problema del dominio

El simulador necesita manejar acciones futuras con prioridad temporal:
- incidentes guionizados
- retornos diferidos de pasajeros
- transferencias rápidas
- reacciones encadenadas

Cada acción tiene momento y prioridad.

---

## Estructura elegida

Se implementó `BinaryMinHeap<T>`.

---

## ¿Por qué no una lista ordenada?

Porque reordenar una lista completa en cada inserción introduce costo innecesario.

Con heap:
- inserción: O(log n)
- extracción del mínimo: O(log n)
- inspección del próximo evento: O(1)

Eso es más correcto para una cola de prioridad temporal.

---

## ¿Por qué no `PriorityQueue<TElement,TPriority>`?

Porque la defensa académica exige estructuras manuales y porque la implementación propia permite justificar el mantenimiento explícito del invariante de heap.

---

## 4. Pila enlazada manual

## Problema del dominio

El simulador necesita retener con facilidad el historial reciente de eventos para:
- mostrar narrativa reciente
- alimentar snapshots
- preparar futura persistencia rápida

Ese patrón es LIFO cuando lo primero que importa es lo último que ocurrió.

---

## Estructura elegida

Se implementó `LinkedStack<T>`.

---

## ¿Por qué no una lista o cola?

Porque la semántica correcta no es FIFO sino LIFO.

Ventajas:
- `Push`: O(1)
- `Pop`: O(1)
- `Peek`: O(1)

Además la implementación enlazada evita depender de redimensionamientos internos.

---

## 5. Árbol causal interno

## Problema del dominio

Los acontecimientos no debían evaluarse como hechos aislados. Una eventualidad nueva debía tener en cuenta lo ocurrido durante la jornada.

---

## Estructura elegida

Se implementó `EventualityTree`.

El árbol guarda:
- categoría del evento
- severidad
- presión
- tiempo
- etiquetas de contexto

Luego calcula presión causal para cascadas futuras.

---

## ¿Por qué un árbol y no solo una lista de eventos?

Una lista conserva orden temporal, pero no organiza relaciones con suficiente riqueza.

El árbol permite ponderar:
- profundidad
- recencia
- severidad
- cercanía temática

Eso entrega una memoria contextual más útil que una secuencia plana.

---

## 6. Perfil maestro de riesgo

## Problema del dominio

La nueva interfaz exige calibrar múltiples probabilidades sin convertir la configuración en un conjunto disperso de variables sin relación.

---

## Estructura elegida

Se creó `SimulationRiskTuningProfile`.

Concentra:
- multiplicador global
- tormentas
- vientos fuertes
- neblina
- desgaste mecánico
- falla mecánica de cabina
- cortes eléctricos
- picos de tensión

---

## ¿Por qué no guardar parámetros sueltos?

Porque una colección dispersa:
- complica validación
- dificulta serialización
- vuelve confuso el reporte
- genera duplicación de reglas entre UI y motor

El perfil único es mejor porque encapsula, normaliza y documenta la calibración completa.

---

## 7. `ObservableCollection<T>` para sincronización visual

## Problema del dominio

La UI debe actualizar cabinas, estaciones, eventos y toasts con mínima fricción de binding.

---

## Estructura elegida

Se utilizaron `ObservableCollection<T>` en el ViewModel.

---

## ¿Por qué no reconstruir listas anónimas en cada binding?

Porque eso complica notificaciones, entorpece la lectura del estado y fragmenta la sincronización de la interfaz.

`ObservableCollection<T>` es la opción correcta en WPF cuando la colección forma parte del estado vivo de la aplicación.

---

## 8. `Canvas` lógico fijo con `ViewBox`

## Problema del dominio

La escena debía adaptarse a resoluciones pequeñas sin perder proporción ni control de coordenadas.

---

## Estructura elegida

Se adoptó un `Canvas` con tamaño lógico fijo y un `ViewBox` para escalado proporcional.

---

## ¿Por qué no recalcular toda la geometría por resolución real?

Porque eso aumenta complejidad, vuelve más frágil el layout y complica el dibujo de cabinas, estaciones y overlays.

Con coordenadas lógicas fijas:
- la escena sigue siendo determinista
- la adaptación es proporcional
- el mantenimiento del dibujo es más estable

---

## 9. Contratos de persistencia preparados para MySQL

## Problema del dominio

La base de datos debe incorporarse después sin contaminar el motor ni obligar a reescribir toda la simulación.

---

## Estructura elegida

Se dejaron:
- interfaces de repositorio
- settings de base de datos
- envelope de persistencia de corrida

---

## ¿Por qué no acoplar SQL directo dentro del motor?

Porque mezclar simulación con acceso a datos produce:
- alta dependencia
- pruebas más difíciles
- menor portabilidad
- mayor probabilidad de romper el núcleo cuando la infraestructura cambie

El contrato desacoplado es la opción correcta porque el motor solo produce información. La infraestructura futura la almacenará.

---

## 10. Bibliotecas especializadas donde sí aportan valor

### ScottPlot para telemetría

Se eligió **ScottPlot** porque resuelve con claridad lo que la UI necesita:
- series temporales en tiempo real
- control explícito del eje X
- bajo costo de integración con WPF

Es mejor que dibujar telemetría manualmente en `Canvas` porque evita reimplementar ejes, escalado y redibujado eficiente.

### QuestPDF para reportes

Se eligió **QuestPDF** porque la exportación documental sí requiere un motor especializado y confiable.

Es mejor que generar PDF artesanalmente porque reduce errores de maquetación y preserva tiempo de ingeniería para el problema real del proyecto.

---

## Conclusión general

Las estructuras y contratos seleccionados se justifican por dominio y por estabilidad:

- **lista circular**: ciclo operativo de cabinas
- **grafo**: red de estaciones y segmentos
- **heap**: prioridad temporal de acciones e incidentes
- **pila**: historial reciente LIFO
- **árbol causal**: memoria estructurada de eventualidades
- **perfil maestro de riesgo**: calibración coherente y persistible
- **observable collections**: sincronización limpia de UI
- **canvas lógico + viewbox**: adaptabilidad estable
- **contratos MySQL desacoplados**: persistencia futura sin contaminar el motor

Se eligieron porque describen mejor el problema real y porque dejan al proyecto listo para crecer sin perder coherencia técnica.
