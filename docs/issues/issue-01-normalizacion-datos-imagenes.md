## Problema

RhodesAPI devuelve datos con estructuras variables, y las imágenes de los operadores no cargan. Lo detectamos al conectar el catálogo con `fetch()` (Sección 3).

### 1. Los dominios del plan original ya no existen

| URL | Estado |
|---|---|
| `https://api.rhodesapi.com/api/operator` (la del plan) | No resuelve DNS |
| `https://rhodesapi.up.railway.app/api/operator` | Responde 200 con CORS `*`, 338 registros y 5,5 MB |
| `https://ak.gamepress.gg/...` (todos los enlaces del campo `art`) | No resuelve DNS: ningún retrato carga |

### 2. Estructuras irregulares en el JSON

- **Números como texto con basura:** `"block": "block: 2"` (Saria, Penance, Mudrock y otros), `"cost": "09"`, `"block": " 1"`, `""`.
- **`affiliation` como texto o como arreglo:** `"Penguin Logistics"` frente a `["Columbia", "Rhine Lab"]`.
- **Tiers faltantes:** 34 operadores sin `e2max` y 12 sin `e1max`.
- **Operadores sin estadísticas:** Ulpianus trae `{"error": "something went wrong, most likely operator does not have stats yet on gp"}` en todos sus tiers.
- **Rareza 0** en unidades de reserva (`Reserve Operator - Defender`).
- **`description` vacía** en 3 operadores.
- **Coste DP poco confiable:** Thorns figura con 96 y Hoshiguma con `"02"`. No es corregible desde el cliente.

## Solución (adaptador seguro)

**Frontend (`script.js`)**
- `parseNumber()` extrae el primer número de cualquier valor: `"block: 2"` da 2, `"09"` da 9 y `""` da `null`.
- `toList()` acepta tanto texto como arreglo.
- `normalizeStats()` elige el tier más alto con HP válido y toma de `base` el coste, la RES, el intervalo y el redespliegue. Si ningún tier es válido, el operador se descarta.
- `normalizeOperator()` descarta los registros sin nombre, con rareza fuera de 1 a 6 o con una clase desconocida. Si la descripción está vacía, usa la biografía.

**Retratos**
- `tools/build_operator_portraits.py` reconstruye el identificador interno (`char_XXX_nombre`), a partir del enlace o, en los nombres en chino, de la `appellation` de la tabla del juego.
- Verifica cada retrato en `yuanyan3060/ArknightsGameResource` y genera `assets/data/operator-portraits.json`: 337 de 338 resueltos.
- Si una imagen falla, se muestra `assets/operator-placeholder.svg`.

**Backend C#**
- `RhodesOperatorAdapter` aplica las mismas reglas, sin lanzar excepciones.
- `RhodesApiHttpClient` convierte cualquier fallo remoto en `UpstreamApiException`, que el controlador responde como 503.

## Evidencia

- **Commit:** `df62d24` (feat(api): integración fetch() con RhodesAPI…).
- **Pruebas:** `tests/frontend/normalize.test.js` (17 casos) y `Backend/RhodesAI.Tests/OperatorServiceTests.cs` (adaptador y cliente HTTP).
