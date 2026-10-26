# Pull Request #1: los 5 retos interactivos

**Rama:** `feature/retos-interactivos` → `main`

## Resumen

Implementa los 5 retos adicionales de la Sección 5 de la tarea, tanto en el frontend (`script.js`, `index.html`, `style.css`) como en el backend C# (servicios y DTOs), siguiendo el ciclo TDD Red → Green.

| Reto | Frontend | Backend C# |
|---|---|---|
| 1. Tema oscuro y claro | Botón en la barra (`aria-pressed`), preferencia en `localStorage` (`rhodes.theme`), respeta `prefers-color-scheme` y se aplica antes del primer pintado | No aplica |
| 2. Orden A a Z, Z a A y por rareza | `sortOperators()`: ignora comillas y acentos y desempata por nombre | `OperatorFilterDto.SortBy` (`OperatorSort`) en `/api/operators/filter` |
| 3. Estadísticas dinámicas | `computeStats()`: total, promedios de HP, ATK, DEF y coste, y distribución porcentual por clase y rareza, recalculada en cada búsqueda o filtro | `DynamicStatsSummaryDto` con `ClassDistribution` y `RarityDistribution` en `/api/operators/stats` |
| 4. Comparador de dos operadores | Modal `<dialog>` que se abre desde la barra o desde cada tarjeta, con 7 atributos, barras escaladas, ganador resaltado y veredicto | `ComparisonResultDto` con `RatioA`, `RatioB`, `LowerIsBetter`, `WinsA`, `WinsB` y `OverallWinner` en `POST /api/comparisons` |
| 5. Historial de búsquedas | `createSearchHistory()`: 8 búsquedas como máximo, sin duplicados (ignora mayúsculas y acentos), chips para repetir una búsqueda y botón para limpiar | No aplica |

## Regla del comparador

En HP, ATK, DEF, RES y bloqueo gana el valor **mayor**. En coste de DP e intervalo de ataque gana el valor **menor**. Cada barra se mide respecto al mayor de los dos valores del atributo.

## Pruebas

- **RED:** 38 pruebas nuevas en JS (`tests/frontend/challenges*.test.js`) y 12 en xUnit (`ChallengesServiceTests.cs`) fallaron antes de implementar. Ver `docs/test-reports/seccion5-red-*.txt`.
- **GREEN:** 119/119 en JS (`npm test`) y 142/142 en xUnit (`dotnet test Backend/RhodesAI.sln`). Ver `docs/test-reports/seccion5-green-*.txt`.
- **Navegador:** comprobado con los 335 operadores reales: filtro Sniper 6★ con orden Z a A, comparador Typhon contra W, tema claro persistente y chips del historial.

Relacionado con el Issue #2: *Implementación del panel comparador de 2 operadores y cálculo de estadísticas dinámicas en tiempo real*.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
