# Justificación formal de estructuras de datos

Este documento sirve para defender académicamente por qué se eligieron una **lista circular manual** para cabinas y un **grafo manual** para estaciones.

---

## 1. Lista circular doblemente enlazada para cabinas

## Problema del dominio

Una cabina de teleférico no es un elemento que "aparece y desaparece" una sola vez. Operativamente:
- recorre un ciclo
- llega a estación
- se detiene
- descarga/carga
- invierte sentido
- vuelve a recorrer el tramo
- repite el proceso indefinidamente

Es decir, su comportamiento es **cíclico**.

---

## Estructura elegida

Se implementó manualmente `CircularLinkedList<T>` y sobre ella una fachada de dominio llamada `CabinRing`.

### ¿Por qué circular?
Porque el problema ya es circular por naturaleza. En una lista circular:
- el último nodo vuelve al primero
- siempre existe siguiente y anterior lógico
- no hay que programar manualmente el "si llegué al final, vuelve al principio"

Eso reduce errores y expresa mejor el problema real.

### ¿Por qué doblemente enlazada?
Porque en un sistema operativo de cabinas interesa consultar:
- la siguiente cabina
- la cabina previa
- el orden relativo de despacho
- vecinos inmediatos para seguridad futura

Una lista simplemente enlazada solo facilita avanzar hacia adelante. La doblemente enlazada da más flexibilidad con costo controlado.

---

## ¿Por qué no usar `List<T>` o arreglo?

### Sí se puede usar, pero no es la mejor representación conceptual.

Con un arreglo o `List<T>` tendrías que resolver manualmente:
- wrap-around del índice
- siguiente del último elemento
- anterior del primero
- inserciones o eliminaciones intermedias si el sistema crece

Además, la semántica del problema queda menos clara: una lista indexada representa mejor secuencias estáticas que circuitos operativos cíclicos.

### Comparación
- `List<T>` es buena para acceso por índice.
- la lista circular es mejor para **vecindad cíclica** y rotación lógica.

---

## ¿Por qué no usar `Queue<T>`?

Porque una cola modela un flujo **FIFO** puro.

El problema de las cabinas no es solamente "quién va primero". También interesa:
- navegar al siguiente y anterior
- rotar la cabeza del ciclo
- mantener la idea de circuito continuo

Una cola no representa bien ese comportamiento.

---

## ¿Por qué no usar `LinkedList<T>` del framework?

Por requerimiento académico y por control técnico.

La implementación propia permite:
- demostrar comprensión real de la estructura
- documentar exactamente por qué existe
- agregar validación de propiedad del nodo (`Owner`)
- adaptar la semántica a cabinas y despacho

Esto es importante si el proyecto debe defenderse como construcción propia.

---

## Ventajas concretas para este proyecto

- expresa la naturaleza cíclica del sistema
- simplifica navegación siguiente/anterior
- deja una base clara para múltiples cabinas por tramo
- reduce lógica manual de borde
- es reusable fuera de la UI

---

## Complejidad esperada

### Lista circular doblemente enlazada
- inserción local: O(1)
- eliminación local con nodo conocido: O(1)
- obtener siguiente/anterior: O(1)
- búsqueda por valor: O(n)

### Comentario importante
El costo de búsqueda lineal es aceptable aquí porque el volumen esperado de cabinas por tramo es pequeño comparado con el beneficio semántico de la estructura.

---

## 2. Grafo manual para estaciones

## Problema del dominio

Aunque el caso Mukumbarí actual es casi lineal, el proyecto no va a quedarse ahí. El sistema debe estar listo para:
- múltiples estaciones
- conexiones entre estaciones
- caminos alternos
- rutas de mantenimiento o simulación avanzada
- consultas de conectividad
- búsqueda de trayectos

Eso ya no es una lista simple: es una **red**.

---

## Estructura elegida

Se implementó `StationNetworkGraph` mediante:
- diccionario de estaciones
- diccionario de segmentos
- listas de adyacencia

---

## ¿Por qué un grafo y no una lista de estaciones?

Una lista lineal solo funciona bien si el mundo siempre es estrictamente lineal.

Problema:
- no modela ramificaciones de forma natural
- no resuelve caminos mínimos
- no permite crecer sin rehacer el modelo

El grafo sí lo hace.

---

## ¿Por qué no un árbol?

Un árbol obliga a una estructura jerárquica rígida.

Eso no es ideal porque una red de teleférico futura podría incluir:
- conexiones cruzadas
- rutas alternativas
- enlaces temporales o de mantenimiento

Un grafo es más general que un árbol y contiene al caso lineal como caso particular.

---

## ¿Por qué lista de adyacencia y no matriz de adyacencia?

Porque este problema es **disperso**.

En redes de estaciones, cada estación suele conectarse con pocas vecinas, no con todas.

### Matriz de adyacencia
Ventaja:
- consulta de conexión directa en O(1)

Desventajas:
- memoria O(V^2)
- poco natural si el sistema crece y sigue siendo disperso
- menos cómoda para recorrer vecinos reales

### Lista de adyacencia
Ventajas:
- memoria proporcional a estaciones + segmentos
- muy buena para recorrer vecinos
- muy buena para caminos mínimos en redes dispersas
- más cercana a cómo pensamos el problema real

Por eso es la mejor elección aquí.

---

## Ventajas concretas para este proyecto

- prepara el simulador para crecer sin rediseño radical
- permite cálculo de caminos mínimos
- representa mejor una red real que una lista rígida
- soporta bien evolución futura del mapa operacional

---

## Complejidad esperada

### Grafo con lista de adyacencia
- agregar estación: O(1)
- agregar segmento: O(1)
- obtener vecinos: O(grado del nodo)
- camino mínimo actual con selección lineal: adecuado para red pequeña

### Comentario importante
Para el tamaño actual del proyecto, una implementación clara y mantenible es más valiosa que una sobre-optimización prematura.

---

## Conclusión de defensa

### Lista circular
Se usa porque modela mejor el comportamiento cíclico de las cabinas que una lista lineal, un arreglo o una cola.

### Grafo
Se usa porque modela mejor la red de estaciones y su crecimiento futuro que una lista, una matriz o un árbol rígido.

En otras palabras: no se eligieron estas estructuras por "verse avanzadas", sino porque **se ajustan mejor al problema real del simulador** y además dejan una base correcta para las siguientes fases del proyecto.
