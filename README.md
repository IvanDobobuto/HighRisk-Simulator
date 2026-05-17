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
### Entorno Windows (Command Prompt / CMD o PowerShell)
Para instalar las herramientas requeridas de manera automatizada sin necesidad de asistentes web, abra un terminal con privilegios de administrador y ejecute el gestor oficial `winget`:
1.	Instalar el SDK: Asegúrate de tener instalado el [.NET 8.0 SDK.](https://dotnet.microsoft.com/download/dotnet/8.0):
```cmd
winget install Microsoft.DotNet.SDK.8
```
2. Instalar CMake 3.20 o superior:
```cmd
winget install Kitware.CMake
```
3.	Clona y accede a el repositorio:
```cmd
git clone https://github.com/IvanDobobuto/HighRisk-Simulator
cd HighRisk-Simulator
```
4. Restauración y Construcción con CMake y dotnet CLI:
```cmd
dotnet restore
```
4.1 Configurar y compilar utilizando la infraestructura CMake
```cmd
cmake -B build -S .
cmake --build build --config Release
```
5.	Ejecutar directamente:
```cmd
dotnet run --project HighRiskSimulator/HighRiskSimulator.csproj -c Release
```
### Entorno GNU/Linux (Ubuntu / Debian / RHEL y Derivados)
1. Instalación de Dependencias vía Administrador de Paquetes
Actualice sus repositorios locales e instale el SDK de .NET 8 y CMake utilizando la terminal:
```bash
# Actualizar los índices de paquetes
sudo apt update && sudo apt upgrade -y

# Instalar los prerequisitos esenciales de CMake y compilación
sudo apt install -y build-essential cmake

# Instalar el SDK de .NET 8 (Repositorios oficiales de Microsoft)
sudo apt install -y dotnet-sdk-8.0
```
2. Clonación del Repositorio
```bash
git clone [https://github.com/IvanDobobuto/HighRisk-Simulator.git](https://github.com/IvanDobobuto/HighRisk-Simulator.git)
cd HighRisk-Simulator
```
3. Guión Avanzado de Limpieza, Resolución de Dependencias y Compilación (Excepción de Linux)
3.1 Limpiar builds y cache
```bash
rm -rf build
rm -rf HighRiskSimulator/bin HighRiskSimulator/obj
rm -rf HighRiskSimulator.Core/bin HighRiskSimulator.Core/obj
rm -rf HighRiskSimulator.Tests/bin HighRiskSimulator.Tests/obj
```
3.2 Limpiar cache NuGet
```bash
dotnet nuget locals all --clear
```
3.3 Restaurar paquetes
```bash
dotnet restore
```
3.4 Agregar libreria nativa correcta para Linux
```bash
dotnet add HighRiskSimulator/HighRiskSimulator.csproj package SkiaSharp.NativeAssets.Linux --version 3.119.0
```
3.5 Compilar proyecto utilizando la solución unificada
```bash
dotnet build HighRiskSimulator.sln -c Release
```
4. Ejecutar el proyecto
```bash
dotnet run --project HighRiskSimulator/HighRiskSimulator.csproj -c Release
```
### Entorno macOS (Terminal de Apple Architecture x64/ARM64)
1. Instalación de Dependencias vía Homebrew
Utilice el gestor estándar de paquetes de macOS (brew) para descargar e inicializar el entorno de construcción:
1.1 Actualizar las fórmulas de Homebrew
```bash
brew update
```
1.2 Instalar CMake (v3.20 o superior garantizado)
```bash
brew install cmake
```
1.3 Instalar .NET 8 SDK de manera aislada
```bash
brew install --cask dotnet-sdk
```
2. Clonación del Repositorio
```bash
git clone [https://github.com/IvanDobobuto/HighRisk-Simulator.git](https://github.com/IvanDobobuto/HighRisk-Simulator.git)
cd HighRisk-Simulator
```
3. Configuración
Restaurar los metadatos de paquetes NuGet:
```bash
dotnet restore
```
4. Generar los archivos de build nativos con CMake
```bash
cmake -B build -S .
cmake --build build --config Release
```
5. Lanzar el simulador dinámico en macOS bajo Avalonia Desktop
```bash
dotnet run --project HighRiskSimulator/HighRiskSimulator.csproj -c Release
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
