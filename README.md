# HighRisk Simulator

Simulador estadístico-operativo del teleférico Mukumbarí construido sobre **.NET 8 + WPF**, rediseñado para entregar una jornada más realista, una interfaz mucho más clara y una base sólida para persistencia futura en **MySQL**.

La iteración actual consolida:

- motor por ticks con **delta time escalable de 1x a 50x**
- **múltiples cabinas por tramo** configurables
- **panel de operador colapsable** para centralizar controles y no saturar la escena
- **sandbox visual 2D** escalable con `ViewBox` y `Canvas` lógico fijo
- **telemetría constante de estaciones** con colas de ascenso y descenso visibles
- **sistema de notificaciones tipo toast** para confirmar acciones sin tapar la simulación
- **panel maestro de riesgos** con multiplicador global y sintonía fina por categoría
- **inyección de fallas en caliente** sin necesidad de pausar la corrida
- clima enriquecido con **tormenta, vientos fuertes, neblina y nieve**
- eventos adicionales de **desgaste mecánico** y **picos de tensión**
- **sprites 2D funcionales** para cabinas, estaciones, alertas y estados de frenado
- telemetría temporal con **ScottPlot**
- exportación estructurada a **PDF y JSON**
- contratos listos para futura persistencia histórica en **MySQL**

La prioridad de esta versión fue elevar la calidad de la experiencia completa: mejor lectura, mejor control, más realismo, más trazabilidad y más argumentos técnicos para justificar cada decisión del proyecto.

---

## Estado actual del proyecto

### Núcleo funcional consolidado
- Motor principal con tiempo fijo por ticks y escalamiento hasta **50x**.
- Base física y lógica de movimiento para cabinas por tramo.
- Modelo dirigido de estaciones y segmentos usando **grafo manual**.
- Gestión cíclica de cabinas con **lista circular doblemente enlazada**.
- Configuración de **múltiples cabinas por sentido y por tramo**.
- Sistema de eventos aleatorios, guionizados y forzados en tiempo real.
- Exportación completa de reportes en **PDF** y **JSON**.

### UI/UX consolidada
- Encabezado orientado a lectura rápida del estado actual.
- **Panel lateral colapsable** con controles agrupados por responsabilidad.
- Escena principal tipo sandbox con cabinas, estaciones, clima, diagnósticos y colas.
- Telemetría visible de estaciones sin tener que cambiar de pestaña.
- Controles de tiempo con presets y ajuste manual continuo.
- Quick-triggers para intervención operativa sin interrumpir la simulación.
- Confirmaciones no intrusivas mediante **toasts**.

### Base analítica y de persistencia preparada
- Reporte de corrida con resumen de calibración de riesgo.
- Contratos desacoplados para snapshots y reportes históricos.
- Documento privado y contratos listos para futura integración con **MySQL**.

---

## Qué cambió respecto a la iteración anterior

### 1) La interfaz dejó de competir contra el usuario
Se eliminó la sensación de desorden reorganizando la operación en tres zonas claras:
- control maestro lateral
- escena visual central
- panel analítico y telemétrico lateral

La decisión mejora lectura, reduce fricción y permite que el usuario sepa desde el primer minuto dónde configurar, dónde observar y dónde diagnosticar.

### 2) El tiempo ya no limita las pruebas académicas
Ahora el simulador puede acelerarse hasta **50x** y además permite ajuste manual continuo. Esto sirve para:
- recorrer jornadas largas rápidamente
- validar estabilidad del motor
- comparar resultados sin esperar una ejecución lenta

### 3) El riesgo ahora se puede calibrar de forma explícita
La corrida dejó de depender solo de presión global. Ahora existe un perfil maestro con:
- multiplicador global
- tormentas
- vientos fuertes
- neblina
- desgaste mecánico
- falla mecánica de cabina
- cortes eléctricos
- picos de tensión

Esto permite construir escenarios académicos muy específicos sin romper el motor ni duplicar lógica.

### 4) La escena visual ya no es solo decorativa
La vista principal ahora representa:
- estaciones detalladas
- cableado visual
- cabinas con estados diferenciados
- indicadores rápidos de fallas
- colas por estación
- superposiciones de clima

La escena está diseñada para reforzar lectura operacional, no para distraer.

### 5) La persistencia futura dejó de ser abstracta
La arquitectura ya no se queda en “algún día se conectará una base”. Ahora existe una base contractual concreta para **MySQL**, con documentación privada de integración y lineamientos de esquema para guardar:
- corridas
- snapshots
- eventos
- telemetría
- calibraciones

---

## Arquitectura actual

```text
HighRisk-Simulator/
|-- HighRiskSimulator.sln
|-- Directory.Build.props
|-- README.md
|-- Docs/
|   |-- Architecture.md
|   |-- DataStructuresJustification.md
|   |-- ManualDeUso.md
|   |-- RevisionEstabilidadUI.md
|   |-- Roadmap.md
|   |-- SimulationRealism.md
|   |-- VerificacionTecnica.md
|   `-- Privado/
|       `-- IntegracionMySQL.md
|-- HighRiskSimulator.Core/
|   |-- Domain/
|   |-- DataStructures/
|   |-- Simulation/
|   |-- Factories/
|   `-- Persistence/
|-- HighRiskSimulator/
|   |-- Views/
|   |-- ViewModels/
|   |-- Services/
|   |-- Models/
|   `-- Helpers/
`-- HighRiskSimulator.Tests/
    |-- DataStructures/
    `-- Simulation/
```

---

## Decisiones académicas principales

### Lista circular doblemente enlazada
Se usa porque la operación de cabinas por tramo es cíclica. La estructura expresa mejor la relación **siguiente / anterior** que una colección lineal.

### Grafo dirigido
Se usa porque el sistema es una red de estaciones y segmentos. Aunque Mukumbarí sea lineal en esta etapa, el modelo queda listo para crecer sin rediseñar la base.

### Heap mínimo manual
Se usa para gestionar acciones futuras con prioridad temporal:
- incidentes programados
- retornos diferidos
- transferencias internas
- contingencias encadenadas

### Pila enlazada manual
Se usa para el historial reciente de eventos con acceso **LIFO**, adecuado para UI, narrativa y trazabilidad rápida.

### Árbol causal interno
Se eligió porque permite conservar memoria contextual de la jornada y usarla para modular cascadas de riesgo.

### Perfil maestro de calibración
Se encapsuló en `SimulationRiskTuningProfile` para concentrar la configuración del riesgo en un único contrato coherente. Esta decisión es mejor que distribuir sliders y multiplicadores sueltos porque evita dispersión, simplifica serialización y deja lista la persistencia de calibraciones.

### Sandbox visual con `Canvas` + `ViewBox`
Se eligió esta combinación en lugar de un motor gráfico externo porque:
- conserva estabilidad en WPF
- reduce dependencias y superficie de fallo
- permite resolución lógica fija y escalado proporcional
- mantiene el motor académico separado de la capa visual

### Preparación para MySQL en lugar de acoplar SQL al motor
Se dejaron contratos y settings desacoplados para que la base de datos futura no contamine la lógica de simulación. El motor produce snapshots y reportes; la infraestructura persistente será responsable de almacenarlos.

---

## Qué muestra la interfaz actual

### Encabezado
- estado operacional
- tiempo simulado
- riesgo agregado
- narrativa actual de la jornada

### Panel lateral de operador
- modo de simulación
- escenario
- fecha simulada
- presión operacional
- densidad de cabinas
- velocidad por preset y control manual
- calibración completa de riesgos
- quick-triggers operativos
- controles de ejecución y exportación

### Centro visual
- estaciones con colas por sentido
- escena sandbox con cableado, entorno y cabinas
- estados visuales de cabina
- indicadores rápidos de falla
- superposición de clima

### Lateral analítico
- ficha operativa de jornada
- gráfica histórica de telemetría
- salida de exportación y trazabilidad

### Pestañas inferiores
- **Eventos**
- **Cabinas**
- **Estaciones**

---

## Cómo ejecutar el proyecto

### Requisitos
- **.NET 8 SDK**
- Visual Studio con workload **.NET desktop development**

### Pasos
1. Abrir `HighRiskSimulator.sln`.
2. Restaurar paquetes NuGet.
3. Establecer `HighRiskSimulator` como proyecto de inicio.
4. Ejecutar con `F5`.

### Pruebas
Desde la raíz de la solución:

```bash
dotnet test
```

---

## Dependencias relevantes

### En uso ahora
- **ScottPlot.WPF** para telemetría temporal.
- **QuestPDF** para reporte multipágina.

### Deliberadamente no integradas en esta fase
- motores de juego 2D externos
- frameworks de física ajenos al núcleo WPF
- acceso directo a MySQL desde la capa de simulación

La decisión fue priorizar control, estabilidad y justificación académica antes que complejidad ornamental.

---

## Documentación del proyecto

- `Docs/Architecture.md` - organización por capas y justificación de la arquitectura.
- `Docs/DataStructuresJustification.md` - defensa formal de estructuras y contratos seleccionados.
- `Docs/ManualDeUso.md` - guía de operación de la interfaz y de los flujos académicos.
- `Docs/RevisionEstabilidadUI.md` - decisiones de estabilidad y UX aplicadas.
- `Docs/Roadmap.md` - evolución sugerida desde esta base.
- `Docs/SimulationRealism.md` - criterios de realismo adoptados.
- `Docs/VerificacionTecnica.md` - revisión técnica y checklist de validación.
- `Docs/Privado/IntegracionMySQL.md` - guía privada para integrar la base de datos histórica.
