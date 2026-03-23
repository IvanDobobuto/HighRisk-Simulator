# Roadmap sugerido

## Objetivo general

Evolucionar desde una base profesional 1D hacia un simulador visual 2D con más realismo operativo, mejor analítica y capacidad de reproducir escenarios históricos o configurados.

---

## Fase siguiente inmediata

### 1. Configuración avanzada desde la interfaz
Agregar paneles para modificar:
- capacidad de cabinas
- velocidad máxima
- aceleraciones
- tiempos de parada
- severidad del clima
- presión de incidentes
- demanda

### 2. Persistencia real con SQLite
Implementar una clase concreta que cumpla `ISimulationSnapshotRepository` y permita:
- guardar snapshots
- guardar eventos
- guardar escenarios personalizados
- reproducir ejecuciones históricas

### 3. Mejorar pruebas automáticas
Ampliar pruebas para:
- separación segura
- frenado de emergencia
- cambio de clima
- salida y retorno de servicio
- escalamiento a accidente

---

## Fase de consolidación del motor

### 4. Multiplicidad de cabinas por tramo
Permitir varias cabinas por segmento y reforzar:
- lógica de despacho
- separación mínima
- control de vecinos
- gestión de cola operacional

### 5. Modelo de pasajeros más rico
Pasar de conteo agregado a uno de estos modelos:
- pasajeros por grupos
- pasajeros por perfiles
- pasajeros individuales

### 6. Mantenimiento y degradación realista
Incorporar:
- mantenimiento preventivo
- desgaste acumulativo
- fallas dependientes de historial
- probabilidad condicionada por salud del sistema

---

## Fase visual 2D

### 7. Sandbox 2D
Crear una representación visual 2D donde se vean:
- estaciones
- cabinas
- tramos
- animaciones más detalladas
- eventos visuales
- capas de información encendibles/apagables

### 8. Separar aún más lógica y render
Crear una capa intermedia de escena para que el motor entregue datos listos para:
- WPF 2D
- exportación a video o imágenes
- dashboards futuros

---

## Fase de simulación avanzada

### 9. Física más profunda
A futuro se puede incorporar:
- tensión del cable
- carga dinámica
- torque en sistemas motrices
- sensibilidad al viento por tramo
- respuesta ante frenado de emergencia severo

### 10. Protocolos de emergencia
Agregar:
- rescate
- evacuación
- tiempos de respuesta
- decisiones operativas automáticas
- cierre parcial o total del sistema

---

## Fase analítica e histórica

### 11. Casos históricos y replay
Con la base de datos integrada se podrá:
- guardar simulaciones
- reproducirlas desde cualquier punto
- comparar escenarios
- construir librería de incidentes

### 12. Métricas y reportes
Agregar:
- reportes por jornada
- histogramas de eventos
- comparación de perfiles diarios
- exportación de resultados

---

## Recomendación de orden

Si el proyecto debe avanzar semana a semana, el orden recomendado es:

1. persistencia SQLite desacoplada
2. configuración avanzada en UI
3. más pruebas automáticas
4. múltiples cabinas por tramo
5. mejora del modelo de pasajeros
6. sandbox 2D
7. física avanzada
8. replay histórico y analítica completa

---

## Criterio rector

No conviene saltar directo al 2D si el núcleo aún no está consolidado. La base actual ya hizo el paso correcto: primero un motor serio, luego una visualización más ambiciosa.
