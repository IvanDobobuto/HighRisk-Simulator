# Justificación formal de estructuras de datos

## Objetivo de este documento

Este documento explica por qué se eligieron determinadas estructuras manuales para el simulador y por qué son preferibles, en este contexto, a otras alternativas más simples o más genéricas.

La idea no fue “usar estructuras avanzadas por usarlas”, sino elegir las que mejor representan el problema real.

---

## 1. Lista circular doblemente enlazada

## Problema del dominio

Las cabinas de un tramo siguen un comportamiento naturalmente cíclico:
- salen de una estación
- recorren el tramo
- llegan a la estación opuesta
- cambian de dirección
- vuelven a repetir el ciclo

Eso no es una secuencia lineal simple. Es una operación circular.

---

## Estructura elegida

Se implementó manualmente:
- `CircularLinkedList<T>`
- `CabinRing`

---

## ¿Por qué no un `List<T>`?

Porque aunque una lista lineal sirve para almacenar elementos, no expresa bien la semántica de “siguiente cabina” y “cabina anterior” cuando el problema es cíclico.

Con un `List<T>` normalmente hay que manejar índices y hacer wrap-around manual.

Problemas:
- más lógica accidental
- más probabilidad de errores
- la estructura no expresa bien el modelo del dominio

La lista circular sí lo hace.

---

## ¿Por qué doblemente enlazada y no solo enlazada?

Porque en la operación real es útil razonar tanto sobre la siguiente como sobre la anterior cabina del ciclo.

Ventajas:
- siguiente cabina: O(1)
- cabina anterior: O(1)
- inserciones y reordenamientos locales: O(1) con nodo conocido

---

## 2. Grafo dirigido para estaciones y tramos

## Problema del dominio

Aunque Mukumbarí siga una línea principal, conceptualmente el sistema es una red de nodos (estaciones) y aristas (tramos).

Se necesita poder:
- consultar conexiones
- buscar rutas
- mantener un modelo extensible
- representar crecimiento futuro

---

## Estructura elegida

Se implementó `StationNetworkGraph` usando:
- diccionario de estaciones
- diccionario de segmentos
- listas de adyacencia salientes y entrantes

---

## ¿Por qué un grafo y no una lista simple?

Porque una lista de estaciones solo sirve para un mundo estrictamente lineal.

Un grafo permite:
- ramificaciones futuras
- rutas alternativas
- consultas de conectividad
- caminos mínimos

Es una decisión de escalabilidad correcta.

---

## ¿Por qué dirigido?

Porque la red física tiene sentido operacional por dirección:
- una estación puede tener salidas y entradas distintas
- los tramos pueden modelarse con reglas de recorrido explícitas
- la noción de origen-destino queda mejor representada

Aunque un tramo del escenario actual admita recorrido inverso, el modelo sigue siendo más claro si la dirección forma parte de la estructura.

---

## 3. Heap mínimo manual

## Problema del dominio

El simulador necesita manejar acciones futuras con prioridad temporal:
- incidentes guionizados
- retornos diferidos de pasajeros
- transferencias rápidas
- reacciones encadenadas

Cada acción tiene un momento y una prioridad.

---

## Estructura elegida

Se implementó `BinaryMinHeap<T>`.

---

## ¿Por qué no una lista ordenada?

Porque si cada vez que se inserta una acción futura hay que reordenar una lista completa, el costo y la lógica crecen innecesariamente.

Con heap:
- inserción: O(log n)
- extracción del mínimo: O(log n)
- inspección del próximo evento: O(1)

Eso es mucho más adecuado para una cola de prioridad.

---

## ¿Por qué no `PriorityQueue<TElement, TPriority>` de .NET?

Porque la defensa académica del proyecto exige estructuras manuales y porque una implementación propia permite justificar:
- el mantenimiento del invariante de heap
- heapify up
- heapify down
- relación directa con el problema

---

## 4. Pila enlazada manual

## Problema del dominio

El simulador necesita retener con facilidad el historial reciente de eventos para:
- mostrar la narrativa más reciente
- alimentar snapshots
- preparar futura persistencia rápida

Ese patrón es claramente LIFO en muchos casos de consulta rápida del “último evento relevante”.

---

## Estructura elegida

Se implementó `LinkedStack<T>`.

---

## ¿Por qué no una lista o cola?

Porque la semántica correcta no es FIFO sino LIFO. Interesa consultar primero lo más reciente.

Ventajas:
- `Push`: O(1)
- `Pop`: O(1)
- `Peek`: O(1)

Además la implementación enlazada evita depender de redimensionamientos internos de arreglos.

---

## 5. Árbol causal interno

## Problema del dominio

El usuario pidió que los acontecimientos no se evaluaran como hechos aislados, sino que una eventualidad nueva tuviera en cuenta lo ya ocurrido durante el día.

Eso requiere una estructura que preserve relaciones y permita navegar contexto acumulado.

---

## Estructura elegida

Se implementó `EventualityTree`.

El árbol guarda:
- categoría del evento
- severidad
- presión
- tiempo
- etiquetas de contexto

Luego calcula una presión causal para posibles cascadas futuras.

---

## ¿Por qué un árbol y no solo una lista de eventos?

Una lista solo conserva orden temporal. Un árbol permite organizar el contexto y ponderarlo por:
- profundidad
- recencia
- severidad
- cercanía temática

Eso no convierte al simulador en “IA mágica”, pero sí le da una memoria estructurada del día mucho más útil que una lista plana.

---

## Conclusión general

Las estructuras seleccionadas se justifican por dominio:

- **lista circular**: ciclo operativo de cabinas
- **grafo**: red de estaciones y tramos
- **heap**: prioridad temporal de acciones e incidentes
- **pila**: historial reciente LIFO
- **árbol causal**: memoria estructurada de eventualidades

Se eligieron porque describen mejor el problema real y porque dejan al proyecto listo para crecer sin romper su base conceptual.

---

## 6. Decisiones complementarias de infraestructura

Aunque el núcleo académico del proyecto se resolvió con estructuras manuales, hubo dos necesidades que sí convenía cubrir con bibliotecas especializadas: la telemetría visual y la exportación documental. La decisión fue deliberada: no tiene sentido reimplementar desde cero una librería de gráficas o un motor PDF cuando eso no aporta valor académico directo al problema del teleférico.

### ScottPlot para telemetría

Se eligió **ScottPlot** porque encaja bien con WPF y permite:
- series temporales en tiempo real
- actualización frecuente con bajo esfuerzo de integración
- control explícito del eje X para seguimiento automático de la ventana temporal

#### ¿Por qué no dibujar la telemetría manualmente en un `Canvas`?
Porque implicaría reimplementar:
- ejes
- escalado
- rotulación
- paneo
- redibujado eficiente

Eso aumentaría mucho el trabajo de interfaz sin mejorar el motor ni las estructuras de datos.

#### ¿Por qué no usar una librería más pesada?
Porque para esta fase solo se necesitaba:
- series simples
- lectura clara
- integración rápida con WPF

ScottPlot cubre eso con menos complejidad accidental.

### QuestPDF para reportes

Se eligió **QuestPDF** para generar el log final en PDF porque permite construir:
- tablas estructuradas
- encabezados y pies repetidos
- secciones narrativas
- documentos reproducibles desde código

#### ¿Por qué no exportar solo texto plano?
Porque el usuario pidió reportes formales con:
- tablas
- métricas
- estadística consolidada
- línea de tiempo entendible

Un PDF estructurado responde mucho mejor a esa necesidad.

#### ¿Por qué no depender de Word o de una exportación manual?
Porque el reporte se genera automáticamente desde el estado del motor, sin depender de edición humana posterior. Eso vuelve el flujo repetible, trazable y defendible.

### SQLite preparada pero no acoplada todavía

Se dejó la arquitectura lista para SQLite, pero no se acopló la base de datos directamente al motor.

#### ¿Por qué no conectarla ya dentro de `SimulationEngine`?
Porque eso mezclaría:
- simulación
- persistencia
- detalles de infraestructura

La decisión correcta fue dejar interfaces (`ISimulationSnapshotRepository`, `ISimulationRunRepository`) y una guía privada de integración. Así el motor se mantiene limpio y la conexión futura puede hacerse sin reescribir el núcleo.
