# Realismo de simulación adoptado en esta fase

## Objetivo de este documento

Explicar cómo se ajustó el simulador para que el comportamiento se acerque más a una jornada real y deje de “forzar” incidentes o colas artificiales.

---

## 1. Principio rector

El sistema debe poder producir jornadas donde:
- no ocurra nada grave
- ocurran desviaciones leves
- aparezcan fallas relevantes solo cuando el contexto las favorece

Eso es más realista que obligar a que siempre ocurra una catástrofe.

---

## 2. Colas realistas por estación

### Estación base
Barinitas recibe la mayor parte de la demanda exógena. Por ello:
- sí puede crecer la cola de ascenso
- no debe crecer una cola de descenso

### Estaciones intermedias
No generan grandes colas por sí solas. Su demanda nace sobre todo cuando una cabina descarga pasajeros y esos pasajeros:
- deciden continuar la ruta
- permanecen un tiempo en la estación
- retornan después

### Estación final
Pico Espejo no debe generar ascenso adicional, pero sí puede generar retorno descendente porque muchos pasajeros culminan allí su visita y luego bajan.

---

## 3. Temporadas y calendario venezolano

La demanda se ajusta por:
- fines de semana
- agosto
- Navidad y Año Nuevo
- Carnaval
- Semana Santa
- algunos feriados nacionales

Esto hace que el flujo de pasajeros no sea uniforme durante todo el año.

---

## 4. Dos modos de presión

### Operación realista
Favorece jornadas mayormente estables. Los incidentes severos son menos frecuentes.

### Entrenamiento intensificado
Aumenta la presión del entorno para practicar protocolos y observar más contingencias sin reescribir el modelo.

---

## 5. Misma semilla, día parecido pero no idéntico

El simulador mezcla:
- una semilla base
- una variación operacional interna

Con esto se evita el efecto poco realista de repetir exactamente el mismo día cuando la intención del sistema es simular jornadas plausibles, no reproducir clones perfectos.

---

## 6. Árbol causal interno

El árbol causal guarda memoria del contexto del día. Ejemplos de uso:
- un periodo prolongado de viento fuerte puede elevar presión para fallas de frenado o separación
- una falla previa puede aumentar la vulnerabilidad ante otra desviación
- un entorno frío con mayor humedad puede volver más sensible el sistema al hielo

El árbol no se muestra en el reporte final, pero sí influye internamente en el cálculo de la presión causal.

---

## 7. Clima por severidad y altura

El clima no solo cambia una etiqueta visual. Impacta:
- velocidad operativa
- visibilidad
- riesgo agregado
- probabilidad de hielo según altitud
- presión operacional de tramos altos

Por eso los tramos superiores son más sensibles en tormenta, nieve o frío intenso.

---

## 8. Por qué no se modelaron pasajeros individuales todavía

Se optó por grupos agregados con decisiones diferidas porque:
- mantiene costo computacional moderado
- permite colas realistas
- permite explicación académica clara
- deja espacio para pasar a un modelo origen-destino más fino después

Es un equilibrio entre realismo y complejidad razonable para esta fase.

---

## 9. Simulacro instantáneo

El simulacro instantáneo no “inventa” un log prefabricado. Lo que hace es ejecutar la jornada completa en memoria sin esperar el temporizador visual y luego consolidar:
- eventos
- métricas
- tablas por cabina y estación
- conclusiones de la corrida

---

## Conclusión

El realismo de esta fase no depende de gráficos complejos, sino de reglas operativas más creíbles:
- colas correctas
- clima con impacto real
- temporadas
- contexto acumulado
- incidentes no forzados
- cierre de jornada exportable

Eso deja una base muy superior para avanzar a 2D sin perder coherencia estadística.
