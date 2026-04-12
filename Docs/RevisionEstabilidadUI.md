# Revisión de estabilidad y rediseño UI/UX

## Objetivo de la revisión

La revisión de esta iteración se concentró en corregir el principal punto débil del proyecto: una interfaz con demasiada información compitiendo por la atención del operador.

La meta no fue embellecer por encima de la lógica. La meta fue reorganizar la presentación para que el usuario:
- entienda rápido dónde actuar
- pueda leer el estado sin esfuerzo
- no tenga que pausar para intervenir
- mantenga siempre visible la simulación

---

## Cambios aplicados

### 1. Panel lateral colapsable

Se sustituyó la dispersión de controles por un panel lateral único con `Expander`.

La decisión se adoptó porque:
- concentra la operación manual en un único punto
- reduce ruido visual en el área principal
- conserva todas las funciones existentes
- permite colapsar el panel sin perder acceso a la escena

Es mejor que repartir controles sobre toda la ventana porque disminuye carga cognitiva y mejora descubrimiento funcional.

---

### 2. Diseño adaptativo con `ViewBox` y grillas proporcionales

La escena pasó a una resolución lógica fija escalada por `ViewBox`, mientras la ventana se organiza con `Grid` proporcional.

La decisión se adoptó porque:
- preserva proporciones en pantallas pequeñas
- evita layouts quebrados en 800x600
- simplifica el dibujo de cabinas, estaciones y overlays
- reduce errores al redimensionar

Es más estable que recalcular geometría real para cada tamaño de ventana.

---

### 3. Scene-first layout

La interfaz se reorganizó en tres zonas:
- control
- simulación
- análisis

Esta separación es mejor que una sola superficie saturada porque cada zona resuelve una responsabilidad distinta.

---

### 4. Toasts no bloqueantes

Se añadieron confirmaciones breves tipo toast para:
- inicio
- pausa
- calibración de riesgo
- inyección de fallas
- exportación
- cierre

La decisión es mejor que usar cuadros modales porque no interrumpe la operación ni tapa la escena.

---

### 5. Telemetría constante de estaciones

Las colas de ascenso y descenso se hicieron visibles de forma persistente en la zona central superior.

La decisión se adoptó porque esa información es operativamente sensible y no debía quedar escondida en una pestaña secundaria.

---

### 6. Diagnóstico visual de cabinas

Se introdujeron estados visuales y iconografía rápida sobre las cabinas.

La decisión se adoptó porque el operador necesita detectar:
- falla mecánica
- anomalía eléctrica
- frenado protector
- retiro de servicio

sin depender exclusivamente del log textual.

---

### 7. No se incorporó un motor de juego externo

Se evaluó la conveniencia de una base visual más compleja, pero en esta fase se mantuvo WPF nativo con dibujo programático.

La decisión se adoptó porque:
- reduce dependencias externas
- protege estabilidad del proyecto actual
- conserva integración directa con el resto de la UI
- mantiene más control sobre comportamiento y escalado

Es la opción más óptima para mejorar fuerte la presentación sin poner en riesgo el sistema.

---

## Impacto sobre estabilidad

Los cambios de UI se aplicaron preservando la lógica existente.

Se priorizó:
- no eliminar funciones
- no duplicar lógica del motor en la vista
- no introducir dependencias que alteren la ejecución de la simulación
- no convertir el renderizado visual en un nuevo punto de fallo del sistema

---

## Archivos intervenidos

- `HighRiskSimulator/ViewModels/MainViewModel.cs`
- `HighRiskSimulator/Views/MainWindow.xaml`
- `HighRiskSimulator/Views/MainWindow.xaml.cs`
- `HighRiskSimulator/App.xaml`

---

## Conclusión

La interfaz actual ya no trabaja contra el usuario. Ahora ordena la operación, deja visible lo importante, permite intervenir sin detener la corrida y convierte la simulación en una experiencia mucho más clara, controlable y defendible académicamente.
