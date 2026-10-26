# Plan de Implementación Maestro: Web-Project-Rhodes-AI (Arknights Interactive Explorer)

## 1. Rol y Directrices Críticas para Claude Opus 5.5
Actúa como un Arquitecto de Software Senior Full-Stack especialista en **C# (.NET)**, **Desarrollo Web Frontend Moderno** y **Desarrollo Guiado por Pruebas (TDD)**. Tu objetivo es desarrollar de principio a fin el proyecto **Explorador Interactivo de Información de Arknights (Rhodes Island Terminal)** en el repositorio:
- **Repositorio oficial:** `https://github.com/Juulren/Web-Project-Rhodes-AI.git`
- **API pública base:** RhodesAPI (`https://api.rhodesapi.com/api/operator`)

### 1.1. Regla Crítica para el Frontend (`SKILLS.md`)
Antes de escribir cualquier línea de HTML, CSS o JavaScript, **lee e interpreta el archivo `SKILLS.md`** ubicado en el repositorio. Todo el diseño visual, sistema de grillas, tipografía, paleta de colores, micro-interacciones, animaciones, estructura semántica y buenas prácticas de UI/UX del frontend deben seguir estrictamente lo establecido en `SKILLS.md`.

### 1.2. Protocolo Estricto de Ejecución Paso a Paso (TDD + Git Push + Pausa Interactiva)
**PROHIBIDO generar o ejecutar todo el proyecto de una sola vez.** El desarrollo está dividido en **6 Secciones (correspondientes a los 6 Commits principales)**. En **cada sección** debes cumplir obligatoriamente este flujo de 5 pasos:
1. **Fase RED (Prueba que falla):** Escribir primero las pruebas unitarias/integración correspondientes a la sección (en C# con xUnit y/o validaciones automatizadas en JS) y ejecutarlas **demostrando en consola que fallan** porque la funcionalidad aún no existe o está incompleta.
2. **Fase GREEN (Implementación):** Escribir el código de producción mínimo y limpio (respetando POO, MVC, SOLID, CRUD y `SKILLS.md`) para que todas las pruebas pasen.
3. **Verificación de Pruebas:** Volver a ejecutar la suite de pruebas y mostrar el reporte en verde confirmando que el 100% de las pruebas pasan sin romper código previo.
4. **Commit y Push a GitHub:** Registrar el avance y subirlo al repositorio remoto (`https://github.com/Juulren/Web-Project-Rhodes-AI.git`) mediante `git add`, `git commit` y `git push`.
5. **PAUSA OBLIGATORIA DE CONTROL:** Una vez subido el commit a GitHub, **DETÉN TU EJECUCIÓN INMEDIATAMENTE** y pregúntame:
   > *"He completado, probado (Red -> Green) y subido el Commit #X a GitHub. ¿Deseas que avancemos a la siguiente sección o requieres hacer algún cambio o ajuste en esta etapa?"*
   **No avances a la siguiente sección hasta recibir mi confirmación explícita.**

---

## 2. Arquitectura del Proyecto y Compatibilidad con GitHub Pages
El proyecto debe cumplir simultáneamente con una arquitectura empresarial en **C# (.NET)** con su proyecto de pruebas y con la estructura de archivos en la raíz exigida para su publicación directa en **GitHub Pages**:

```text
Web-Project-Rhodes-AI/
├── index.html                  # Vista principal (en raíz para despliegue directo en GitHub Pages)
├── style.css                   # Estilos responsivos siguiendo estrictamente SKILLS.md
├── script.js                   # Lógica cliente, consumo con fetch(), filtros, retos y localStorage
├── README.md                   # Documentación completa, prompts de IA, retos y guía de uso
├── SKILLS.md                   # Directrices de diseño y arquitectura frontend
├── prompt.md                   # Este plan de implementación maestro
└── Backend/                    # Solución C# (.NET MVC / Web API) + Proyecto de Pruebas TDD
    ├── RhodesAI.sln
    ├── RhodesAI.Backend/
    │   ├── RhodesAI.Backend.csproj
    │   ├── Program.cs          # Configuración de DI (Inversión de Dependencias), CORS y rutas MVC
    │   ├── Controllers/        # Capa Controlador (MVC)
    │   │   ├── OperatorsController.cs
    │   │   ├── FavoritesController.cs
    │   │   └── ComparisonsController.cs
    │   ├── Models/             # Capa Modelo y Entidades POO
    │   │   ├── Base/
    │   │   │   └── BaseEntity.cs
    │   │   ├── Domain/
    │   │   │   ├── Operator.cs
    │   │   │   ├── OperatorStats.cs
    │   │   │   ├── CombatOperator.cs
    │   │   │   └── FavoriteSquadMember.cs
    │   │   └── DTOs/
    │   │       ├── OperatorFilterDto.cs
    │   │       ├── ComparisonResultDto.cs
    │   │       └── DynamicStatsSummaryDto.cs
    │   ├── Services/           # Lógica de Negocio (SOLID)
    │   │   ├── Interfaces/
    │   │   │   ├── IOperatorReadService.cs
    │   │   │   ├── IOperatorStatsService.cs
    │   │   │   ├── IStatCalculatorStrategy.cs
    │   │   │   └── IFavoriteCrudService.cs
    │   │   └── Implementations/
    │   │       ├── RhodesOperatorService.cs
    │   │       ├── EliteTwoStatCalculator.cs
    │   │       └── FavoriteCrudService.cs
    │   └── Repositories/       # Capa de Acceso a Datos y Persistencia CRUD
    │       ├── Interfaces/
    │       │   └── IRepository.cs
    │       └── Implementations/
    │           └── InMemoryFavoriteRepository.cs
    └── RhodesAI.Tests/         # Proyecto de Pruebas Unitarias y de Integración (TDD)
        ├── RhodesAI.Tests.csproj
        ├── DomainTests.cs
        ├── FavoriteCrudTests.cs
        ├── OperatorServiceTests.cs
        └── FrontendContractTests.cs
```

### Estrategia de Conexión Híbrida (`fetch()`)
En `script.js`, el cliente HTTP utilizará `fetch ()` apuntando primero al backend local en C# (`http://localhost:5000/api/operators`) para aprovechar el procesamiento MVC/CRUD, e implementará un **Fallback Automático Transparente** hacia `https://api.rhodesapi.com/api/operator` y `localStorage` cuando la aplicación se ejecute desde **GitHub Pages** (`https://juulren.github.io/Web-Project-Rhodes-AI/`).

---

## 3. Especificaciones del Backend en C# (POO, MVC, SOLID y CRUD)

### 3.1. Programación Orientada a Objetos (POO)
1. **Abstracción y Herencia:**
   - Crear la clase abstracta `BaseEntity` con propiedades encapsuladas (`Id`, `CreatedAt`, `UpdatedAt`) y métodos virtuales de validación.
   - Crear la clase base `Operator` y subclases especializadas o composiciones (`CombatOperator`, `OperatorStats`) que hereden de `BaseEntity`.
2. **Encapsulamiento:**
   - Uso de propiedades con modificadores de acceso (`public get`, `private/protected set`), validación de invariantes en constructores y métodos de dominio para mutar el estado (ej. `UpdateTacticalNotes(string notes)`).
3. **Polimorfismo:**
   - Método virtual/abstracto `CalculateCombatPower()` sobrescrito (`override`) según el nivel de promoción del operador (Base, Elite 1, Elite 2) o su clase táctica (*Guard, Caster, Sniper, Defender, Medic, Vanguard, Supporter, Specialist*).

### 3.2. Patrón MVC (Model - View - Controller)
- **Models:** Clases de dominio fuertemente tipadas que mapean la respuesta JSON de RhodesAPI (`name`, `rarity`, `class`, `art`, `description`, `biography`, `statistics`, `affiliation`) junto con DTOs de transferencia.
- **Views:** El frontend desacoplado en la raíz (`index.html`, `style.css`, `script.js`) que actúa como capa de presentación reactiva servida estáticamente o vía GitHub Pages.
- **Controllers:**
  - `OperatorsController`: Endpoints `GET /api/operators`, `GET /api/operators/{name}`, `GET /api/operators/filter`, `GET /api/operators/stats`.
  - `FavoritesController`: Controlador RESTful dedicado al **CRUD completo** de operadores favoritos y notas tácticas del usuario.
  - `ComparisonsController`: Endpoint `POST /api/comparisons` que recibe dos operadores y devuelve el análisis comparativo de sus estadísticas.

### 3.3. Principios SOLID Aplicados
- **S - Single Responsibility Principle (Responsabilidad Única):**
  - `RhodesApiHttpClient` solo consume y deserializa datos de RhodesAPI.
  - `RhodesOperatorService` solo aplica reglas de negocio (filtrado, ordenamiento y estadísticas dinámicas).
  - `InMemoryFavoriteRepository` solo gestiona la persistencia en memoria/JSON de los registros CRUD.
- **O - Open/Closed Principle (Abierto/Cerrado):**
  - Implementar la interfaz `IStatCalculatorStrategy` para agregar nuevas fórmulas de evaluación de operadores (ej. sinergia por facción o eficiencia de coste DP) sin modificar `RhodesOperatorService`.
- **L - Liskov Substitution Principle (Sustitución de Liskov):**
  - Cualquier implementación de `IRepository<FavoriteSquadMember, string>` (en memoria o archivo JSON) puede sustituirse en los controladores sin alterar el comportamiento esperado de la API.
- **I - Interface Segregation Principle (Segregación de Interfaces):**
  - Dividir los contratos en interfaces granulares: `IOperatorReadService`, `IOperatorStatsService` e `IFavoriteCrudService` en lugar de una interfaz monolítica.
- **D - Dependency Inversion Principle (Inversión de Dependencias):**
  - Los controladores dependen exclusivamente de abstracciones registradas en el contenedor IoC de `Program.cs` (`builder.Services.AddScoped<...>()`).

### 3.4. Operaciones CRUD Completas (`FavoritesController`)
Implementar las 4 operaciones fundamentales sobre la entidad `FavoriteSquadMember` (que almacena el operador favorito, rol asignado en el escuadrón, nivel de prioridad y nota táctica personalizada):
1. **CREATE (`POST /api/favorites`):** Registra un operador en la lista de favoritos validando que no esté duplicado.
2. **READ (`GET /api/favorites` y `GET /api/favorites/{id}`):** Obtiene todos los operadores favoritos o el detalle de uno específico.
3. **UPDATE (`PUT /api/favorites/{id}`):** Permite editar la nota táctica personalizada, nivel de promoción preferido o calificación asignada al operador favorito.
4. **DELETE (`DELETE /api/favorites/{id}`):** Elimina un operador de la colección de favoritos.
*(En `script.js`, todas estas operaciones CRUD deben sincronizarse también en `localStorage` para garantizar persistencia total en GitHub Pages).*

---

## 4. Requisitos Obligatorios del Frontend (`index.html`, `style.css`, `script.js`)
Siguiendo estrictamente las pautas visuales y de arquitectura de `SKILLS.md`, implementar:

1. **Interfaz con HTML, CSS y JavaScript:** Código modular, semántico y limpio en `index.html`, `style.css` y `script.js` sin depender de páginas estáticas con datos escritos manualmente.
2. **Conexión a la API con `fetch ()`:** Consumir datos reales de los operadores de Arknights de manera asíncrona (`async/await`).
3. **Mínimo 10 elementos con imagen y descripción:** Renderizar el catálogo mostrando al menos 10 operadores (o más, paginados/filtrados) en tarjetas que incluyan su retrato oficial (`art`), nombre (`name`), rareza (`rarity` de 1 a 6 estrellas), clase (`class`) y descripción/biografía (`description` / `lore`).
4. **Buscador funcional en tiempo real:** Barra de búsqueda que filtre operadores por nombre, facción o palabras clave en su descripción.
5. **Filtros organizadores:** Selectores interactivos para filtrar resultados por **Clase** (*Guard, Sniper, Medic, Caster, Defender, Vanguard, Supporter, Specialist*), **Rareza** (*1 a 6 estrellas*) y **Afiliación**.
6. **Sección de Favoritos interactiva (con soporte CRUD):** Panel o pestaña dedicada donde el usuario pueda agregar operadores a favoritos, agregar/editar una nota táctica personalizada (Update) y eliminarlos (Delete).
7. **Persistencia con `localStorage`:** Guardar y sincronizar automáticamente la lista de favoritos (y sus notas tácticas) en `localStorage` para que permanezcan al recargar la página.
8. **Manejo de errores y estados vacíos:** Mostrar mensajes y alertas visuales claras cuando una búsqueda no arroje resultados o cuando falle la consulta a la API (incluyendo un botón de reintento).
9. **Diseño Responsivo (Mobile-First & Desktop):** Interfaz adaptable a teléfonos celulares, tablets y computadoras de escritorio.
10. **Listo para GitHub Pages:** Rutas relativas limpias y compatibilidad total con HTTPS en GitHub Pages.

---

## 5. Implementación de TODOS los Retos Adicionales (Sección 5 de la Tarea)
El proyecto debe incluir **los 5 retos** integrados en la interfaz y explicados en el código:

1. **Modo Oscuro y Claro (Theme Switcher):**
   - Botón conmutador en el encabezado que alterne entre el tema táctico oscuro de *Rhodes Island* y el tema claro de laboratorio, guardando la preferencia del usuario en `localStorage`.
2. **Ordenamiento Alfabético y Multicriterio:**
   - Selector que permita ordenar los resultados mostrados **Alfabéticamente (A-Z y Z-A)**, así como por **Rareza (Mayor a menor / Menor a mayor)**.
3. **Estadísticas Dinámicas en Tiempo Real:**
   - Panel superior (Dashboard de Inteligencia) que se recalcule dinámicamente cada vez que se aplique una búsqueda o filtro, mostrando: total de operadores listados, promedio de HP, ATK, DEF y Coste DP del grupo visible, y distribución porcentual por clase y rareza.
4. **Comparador de Dos Personajes (Versus Modal / Panel):**
   - Herramienta interactiva donde el usuario seleccione **Operador A** y **Operador B** para comparar cara a cara sus estadísticas (`HP`, `ATK`, `DEF`, `RES`, `Cost`, `Block`, `Attack Time`) con barras comparativas visuales que resalten quién gana en cada atributo.
5. **Historial de Búsquedas Persistente:**
   - Registro automático de las últimas búsquedas realizadas (guardado en `localStorage`) con chips interactivos debajo del buscador para volver a ejecutar una búsqueda anterior con un clic o limpiar el historial.

---

## 6. Plan de Ejecución Paso a Paso por Commit (Con Ciclo TDD y Pausa de Confirmación)

Deberás ejecutar el desarrollo siguiendo estrictamente estas **6 Secciones secuenciales**. Recuerda: en cada sección primero haces que las pruebas fallen (RED), luego implementas para que pasen (GREEN), haces commit/push a `https://github.com/Juulren/Web-Project-Rhodes-AI.git` y **te detienes a preguntarme si avanzamos o hacemos cambios**.

---

### SECCIÓN 1 / COMMIT #1: Arquitectura Backend C# (POO, MVC, SOLID y CRUD)
- **Objetivo:** Crear la solución en `Backend/`, modelos POO (`BaseEntity`, `Operator`, `CombatOperator`, `FavoriteSquadMember`), interfaces SOLID, repositorio y controladores MVC (`OperatorsController`, `FavoritesController`, `ComparisonsController`).
- **Paso 1 (Pruebas que fallan - RED):**
  - Crear el proyecto `RhodesAI.Tests` y escribir las pruebas unitarias en `DomainTests.cs` (validación de encapsulamiento y polimorfismo en `CalculateCombatPower()`) y `FavoriteCrudTests.cs` (operaciones Create, Read, Update, Delete y rechazo de duplicados) apuntando a métodos aún no implementados (`throw new NotImplementedException()`).
  - Ejecutar `dotnet test` y mostrar que las pruebas **fallan**.
- **Paso 2 (Implementación - GREEN):**
  - Implementar todas las clases de dominio, servicios SOLID, repositorio en memoria/JSON y controladores MVC en C#.
  - Volver a ejecutar `dotnet test` y verificar que el 100% de las pruebas **pasen**.
- **Paso 3 (Subida a GitHub):**
  - Ejecutar commit: `feat(backend): estructura inicial C# MVC, entidades POO, interfaces SOLID y CRUD de favoritos`
  - Ejecutar `git push origin main`.
- **Paso 4 (Pausa Interactiva):**
  - Detenerse y preguntar al usuario si desea avanzar a la Sección 2 o realizar cambios en el Commit #1.

---

### SECCIÓN 2 / COMMIT #2: Estructura Base Frontend (`index.html` y `style.css` con `SKILLS.md`)
- **Objetivo:** Construir la interfaz semántica en `index.html` y el sistema de diseño responsivo en `style.css` siguiendo estrictamente las reglas de `SKILLS.md`.
- **Paso 1 (Pruebas que fallan - RED):**
  - Escribir pruebas de verificación de estructura DOM y contratos visuales (`FrontendContractTests.cs` o script de prueba automatizado) que validen la existencia de contenedores obligatorios: barra de búsqueda, selectores de filtro, contenedor de grilla de operadores, sección de favoritos, panel de estadísticas dinámicas, modal comparador, selector de tema claro/oscuro y reglas `@media` responsivas en `style.css`.
  - Ejecutar la validación antes de escribir el HTML/CSS completo para demostrar que **falla**.
- **Paso 2 (Implementación - GREEN):**
  - Implementar `index.html` y `style.css` respetando `SKILLS.md` al 100%.
  - Ejecutar nuevamente las pruebas de estructura y comprobar que **pasan**.
- **Paso 3 (Subida a GitHub):**
  - Ejecutar commit: `feat(frontend): estructura base index.html y sistema de diseno style.css siguiendo SKILLS.md`
  - Ejecutar `git push origin main`.
- **Paso 4 (Pausa Interactiva):**
  - Detenerse y preguntar al usuario si desea avanzar a la Sección 3 o realizar cambios en el Commit #2.

---

### SECCIÓN 3 / COMMIT #3: Conexión `fetch()`, Renderizado de Operadores y Manejo de Errores
- **Objetivo:** Implementar en `script.js` la conexión híbrida con `fetch ()` (Backend C# + Fallback a RhodesAPI), el renderizado de al menos 10 operadores con imagen y descripción, y el manejo de errores/estados vacíos cuando la API no responde.
- **Paso 1 (Pruebas que fallan - RED):**
  - Crear pruebas unitarias para la capa de datos y mapeo de operadores (normalización de imágenes `art`, extracción de `description`, manejo de respuesta HTTP fallida 500/404 y renderizado mínimo de 10 elementos) antes de implementar las funciones en `script.js` y `OperatorServiceTests.cs`.
  - Ejecutar las pruebas y verificar que **fallen**.
- **Paso 2 (Implementación - GREEN):**
  - Implementar la lógica asíncrona `fetch ()` con `try/catch`, normalización de operadores, renderizado de tarjetas y alertas de error con botón de reintento.
  - Ejecutar las pruebas y confirmar que **pasan**.
- **Paso 3 (Subida a GitHub):**
  - Ejecutar commit: `feat(api): integracion fetch() con RhodesAPI, manejo de errores y renderizado de tarjetas`
  - Ejecutar `git push origin main`.
- **Paso 4 (Pausa Interactiva):**
  - Detenerse y preguntar al usuario si desea avanzar a la Sección 4 o realizar cambios en el Commit #3.

---

### SECCIÓN 4 / COMMIT #4: Buscador Funcional, Filtros y Favoritos CRUD con `localStorage`
- **Objetivo:** Implementar el buscador en tiempo real, los filtros combinados (Clase, Rareza y Afiliación), la sección de Favoritos con soporte CRUD (Crear, Leer, Actualizar nota táctica y Eliminar) y su persistencia en `localStorage` + API C#.
- **Paso 1 (Pruebas que fallan - RED):**
  - Escribir pruebas para el motor de búsqueda/filtrado combinado y la sincronización CRUD en `localStorage` (agregar favorito, evitar duplicados, editar nota táctica y eliminar favorito) esperando que **fallen** inicialmente.
- **Paso 2 (Implementación - GREEN):**
  - Implementar el filtrado reactivo, el buscador con mensajes cuando no existen resultados y el módulo completo de Favoritos con `localStorage`.
  - Ejecutar las pruebas y comprobar que **pasan**.
- **Paso 3 (Subida a GitHub):**
  - Ejecutar commit: `feat(filters): buscador funcional, filtros por clase/rareza y persistencia CRUD en localStorage`
  - Ejecutar `git push origin main`.
- **Paso 4 (Pausa Interactiva):**
  - Detenerse y preguntar al usuario si desea avanzar a la Sección 5 o realizar cambios en el Commit #4.

---

### SECCIÓN 5 / COMMIT #5: Implementación de los 5 Retos Adicionales (Rama + Pull Request)
- **Objetivo:** Implementar los 5 retos adicionales de la Sección 5 de la tarea:
  1. Modo oscuro y claro persistente.
  2. Ordenar resultados alfabéticamente (A-Z / Z-A) y por rareza.
  3. Mostrar estadísticas dinámicas en tiempo real (promedios de HP, ATK, DEF, Coste y conteos).
  4. Comparar dos personajes cara a cara.
  5. Implementar un historial de búsquedas interactivo en `localStorage`.
- **Paso 1 (Pruebas que fallan - RED):**
  - Crear una rama `feature/retos-interactivos` (para cumplir también con el requisito de Pull Request).
  - Escribir pruebas para el algoritmo de ordenamiento A-Z/Z-A, el cálculo de estadísticas dinámicas (`DynamicStatsSummaryDto`), el comparador de dos operadores (`ComparisonResultDto`) y el límite/deduplicación del historial de búsquedas. Ejecutar y demostrar que **fallan**.
- **Paso 2 (Implementación - GREEN):**
  - Implementar los 5 retos tanto en los servicios/controladores de C# como en `script.js`, `index.html` y `style.css`.
  - Ejecutar toda la suite de pruebas y verificar que **pasan**.
- **Paso 3 (Subida a GitHub y Pull Request):**
  - Ejecutar commit: `feat(challenges): implementacion de los 5 retos (tema oscuro/claro, orden A-Z, stats dinamicas, comparador e historial)`
  - Subir la rama `feature/retos-interactivos` y crear/fusionar el Pull Request hacia `main` en GitHub.
- **Paso 4 (Pausa Interactiva):**
  - Detenerse y preguntar al usuario si desea avanzar a la Sección 6 o realizar cambios en el Commit #5.

---

### SECCIÓN 6 / COMMIT #6: Documentación Exhaustiva en `README.md`, Issues y Verificación de GitHub Pages
- **Objetivo:** Documentar al menos 2 Issues en el repositorio, verificar la compatibilidad de despliegue en GitHub Pages y redactar el archivo `README.md` con todos los requisitos académicos.
- **Paso 1 (Pruebas que fallan - RED):**
  - Ejecutar un script validador de entregables que verifique que `README.md` contenga todas las secciones obligatorias (integrantes, arquitectura C# POO/MVC/SOLID/CRUD, 3 prompts de IA, 1 respuesta corregida, 1 error resuelto, 1 modificación explicada, reflexión del equipo y respuestas a las 6 preguntas de evaluación). Mostrar que **falla** antes de actualizar el `README.md`.
- **Paso 2 (Implementación - GREEN):**
  - Redactar el archivo `README.md` completo y verificar el pase del validador de entregables más toda la suite de pruebas del proyecto (`dotnet test`).
- **Paso 3 (Subida a GitHub):**
  - Ejecutar commit: `docs(readme): documentacion completa del proyecto, prompts de IA, correcciones y guia de despliegue`
  - Ejecutar `git push origin main`.
- **Paso 4 (Pausa Interactiva Final):**
  - Detenerse y preguntar al usuario si requiere algún ajuste final o revisión antes de dar por concluida la implementación.

---

## 7. Especificación Detallada de Issues, Pull Request y `README.md`

### 7.1. Issues y Pull Request Requeridos en GitHub
Durante el flujo de trabajo en `https://github.com/Juulren/Web-Project-Rhodes-AI.git`, documentar o crear mediante GitHub CLI (`gh issue create` / `gh pr create`):
- **Issue #1:** *Normalización de datos e imágenes faltantes en ciertos operadores de RhodesAPI* (documentando cómo algunos operadores devuelven estructuras variables en `art` o `statistics` y cómo se solucionó con un adaptador seguro).
- **Issue #2:** *Implementación del panel comparador de 2 operadores y cálculo de estadísticas dinámicas en tiempo real* (documentando la mejora incorporada mediante Pull Request).
- **Pull Request #1:** Fusión de la rama `feature/retos-interactivos` hacia `main` incorporando los 5 retos adicionales y sus pruebas unitarias.

### 7.2. Contenido Obligatorio del archivo `README.md`
El archivo `README.md` generado en la Sección 6 debe incluir de forma clara y estructurada:
1. **Descripción del Proyecto, Integrantes e Instrucciones de Uso:**
   - Explicación funcional del explorador de operadores de *Arknights* y guía paso a paso para ejecutar tanto el Frontend en GitHub Pages como el Backend en C# (.NET).
2. **Arquitectura Técnica (Backend C# + Frontend `SKILLS.md`):**
   - Diagrama y explicación de cómo se aplicaron **POO** (Herencia, Polimorfismo, Encapsulamiento, Abstracción), **MVC**, los 5 principios **SOLID** y el **CRUD** completo de favoritos.
3. **Enlaces Oficiales:**
   - Enlace al repositorio: `https://github.com/Juulren/Web-Project-Rhodes-AI.git`
   - Enlace a la aplicación funcionando en GitHub Pages: `https://juulren.github.io/Web-Project-Rhodes-AI/`
4. **Documentación Obligatoria del Uso de Inteligencia Artificial:**
   - **Tres prompts importantes** utilizados durante el desarrollo.
   - **Una respuesta de la IA que haya requerido correcciones** (mostrando el código original sugerido, por qué fallaba o era ineficiente, y cómo se corrigió).
   - **Un error técnico encontrado y cómo lo resolvieron** (ej. manejo de operadores sin estadísticas `e2` o fallback automático de CORS entre C# local y GitHub Pages).
   - **Una modificación realizada al código explicada detalladamente** (explicando línea por línea el funcionamiento técnico de los 5 retos adicionales implementados).
   - **Una reflexión grupal** sobre qué aprendieron utilizando inteligencia artificial y desarrollo guiado por pruebas (TDD).
5. **Guía de Estudio y Respuestas para la Presentación en Clase (Sección 8 de la Tarea):**
   - Respuestas técnicas claras y vinculadas a las funciones reales del proyecto para que cualquier integrante pueda responder:
     1. *¿Qué función cumple `fetch ()` dentro de su aplicación?*
     2. *¿Qué diferencia existe entre HTML, CSS y JavaScript en el proyecto?*
     3. *¿Cómo funciona el buscador y el sistema de filtros que desarrollaron?*
     4. *¿Qué sucede internamente y en la interfaz cuando la API no responde?*
     5. *¿Qué parte del código fue generada por la IA y qué modificaciones realizaron ustedes?*
     6. *¿Cómo publicaron su aplicación mediante GitHub Pages?*