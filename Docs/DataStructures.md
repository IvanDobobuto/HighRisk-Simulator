# Data Structures Justification

## Criterio general

Las estructuras se eligieron para representar el problema real del teleférico, no para agregar complejidad innecesaria. Cada estructura debe aportar una ventaja clara en semántica, eficiencia o mantenibilidad.

## `CircularLinkedList<T>` y `CabinRing`

### Problema

Las cabinas se mueven en ciclos: salen, recorren el tramo, llegan a estación, cambian de sentido y vuelven a participar en el flujo.

### Justificación

Una lista circular expresa de forma natural la idea de “siguiente cabina” y “cabina anterior” sin lógica repetida de wrap-around. `CabinRing` encapsula esa relación y evita que otras partes del motor manipulen la estructura de forma insegura.

### Por qué no solo `List<T>`

`List<T>` almacena bien, pero no representa el ciclo. Cada consulta de vecindad requeriría cálculos manuales de índices y validaciones adicionales. La lista circular reduce esa lógica accidental.

## `StationNetworkGraph`

### Problema

Las estaciones y tramos forman una red ordenada con conexiones y distancias. Aunque el escenario actual es lineal, el dominio puede crecer hacia rutas alternativas o cierres parciales.

### Justificación

El grafo modela estaciones como nodos y tramos como aristas. Esto permite consultar conectividad, distancias y rutas sin incrustar reglas de red dentro del motor de movimiento.

### Por qué no solo arreglos paralelos

Los arreglos paralelos obligan a mantener sincronizados índices de estaciones, tramos y distancias. Un grafo concentra esa relación en una estructura coherente y extensible.

## `BinaryMinHeap<T>`

### Problema

El motor agenda eventos por prioridad temporal o severidad operacional. Siempre necesita extraer el próximo elemento más importante.

### Justificación

Un heap binario permite insertar y extraer mínimos en complejidad `O(log n)`. Es adecuado para colas de prioridad donde el orden total importa menos que obtener rápidamente el próximo evento.

### Por qué no ordenar una lista en cada tick

Ordenar repetidamente una lista tiene mayor costo y mezcla almacenamiento con política de prioridad. El heap mantiene la prioridad como responsabilidad propia.

## `LinkedStack<T>`

### Problema

Algunas decisiones operativas funcionan como historial: la última intervención o evento de revisión debe consultarse primero.

### Justificación

La pila enlazada representa LIFO de forma simple y evita realocaciones grandes cuando el historial crece de manera incremental.

### Por qué no `Stack<T>` estándar

`Stack<T>` sería suficiente en producción, pero la asignatura exige justificar e implementar estructuras propias. La versión enlazada permite demostrar comprensión de nodos, referencias y operaciones LIFO.

## Colecciones observables en Avalonia UI

### Problema

La UI necesita refrescar tablas de eventos, cabinas y estaciones cuando llega cada snapshot.

### Justificación

`ObservableCollection<T>` notifica cambios al binding de Avalonia. Por eso se usa solo en la capa de presentación, no dentro del motor.

## Separación entre entidades y snapshots

### Problema

Las entidades internas cambian durante cada tick. Exponerlas directamente a la UI podría permitir modificaciones fuera del motor.

### Justificación

Los snapshots son copias de lectura para interfaz, pruebas y reportes. Esta separación protege la lógica y facilita exportaciones reproducibles.
