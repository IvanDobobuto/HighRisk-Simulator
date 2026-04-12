# Roadmap sugerido

## Objetivo general

Continuar la evolución del simulador desde una base estadística-operativa robusta, visualmente clara y preparada para persistencia histórica, sin comprometer la coherencia del núcleo actual.

---

## Fase siguiente inmediata

### 1. Persistencia MySQL real

Implementar una infraestructura concreta que cumpla:
- `ISimulationSnapshotRepository`
- `ISimulationRunRepository`

con soporte para:
- historial de corridas
- snapshots por jornada
- reportes y eventos
- telemetría histórica
- filtros por fecha, escenario y calibración

### 2. Replay histórico

Con base de datos integrada, permitir:
- abrir una corrida pasada
- reproducirla desde cualquier snapshot
- comparar una línea temporal contra otra

### 3. Más pruebas automáticas

Extender cobertura para:
- separación segura con múltiples cabinas
- calibración dinámica de riesgo
- impacto de neblina y viento fuerte
- eventos de desgaste mecánico y picos de tensión
- consistencia del cierre acelerado y del reporte exportado

---

## Fase de consolidación del motor

### 4. Modelo origen-destino más rico

Pasar del modelo actual de grupos agregados a uno más fino con:
- grupos por origen-destino
- perfiles de visita por estación
- motivos de viaje

### 5. Mantenimiento preventivo y correctivo

Agregar:
- calendario de mantenimiento
- degradación acumulativa entre jornadas
- reparación y reincorporación con tiempos operativos
- penalización por diferir mantenimiento

### 6. Protocolos operativos avanzados

Incorporar:
- cierre parcial por tramo
- evacuación y rescate
- respuesta automática por severidad
- restricciones por viento, hielo o visibilidad

---

## Fase visual siguiente

### 7. Escena sandbox enriquecida

Profundizar la capa visual con:
- más sprites por estación
- partículas climáticas más ricas
- iluminación simple por condición ambiental
- selección de capas informativas conmutables

### 8. Terreno y assets más detallados

Preparar soporte para:
- texturas básicas por estación
- relieve de montaña más granular
- animaciones adicionales de cabina
- capa de assets desacoplada del motor

---

## Fase analítica e histórica

### 9. Dashboards comparativos

Agregar:
- histogramas de incidentes
- comparación por temporada
- métricas por tramo
- ocupación acumulada por estación
- comparación de perfiles de calibración de riesgo

### 10. Biblioteca de escenarios históricos

Permitir guardar y reutilizar:
- simulaciones cerradas
- perfiles de entrenamiento
- calibraciones académicas predefinidas
- escenarios comparativos para defensa y exposición

---

## Recomendación de orden

1. MySQL real
2. pruebas ampliadas
3. replay histórico
4. mantenimiento y protocolos
5. dashboards comparativos
6. enriquecimiento visual adicional

---

## Criterio rector

La secuencia correcta sigue siendo:
1. consolidar núcleo y persistencia
2. enriquecer modelo operativo
3. expandir análisis y visualización

Así la siguiente capa visual y la base histórica nacerán apoyadas en un motor estable, no sobre una lógica frágil.
