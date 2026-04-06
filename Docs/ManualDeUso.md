# Manual de uso

## 1. Requisitos previos

- .NET 8 SDK
- Visual Studio con soporte WPF
- restauración de paquetes NuGet habilitada

---

## 2. Apertura de la solución

1. Abrir `HighRiskSimulator.sln`.
2. Esperar restauración de paquetes.
3. Seleccionar `HighRiskSimulator` como proyecto de inicio.
4. Ejecutar con `F5`.

---

## 3. Parámetros principales de la corrida

### Modo de simulación
- **Aleatorio inteligente**: jornada no guionizada con clima, demanda y eventualidades contextuales.
- **Escenario específico**: reproduce casos preparados para validación.

### Semilla base
Define el “carácter general” del día.

### Fecha simulada
Afecta:
- temporada
- fines de semana
- feriados
- vacaciones
- presión turística

### Modo de presión
- **Operación realista**: la mayoría de las jornadas deben ser estables.
- **Entrenamiento intensificado**: sube la probabilidad de desvíos y presión operacional.

### Velocidad
- 1x
- 2x
- 3x

### Cabinas por sentido
Permite probar:
- configuración base realista
- presión moderada
- estrés de separación

---

## 4. Controles principales

### Iniciar
Pone el motor en ejecución continua.

### Pausar
Detiene la corrida sin destruir el estado.

### Paso
Avanza un tick lógico y vuelve a pausa.

### Reiniciar
Reconstruye la jornada completa con la configuración actual.

### Simulacro instantáneo
Crea una nueva corrida desde cero, la completa inmediatamente y exporta:
- PDF
- JSON

### Finalizar y exportar
Toma la corrida actual, la lleva al cierre y exporta:
- PDF
- JSON

### Exportar reporte actual
Genera reporte de la corrida tal como va en ese momento.

---

## 5. Inyección de fallas

Primero se selecciona el objetivo:
- sistema completo / auto
- cabina específica

Interpretación:
- para fallas de alcance global (`falla eléctrica`, `tormenta`, `parada de emergencia`), la opción **sistema completo** actúa sobre toda la operación
- para fallas focalizadas (`falla mecánica`, `sobrecarga`), la opción **auto** elige la cabina más cargada del snapshot actual

Luego se puede inyectar:
- falla mecánica
- falla eléctrica
- sobrecarga
- tormenta
- parada de emergencia

Estas acciones sirven para:
- validar protocolos
- observar degradación
- entrenar respuesta del sistema

---

## 6. Qué muestra la interfaz

## Encabezado
- estado operacional
- tiempo simulado
- clima
- pasajeros procesados
- ocupación media
- incidentes activos
- barra de riesgo

## Contexto de jornada
- perfil del día
- temporada detectada
- modo de presión
- visibilidad
- hielo
- semilla operacional derivada
- rutas de exportación

## Pestaña Operación
### Perfil operativo del sistema
Vista 1D del recorrido Mukumbarí con:
- estaciones
- colas por sentido
- reglas de embarque
- cabinas con color por estado

### Colores de cabinas
- verde: operación normal
- azul: detenida en estación
- naranja: alerta o frenado
- rojo: condición crítica o fuera de servicio

### Telemetría ScottPlot
Grafica:
- riesgo
- ocupación media
- presión climática

La ventana temporal se mueve sola; ya no es necesario arrastrar la vista manualmente.

## Pestaña Eventos y reportes
- log de eventos recientes
- descripción del flujo de exportación

## Pestaña Cabinas y estaciones
- tabla operativa de cabinas
- tabla de colas y reglas por estación

---

## 7. Interpretación de la semilla

La simulación ya no usa solo una semilla fija. Ahora hay dos niveles:

### Semilla base
Controla la identidad general del día.

### Semilla de variación operacional
Se genera internamente en cada corrida.

Resultado:
- dos corridas con la misma semilla base se parecen mucho
- pero no son copias exactas del mismo día

Eso hace al simulador más realista.

---

## 8. Reportes exportados

Cada exportación genera:

### PDF
Incluye:
- resumen ejecutivo
- estado final
- métricas consolidadas
- tabla por estación
- tabla por cabina
- línea de tiempo de eventos
- conclusiones de la jornada

### JSON
Incluye respaldo técnico serializado del reporte para auditoría o futura integración.

### Ubicación
Por defecto los artefactos se guardan dentro de la carpeta **Documentos/HighRiskSimulator/Exports/{fecha}** del usuario.

---

## 9. Validaciones manuales sugeridas

### Validación 1: cola realista
1. Ejecutar modo aleatorio.
2. Verificar que Barinitas no acumula cola de descenso.
3. Verificar que Pico Espejo no acumula cola de ascenso.
4. Verificar que estaciones intermedias solo crecen cuando llegan cabinas y descargan pasajeros.

### Validación 2: temporada alta
1. Seleccionar una fecha de agosto o diciembre.
2. Reiniciar.
3. Verificar incremento de presión turística y mayores colas en Barinitas.

### Validación 3: simulacro instantáneo
1. Configurar semilla, fecha y presión.
2. Pulsar `Simulacro instantáneo`.
3. Verificar generación de PDF y JSON.

### Validación 4: cierre acelerado desde una corrida activa
1. Iniciar simulación.
2. Dejar correr unos minutos.
3. Pulsar `Finalizar y exportar`.
4. Verificar que el reporte parte del estado actual y no de una corrida nueva.

### Validación 5: tormenta e impacto en tramos altos
1. Iniciar simulación.
2. Pulsar `Tormenta`.
3. Observar reducción de velocidad, más alerta y aumento del riesgo.

---

## 10. Pruebas automáticas

Desde la raíz:

```bash
dotnet test
```

Las pruebas cubren:
- lista circular
- grafo
- heap
- pila
- reglas terminales de cola
- comportamiento base del motor

---

## 11. Lo que aún no hace esta versión

Todavía no incluye:
- persistencia SQLite real conectada
- terreno 2D o sandbox visual completo
- pasajeros individuales con identidad propia
- física industrial profunda de cable, tensión o torque
- replay histórico desde base de datos

La fase actual se centra en realismo operativo y arquitectura correcta.
