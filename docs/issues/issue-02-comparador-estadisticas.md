## Mejora

**Problema:** con 335 operadores, el catálogo no ayuda a decidir a quién desplegar. Faltan dos cosas:

1. Ver cómo cambia el grupo visible (promedios y distribución) al buscar o filtrar.
2. Enfrentar a dos operadores atributo por atributo.

## Propuesta

### Estadísticas dinámicas en tiempo real
- Panel «Inteligencia del grupo visible» con el total y el promedio de HP, ATK, DEF y coste de DP.
- Distribución porcentual por clase (de mayor a menor) y por rareza (siempre de 6★ a 1★).
- Se recalcula con cada búsqueda, filtro u orden: `computeStats()` en `script.js` y `DynamicStatsSummaryDto` en `GET /api/operators/stats`.

### Comparador de dos operadores
- Modal `<dialog>` que se abre desde la barra o desde el botón «Comparar» de cada tarjeta (el primer clic llena A y el segundo llena B).
- Compara 7 atributos: HP, ATK, DEF, RES, coste, bloqueo e intervalo de ataque.
- Regla del ganador: en HP, ATK, DEF, RES y bloqueo gana el mayor; en coste e intervalo, el menor.
- Barras escaladas respecto al mayor valor, ganador resaltado, texto accesible por fila y veredicto («Typhon gana en 3 de 7 atributos.»).
- `compareOperators()` en `script.js` y `ComparisonResultDto` (`RatioA`, `RatioB`, `LowerIsBetter`, `WinsA`, `WinsB`, `OverallWinner`) en `POST /api/comparisons`.

## Criterios de aceptación

- [x] El panel cambia al filtrar: con Sniper y 6★ pasa de 335 a 14 operadores.
- [x] Con una lista vacía muestra ceros, sin dividir por cero.
- [x] El comparador marca como ganador al valor menor en coste e intervalo.
- [x] Elegir el mismo operador en A y B muestra un aviso en lugar de un empate.
- [x] Pruebas unitarias en JS y en xUnit.

Implementado en el Pull Request #1 (`feature/retos-interactivos`).
