# Manual de uso

## 1. Requisitos previos

- .NET 8 SDK
- Visual Studio 2026 recomendado
- Workload de escritorio para WPF

---

## 2. Apertura de la solución

1. Abrir `HighRiskSimulator.sln`.
2. Esperar restauración de paquetes NuGet.
3. Seleccionar `HighRiskSimulator` como proyecto de inicio.
4. Ejecutar con `F5`.

---

## 3. Modos de simulación

## Aleatorio inteligente
Usa una semilla reproducible para combinar:
- demanda de pasajeros
- clima
- fallas
- degradación operativa
- reglas de seguridad

Si repites la misma semilla y opciones, la simulación debería repetir el mismo comportamiento lógico.

## Escenario específico
Inyecta incidentes programados para reproducir casos concretos.

Escenarios incluidos:
- Sobrecarga en temporada alta
- Falla eléctrica general
- Tormenta andina en cotas altas

---

## 4. Controles principales

### Iniciar
Pone el motor en ejecución continua.

### Pausar
Detiene la evolución temporal sin destruir el estado.

### Paso
Avanza exactamente un tick lógico y vuelve a pausa.

### Reiniciar
Reconstruye por completo el escenario con la semilla actual.

---

## 5. Qué muestra la interfaz

## Encabezado superior
- estado operacional
- tiempo simulado
- clima actual
- pasajeros procesados

## Panel de control
- modo de simulación
- escenario guionizado
- semilla
- perfil operativo activo

## Perfil operativo del sistema
Es la visualización 1D del recorrido.

### Colores de cabina
- verde: operación normal
- azul: cabina detenida en estación
- naranja: frenado o alerta
- rojo: falla crítica o fuera de servicio

## Telemetría con ScottPlot
Grafica en tiempo real:
- riesgo agregado
- ocupación media
- presión climática

## Log de eventos
Muestra eventos relevantes con:
- título
- severidad
- tiempo del evento
- fuente
- tipo

## Tablas laterales
- estado de cabinas
- colas de estaciones

---

## 6. Validaciones manuales sugeridas

## Validación 1: reproducibilidad
1. Ejecutar modo aleatorio con una semilla fija.
2. Observar eventos principales.
3. Reiniciar con la misma semilla.
4. Verificar que el comportamiento general se repite.

## Validación 2: sobrecarga
1. Seleccionar el escenario `Sobrecarga en temporada alta`.
2. Iniciar simulación.
3. Verificar evento de sobrecarga y aumento del riesgo.

## Validación 3: falla eléctrica
1. Seleccionar `Falla eléctrica general`.
2. Iniciar simulación.
3. Verificar frenado de emergencia y degradación del sistema.

## Validación 4: clima extremo
1. Seleccionar `Tormenta andina en cotas altas`.
2. Iniciar simulación.
3. Verificar reducción de velocidad, presión climática y eventos asociados.

---

## 7. Ejecución de pruebas

Desde la raíz de la solución:

```bash
dotnet test
```

Estas pruebas validan la base crítica del proyecto.

---

## 8. Qué no hace todavía esta versión

Aún no incluye:
- entorno 2D sandbox
- persistencia SQLite real
- pasajeros individuales
- física profunda de cable, tensión o torque
- rescate/evacuación detallados
- múltiples cabinas por tramo en operación avanzada

Eso no es un error: esta fase busca primero un núcleo de simulación correcto.
