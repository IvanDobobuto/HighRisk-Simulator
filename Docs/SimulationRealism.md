# Realismo de simulación adoptado en esta fase

## Objetivo de este documento

Explicar cómo se ajustó el simulador para que el comportamiento se acerque más a una jornada real y deje de depender de contingencias artificiales o de una interfaz poco legible.

---

## 1. Principio rector

El sistema debe poder producir jornadas donde:
- no ocurra nada grave
- ocurran desviaciones leves
- aparezcan fallas relevantes solo cuando el contexto las favorece
- el operador pueda intervenir sin romper continuidad temporal

Eso es más realista que obligar a que siempre ocurra una catástrofe o que la simulación solo pueda alterarse desde pausa.

---

## 2. Colas realistas por estación

### Estación base
Barinitas recibe la mayor parte de la demanda exógena. Por ello:
- sí puede crecer la cola de ascenso
- no debe crecer una cola de descenso artificial

### Estaciones intermedias
Su presión nace principalmente cuando una cabina descarga pasajeros y esos pasajeros:
- continúan la ruta
- permanecen un tiempo en la estación
- retornan después

### Estación final
Pico Espejo no genera ascenso adicional, pero sí puede generar retorno descendente.

---

## 3. Clima con impacto real

El clima ya no es solo una etiqueta visual. Impacta:
- velocidad operativa
- visibilidad
- presión de riesgo
- probabilidad de hielo
- probabilidad relativa de ciertos eventos

La fase actual diferencia explícitamente:
- frío
- viento fuerte
- neblina
- nieve
- tormenta

La neblina se incorporó porque en entrenamiento real la pérdida de visibilidad modifica lectura operacional aunque no exista necesariamente una tormenta severa.

---

## 4. Dos niveles de variación

El simulador mezcla:
- una semilla base
- una variación operacional interna

Con esto se evita repetir exactamente el mismo día cuando la intención es simular jornadas plausibles, no clones perfectos.

---

## 5. Árbol causal interno

El árbol causal guarda memoria del contexto del día. Ejemplos:
- un periodo prolongado de viento fuerte puede elevar presión para separación o frenado
- un desgaste previo puede hacer más probable una falla posterior
- un entorno de visibilidad degradada puede aumentar la tensión operativa general

Esto hace que el sistema recuerde lo ocurrido antes de evaluar la siguiente desviación.

---

## 6. Calibración maestra de riesgo

Se agregó un panel maestro de riesgo para construir entornos específicos sin tocar código.

### Multiplicador global
Escala la presión general del sistema.

### Sintonía fina
Permite ajustar individualmente:
- tormenta
- viento fuerte
- neblina
- desgaste mecánico
- falla mecánica de cabina
- corte de energía
- pico de tensión

La decisión es más realista porque no todas las jornadas de estrés se intensifican por la misma causa.

---

## 7. Nuevos eventos de degradación

### Desgaste mecánico
Se modeló para representar una degradación previa a la falla dura. Esto mejora realismo porque en operación real muchas fallas no nacen de cero; suelen venir precedidas por señales de fatiga o pérdida de eficiencia.

### Pico de tensión
Se modeló para representar una contingencia eléctrica breve que puede quedar contenida o escalar a falla. Esto es más correcto que tratar todo incidente eléctrico como corte total inmediato.

---

## 8. Intervención en caliente

La posibilidad de inyectar clima o fallas sin pausar la simulación acerca el comportamiento a una operación real, donde la contingencia aparece mientras el sistema está en marcha.

Esto mejora la calidad del entrenamiento porque el operador debe leer y reaccionar en continuidad temporal.

---

## 9. Escena visual con intención operativa

La nueva escena 2D no se diseñó solo para verse mejor. Se diseñó para reforzar lectura de:
- estado de cabinas
- colas de estaciones
- clima activo
- diagnósticos rápidos

Eso mejora realismo operativo porque el usuario percibe la jornada como un sistema vivo, no como tablas aisladas.

---

## 10. Por qué no se modelaron pasajeros individuales todavía

Se optó por grupos agregados con decisiones diferidas porque:
- mantiene costo computacional razonable
- conserva claridad académica
- permite colas realistas
- deja espacio para un modelo origen-destino más fino en la siguiente fase

Es el equilibrio más correcto entre realismo y complejidad para el estado actual del proyecto.

---

## 11. Simulacro completo

El simulacro completo no inventa un log prefabricado. Lo que hace es ejecutar la jornada en memoria sin esperar el temporizador visual y luego consolidar:
- eventos
- métricas
- tablas por cabina y estación
- calibración aplicada
- conclusiones de la corrida

---

## Conclusión

El realismo de esta fase se sostiene en reglas operativas más creíbles y más controlables:
- colas correctas
- clima con impacto real
- desgaste previo a la falla
- distinción entre pico de tensión y corte total
- contexto acumulado
- intervención en caliente
- visualización alineada con diagnóstico

Eso deja una base mucho más sólida para una defensa académica fuerte y para la futura persistencia histórica.
