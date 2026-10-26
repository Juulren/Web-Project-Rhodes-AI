# Pautas estéticas del frontend

Estas pautas reemplazan a [SKILLS.md](../../SKILLS.md) en todo lo que contradicen: tipografía, color, fondos, esquinas y movimiento. Las reglas de SKILLS.md que no chocan con ellas siguen vigentes: estados de interacción, accesibilidad, redacción sin guiones y requisitos de publicación.

```text
<frontend_aesthetics>
You tend to converge toward generic, "on distribution" outputs. In frontend design, this creates what users call the "AI slop" aesthetic. Avoid this: make creative, distinctive frontends that surprise and delight. Focus on:

Typography: Choose fonts that are beautiful, unique, and interesting. Avoid generic fonts like Arial and Inter; opt instead for distinctive choices that elevate the frontend's aesthetics.

Color & Theme: Commit to a cohesive aesthetic. Use CSS variables for consistency. Dominant colors with sharp accents outperform timid, evenly-distributed palettes. Draw from IDE themes and cultural aesthetics for inspiration.

Motion: Use animations for effects and micro-interactions. Prioritize CSS-only solutions for HTML. Use Motion library for React when available. Focus on high-impact moments: one well-orchestrated page load with staggered reveals (animation-delay) creates more delight than scattered micro-interactions.

Backgrounds: Create atmosphere and depth rather than defaulting to solid colors. Layer CSS gradients, use geometric patterns, or add contextual effects that match the overall aesthetic.

Avoid generic AI-generated aesthetics:
- Overused font families (Inter, Roboto, Arial, system fonts)
- Clichéd color schemes (particularly purple gradients on white backgrounds)
- Predictable layouts and component patterns
- Cookie-cutter design that lacks context-specific character

Interpret creatively and make unexpected choices that feel genuinely designed for the context. Vary between light and dark themes, different fonts, different aesthetics. You still tend to converge on common choices (Space Grotesk, for example) across generations. Avoid this: it is critical that you think outside the box!
</frontend_aesthetics>
```

## Cómo se aplicaron: «PRTS, la terminal de Rhodes Island»

- **Contexto:** la interfaz imita la terminal PRTS de Arknights, con esquinas biseladas, franjas de peligro, sellos de estado, índices numerados por sección y una retícula técnica de fondo.
- **Tipografía:**
  - *Big Shoulders Display*, condensada e industrial, para los títulos.
  - *Familjen Grotesk* para el texto.
  - *Martian Mono* para datos, etiquetas y números.
  - Quedan prohibidas Inter, Roboto, Arial, Helvetica, Open Sans, Space Grotesk y las fuentes del sistema.
- **Color:** todos los colores viven en variables CSS:
  - **Tema oscuro:** tinta casi negra dominante, cian PRTS como acento principal y naranja de originium como acento cortante.
  - **Tema claro:** papel de laboratorio cálido con verde azulado profundo.
  - Cada rareza tiene su color.
- **Fondos:** capas de resplandores radiales, una retícula de 48 px hecha con degradados lineales, líneas de barrido y bandas de franjas de peligro con `repeating-linear-gradient`.
- **Movimiento:** una entrada de página orquestada, con las líneas del hero, los sellos y las tarjetas apareciendo en cascada mediante `animation-delay`. Las microinteracciones se limitan a escaneos al pasar el cursor y barras segmentadas. Con `prefers-reduced-motion` todo queda quieto.

`Backend/RhodesAI.Tests/FrontendContractTests.cs` verifica estas decisiones de forma automática.
