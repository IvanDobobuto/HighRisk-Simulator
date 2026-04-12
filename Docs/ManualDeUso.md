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
Define el carácter general del día.

### Fecha simulada
Afecta:
- temporada
- fines de semana
- feriados
- presión turística

### Modo de presión
- **Operación realista**: la mayoría de las jornadas deben poder cerrar de forma controlada.
- **Entrenamiento intensificado**: aumenta presión operacional y frecuencia de contingencias.

### Densidad de cabinas
Permite probar:
- configuración base realista
- presión moderada
- estrés de separación

### Aceleración de tiempo
La versión actual permite:
- presets rápidos hasta **50x**
- ajuste manual continuo mediante slider

Esto se usa para validar jornadas largas sin modificar el motor.

---

## 4. Estructura de la interfaz

### Encabezado
Resume:
- estado operacional
- tiempo simulado
- riesgo actual
- narrativa del sistema

### Panel de operador colapsable
Agrupa:
- configuración de sesión
- aceleración de tiempo
- calibración de riesgos
- quick-triggers
- comandos de ejecución y exportación

### Centro visual
Muestra:
- telemetría de estaciones
- escena sandbox del teleférico
- clima actual
- HUD operacional

### Panel analítico lateral
Muestra:
- perfil del día
- presión operacional
- semilla derivada
- gráfica histórica de telemetría
- salida de exportación

### Pestañas inferiores
- **Eventos**
- **Cabinas**
- **Estaciones**

---

## 5. Controles principales

### Iniciar
Pone el motor en ejecución continua.

### Pausar
Detiene la corrida sin destruir el estado.

### Paso
Avanza un tick lógico y vuelve a pausa.

### Reiniciar
Reconstruye la jornada completa con la configuración actual.

### Simulacro completo
Crea una nueva corrida desde cero, la completa inmediatamente y exporta:
- PDF
- JSON

### Cerrar y exportar
Toma la corrida actual, la lleva al cierre y exporta:
- PDF
- JSON

### Exportar reporte
Genera el reporte de la corrida tal como va en ese momento.

---

## 6. Panel maestro de riesgos

El panel maestro permite ajustar la probabilidad relativa del sistema sin tocar código.

### Multiplicador global
Escala el entorno completo. Se usa para mover toda la jornada hacia un contexto más estable o más exigente.

### Sintonía fina disponible
Se pueden ajustar individualmente:
- tormentas
- vientos fuertes
- neblina
- desgaste mecánico
- falla mecánica de cabina
- cortes de energía
- picos de tensión

### Aplicar calibración
El botón **Aplicar calibración** envía la nueva matriz directamente al motor. La simulación no necesita reiniciarse para usarla.

---

## 7. Inyección de fallas en caliente

Primero se selecciona el objetivo:
- sistema completo / auto
- cabina específica

Luego se puede inyectar:
- tormenta
- viento fuerte
- neblina
- sobrecarga
- falla mecánica
- desgaste mecánico
- falla eléctrica
- pico de tensión
- parada de emergencia

Estas acciones sirven para:
- validar protocolos
- observar degradación
- entrenar respuestas reactivas
- construir demostraciones académicas específicas

La corrida no necesita estar en pausa para aceptar una inyección.

---

## 8. Telemetría de estaciones

La franja superior del bloque central muestra constantemente, por estación:
- cola de ascenso
- cola de descenso
- total visible de presión local
- regla de embarque

Esta información debe usarse para detectar:
- congestión creciente
- estaciones críticas
- desbalance entre sentidos

---

## 9. Interpretación de la escena visual

### Cabinas
La escena representa cabinas con sprite funcional y diagnóstico rápido.

### Estados visuales principales
- **verde**: operación normal
- **azul**: alerta moderada
- **naranja**: frenado o condición vigilada
- **rojo**: falla relevante
- **gris**: fuera de servicio

### Iconografía de estado
- `⚙` o `⌁`: problema o desgaste mecánico
- `⚡` o `ϟ`: problema eléctrico o sobretensión
- `■`: frenado activo o parada protectiva
- `×`: cabina fuera de servicio

### Clima
La escena superpone efectos según el estado actual:
- viento
- neblina
- nieve
- tormenta

---

## 10. Consola de mensajes

Cada acción importante del operador genera una notificación tipo toast.

Estas notificaciones se usan para confirmar:
- inicio o pausa
- aplicación de calibración
- inyección de falla
- exportación
- cierre de jornada

La ventaja es que confirman la acción sin ocultar la escena.

---

## 11. Reportes exportados

Cada exportación genera:

### PDF
Incluye:
- resumen ejecutivo
- métricas consolidadas
- calibración de riesgo aplicada
- tabla por estación
- tabla por cabina
- línea de tiempo de eventos
- conclusiones de la jornada

### JSON
Incluye respaldo técnico serializado del reporte para auditoría o futura integración histórica.

### Ubicación
Por defecto los artefactos se guardan dentro de la carpeta **Documentos/HighRiskSimulator/Exports/{fecha}** del usuario.

---

## 12. Validaciones manuales sugeridas

### Validación 1: control del tiempo
1. Ajustar el slider de velocidad a 50x.
2. Iniciar simulación.
3. Verificar que el tiempo avance acelerado sin perder coherencia de la escena ni de la telemetría.

### Validación 2: telemetría de estaciones
1. Ejecutar modo aleatorio.
2. Verificar que la franja superior muestre ascenso y descenso por estación.
3. Confirmar que Barinitas y Pico Espejo respetan sus restricciones operativas.

### Validación 3: quick-trigger sin pausa
1. Iniciar simulación.
2. Inyectar viento fuerte o neblina sin pausar.
3. Verificar actualización de escena, narrativa y eventos.

### Validación 4: calibración dinámica
1. Subir el multiplicador global y la probabilidad de tormenta.
2. Aplicar calibración.
3. Observar que la corrida mantiene continuidad y refleja la nueva presión.

### Validación 5: simulacro completo
1. Configurar semilla, fecha y presión.
2. Pulsar `Simulacro completo`.
3. Verificar generación de PDF y JSON.

---

## 13. Pruebas automáticas

Desde la raíz:

```bash
dotnet test
```

Las pruebas cubren principalmente:
- lista circular
- grafo
- heap
- pila
- reglas terminales de cola
- comportamiento base del motor

---

## 14. Lo que aún no hace esta versión

Todavía no incluye:
- conexión operativa real a MySQL
- replay histórico desde base de datos
- pasajeros individuales con identidad propia
- física industrial profunda de cable, tensión o torque
- protocolos avanzados de rescate y evacuación

La fase actual se centra en interfaz clara, control maestro de riesgo, realismo operativo y base sólida para la siguiente integración persistente.
