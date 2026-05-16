# HighRisk Simulator
>Sistema de Apoyo a la Decisión (DSS) y Modelado Estocástico

Simulador estadístico-operativo del teleférico Mukumbarí construido sobre **.NET 8 + Avalonia UI**, rediseñado para entregar una jornada más realista, una interfaz mucho más clara y una base sólida. Esta aplicación esta desarollada en C#, con ScottPlot.Avalonia para telemetria temporal y QuestPDF para reporte multipáginas como dependencias.

La operación en alta montaña es inherentemente volátil. Los cambios climáticos abruptos, los picos de demanda y la fatiga de componentes eléctricos/mecánicos generan un escenario de toma de decisiones crítico. Probar cambios en la velocidad de despacho o protocolos de emergencia directamente en la infraestructura real es inviable por razones de seguridad y presupuesto.

El simulador actúa como un gemelo digital funcional que permite:

- **Validación de Protocolos**: Evaluar la seguridad operativa ante vientos racheados, tormentas y visibilidad nula.
- **Análisis de Resiliencia**: Inyectar fallas críticas en caliente (picos de tensión, desgaste mecánico) para medir tiempos de respuesta y recuperación.
- **Optimización de Flujo:** Predecir y mitigar cuellos de botella en estaciones mediante el análisis de colas y telemetría en tiempo real.

# Características
- **Motor de Simulación:** Arquitectura basada en ticks con Delta Time escalable (1x-50x). Gestión de cabinas mediante listas circulares doblemente enlazadas y modelado de tramos con grafos.
- **Gestión de Riesgos:** Panel maestro para calibrar eventos estocásticos (fallas mecánicas, cortes eléctricos y picos de tensión) e inyección de fallas en caliente.
- **Simulador Climático:** Modelo dinámico de tormentas, vientos racheados, neblina y nieve con impacto en la seguridad operativa.
-  **HMI (Interfaz):** Diseño optimizado en zonas (Control, Escena, Analítica) con panel colapsable, sandbox 2D escalable y notificaciones no intrusivas.
- **Telemetría y Datos:** Gráficos en tiempo real con ScottPlot y exportación de reportes diarios o por lotes a PDF y JSON.
- **Sprites y tutorial:** Menú inicial, feature tour y escena con sprites día/noche, clima animado y assets organizados en `assets/`.

# Instalación y Ejecución
**Compatibilidad:** La interfaz fue migrada a Avalonia UI sobre .NET 8, por lo que el proyecto queda preparado para ejecutarse en Windows, GNU/Linux y macOS con el runtime correspondiente.

## Método rápido (Usuario)
Si solo deseas probar el simulador sin configurar el entorno de desarrollo ve al apartado de [Releases](https://github.com/IvanDobobuto/HighRisk-Simulator/releases) en este repositorio, descarga la última versión disponible para tu sistema operativo, descomprime el contenido en una carpeta local y ejecuta el binario correspondiente.

## Compilación con Visual Studio
1.	Clona el repositorio:
```bash
git clone https://github.com/IvanDobobuto/HighRisk-Simulator
```
2.	Abre el archivo de solución .sln o el proyecto .csproj con Visual Studio 2022.
3.  Asegúrate de tener instalado el SDK de .NET 8 y las cargas necesarias para restaurar paquetes NuGet.
4.  Visual Studio restaurará automáticamente los paquetes de Avalonia UI, ScottPlot y QuestPDF.
5.  Presiona F5 para compilar y lanzar la aplicación.

## Compilación vía CLI
1.	Instalar el SDK: Asegúrate de tener instalado el [.NET 8.0 SDK.](https://dotnet.microsoft.com/download/dotnet/8.0)
2.	Clona y accede a el repositorio:
```bash
git clone https://github.com/IvanDobobuto/HighRisk-Simulator
cd HighRisk-Simulator
```
3.	Restaurar dependencias:
```bash
dotnet restore
```
4.	Ejecutar directamente:
```bash
dotnet run --project HighRiskSimulator
```
# Dependencias
El simulador utiliza un stack moderno de .NET enfocado en alto rendimiento y visualización de datos:
- **.NET 8.0 Avalonia UI:** Framework base para la interfaz de usuario y el motor de ejecución.
- **ScottPlot.Avalonia:** Motor de telemetría de alto desempeño para el renderizado de gráficos en tiempo real.
- **QuestPDF:** Motor de maquetación para la generación de reportes técnicos detallados.

---

## Arquitectura actual

```text
HighRisk-Simulator/
|-- HighRiskSimulator.sln
|-- Directory.Build.props
|-- README.md
|-- Docs/
|   |-- Architecture.md
|   `-- DataStructures.md
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
# Licencia y Uso
Este proyecto se distribuye bajo la Licencia MIT. Eres libre de usar, modificar y distribuir el código, siempre que se mantenga el reconocimiento de la autoría original.

- Uso de QuestPDF: Este proyecto utiliza la Community License de QuestPDF. Para fines académicos y proyectos de código abierto, el uso es gratuito. El cumplimiento de esta licencia ya está integrado en el código fuente.

# Créditos
Desarrollado como parte del currículo de la asignación programación 3 en el semestre A2026 de la facultad de Ingeniería de Sistemas en la Universidad de los Andes (ULA).
Equipo de desarrollo: 
- José Guillermo Quintero Devera
- Ivan Dobobuto
- Roni Vicic

---

# Documentación del proyecto

La documentación se redujo a lo exigido para defensa académica:

- `Docs/Architecture.md` - capas del sistema, flujo de ejecución y decisiones de arquitectura.
- `Docs/DataStructures.md` - justificación de estructuras de datos y uso de snapshots/colecciones.
