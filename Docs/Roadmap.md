# Roadmap sugerido

## Objetivo general

Continuar la evolución del simulador desde una base estadística-operativa robusta hacia una plataforma visual 2D, persistente y analítica, sin comprometer la coherencia del núcleo actual.

---

## Fase siguiente inmediata

### 1. Persistencia SQLite real
Implementar una infraestructura concreta que cumpla:
- `ISimulationSnapshotRepository`
- `ISimulationRunRepository`

con soporte para:
- historial de corridas
- snapshots por jornada
- reportes y eventos
- filtros por fecha y escenario

### 2. Reportes comparativos
Agregar comparación entre corridas:
- misma semilla base
- distintas fechas
- modos de presión distintos
- temporadas distintas

### 3. Más pruebas automáticas
Extender cobertura para:
- separación segura con múltiples cabinas
- impacto del clima por tramo
- transición entre estados Normal / Alerta / Crítico
- validez de reportes exportados
- fast-forward desde estado parcial

---

## Fase de consolidación del motor

### 4. Modelo origen-destino más rico
Pasar del modelo actual de grupos agregados a un modelo más fino con:
- grupos por origen-destino
- perfiles de visita por estación
- motivos de viaje

### 5. Mantenimiento preventivo y correctivo
Agregar:
- calendario de mantenimiento
- degradación acumulativa real por jornada
- reparación y reincorporación con tiempos operativos
- penalización por diferir mantenimiento

### 6. Protocolos operativos avanzados
Incorporar:
- cierre parcial por tramo
- evacuación y rescate
- respuesta automática por severidad
- restricciones por viento o hielo en altura

---

## Fase visual 2D

### 7. Sandbox 2D
Construir una escena visual con:
- estaciones
- terreno simplificado
- tramos y cables
- cabinas animadas
- partículas climáticas
- capas conmutables de información

### 8. Soporte futuro para texturas y terreno
Preparar una capa de escena para:
- texturas básicas
- relieve tipo sandbox
- assets simples de estaciones y vagones
- partículas de nieve, viento o tormenta

---

## Fase analítica e histórica

### 9. Replay histórico
Con base de datos integrada, permitir:
- abrir una corrida pasada
- reproducirla desde cualquier snapshot
- comparar la línea temporal con otra corrida

### 10. Dashboards y métricas agregadas
Agregar:
- histogramas de incidentes
- comparación por temporada
- métricas por tramo
- ocupación acumulada por estación

---

## Recomendación de orden

1. SQLite real
2. pruebas ampliadas
3. reportes comparativos
4. mantenimiento y protocolos
5. sandbox 2D
6. replay histórico
7. texturas y terreno

---

## Criterio rector

La fase correcta sigue siendo:
1. consolidar núcleo y persistencia
2. enriquecer modelo operativo
3. expandir visualización

Así el 2D será una consecuencia natural del motor, no un maquillaje encima de una lógica frágil.
