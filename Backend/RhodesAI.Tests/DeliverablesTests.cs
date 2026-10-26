using System.Text.RegularExpressions;

namespace RhodesAI.Tests;

/// <summary>
/// Validador de entregables de la Tarea 3: archivos obligatorios en la raíz y contenido mínimo de README.md
/// (integrantes, arquitectura, documentación del uso de IA y respuestas para la presentación).
/// </summary>
public partial class DeliverablesTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SKILLS.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    private static string Readme()
    {
        var path = Path.Combine(Root, "README.md");
        Assert.True(File.Exists(path), "Falta README.md en la raíz del repositorio.");
        return File.ReadAllText(path);
    }

    /// <summary>Texto de una sección: desde su encabezado hasta el siguiente encabezado del mismo nivel o superior.</summary>
    private static string Section(string readme, string heading)
    {
        var lines = readme.Split('\n');
        int start = Array.FindIndex(lines, l => Regex.IsMatch(l, $@"^#{{2,3}}\s+{Regex.Escape(heading)}", RegexOptions.IgnoreCase));
        Assert.True(start >= 0, $"README.md no tiene la sección «{heading}».");

        int level = lines[start].TakeWhile(c => c == '#').Count();
        var body = lines.Skip(start + 1)
            .TakeWhile(l => !(l.StartsWith('#') && l.TakeWhile(c => c == '#').Count() <= level));
        return string.Join('\n', body);
    }

    private static int Words(string text) =>
        Regex.Matches(Regex.Replace(text, @"```.*?```", " ", RegexOptions.Singleline), @"[\p{L}\p{N}]+").Count;

    // ---------- Archivos obligatorios ----------

    [Theory]
    [InlineData("index.html")]
    [InlineData("style.css")]
    [InlineData("script.js")]
    [InlineData("README.md")]
    [InlineData("SKILLS.md")]
    [InlineData("prompt.md")]
    [InlineData("404.html")]
    [InlineData(".nojekyll")]
    public void RootFile_Exists(string file)
    {
        Assert.True(File.Exists(Path.Combine(Root, file)), $"Falta {file} en la raíz.");
    }

    // ---------- Secciones del README ----------

    [Theory]
    [InlineData("Integrantes", 3)]
    [InlineData("Descripción", 60)]
    [InlineData("Instrucciones de uso", 80)]
    [InlineData("Arquitectura técnica", 150)]
    [InlineData("Retos adicionales", 60)]
    [InlineData("Pruebas", 40)]
    [InlineData("Dificultades", 60)]
    [InlineData("Uso de inteligencia artificial", 40)]
    [InlineData("Tres prompts importantes", 60)]
    [InlineData("Respuesta de la IA que requirió correcciones", 60)]
    [InlineData("Error encontrado y cómo lo resolvimos", 60)]
    [InlineData("Modificación explicada", 120)]
    [InlineData("Reflexión del equipo", 60)]
    [InlineData("Preguntas para la presentación", 200)]
    public void Readme_HasSectionWithRealContent(string heading, int minWords)
    {
        var words = Words(Section(Readme(), heading));
        Assert.True(words >= minWords, $"La sección «{heading}» tiene {words} palabras; se esperan al menos {minWords}.");
    }

    [Fact]
    public void Readme_LinksRepositoryPagesIssuesAndPullRequest()
    {
        var readme = Readme();

        Assert.Contains("https://github.com/Juulren/Web-Project-Rhodes-AI", readme);
        Assert.Contains("https://juulren.github.io/Web-Project-Rhodes-AI/", readme);
        // GitHub numera Issues y Pull Requests en una sola secuencia: el #1 es el PR, los Issues son el #2 y el #3.
        Assert.Contains("https://github.com/Juulren/Web-Project-Rhodes-AI/issues/2", readme);
        Assert.Contains("https://github.com/Juulren/Web-Project-Rhodes-AI/issues/3", readme);
        Assert.Contains("https://github.com/Juulren/Web-Project-Rhodes-AI/pull/1", readme);
    }

    [Fact]
    public void Readme_ExplainsOopMvcEverySolidPrincipleAndCrud()
    {
        var architecture = Section(Readme(), "Arquitectura técnica");

        foreach (var concept in new[] { "Herencia", "Polimorfismo", "Encapsulamiento", "Abstracción", "MVC", "CRUD",
                     "Responsabilidad única", "Abierto/Cerrado", "Sustitución de Liskov", "Segregación de interfaces",
                     "Inversión de dependencias" })
            Assert.Contains(concept, architecture, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Readme_DocumentsThreeNumberedPrompts()
    {
        var prompts = Section(Readme(), "Tres prompts importantes");
        Assert.Equal(3, PromptHeading().Matches(prompts).Count);
    }

    [Fact]
    public void Readme_CorrectedAnswerShowsOriginalAndFix()
    {
        var section = Section(Readme(), "Respuesta de la IA que requirió correcciones");

        Assert.Contains("Lo que propuso la IA", section);
        Assert.Contains("Por qué fallaba", section);
        Assert.Contains("Cómo lo corregimos", section);
    }

    [Fact]
    public void Readme_ModificationCoversTheFiveChallenges()
    {
        var section = Section(Readme(), "Modificación explicada");

        foreach (var name in new[] { "sortOperators", "computeStats", "compareOperators", "createSearchHistory", "resolveInitialTheme" })
            Assert.Contains(name, section);
        Assert.True(Regex.Matches(section, "```js").Count >= 2, "La modificación debe incluir fragmentos de código explicados.");
    }

    [Theory]
    [InlineData("¿Qué función cumple fetch() dentro de su aplicación?")]
    [InlineData("¿Qué diferencia existe entre HTML, CSS y JavaScript")]
    [InlineData("¿Cómo funciona el buscador")]
    [InlineData("¿Qué sucede")]
    [InlineData("¿Qué parte del código fue generada por la IA y qué modificaciones realizaron")]
    [InlineData("¿Cómo publicaron su aplicación mediante GitHub Pages?")]
    public void Readme_AnswersEveryPresentationQuestion(string question)
    {
        var section = Section(Readme(), "Preguntas para la presentación");
        int index = section.IndexOf(question, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Falta la pregunta «{question}».");

        // Cada pregunta va seguida de una respuesta de al menos 25 palabras antes de la siguiente pregunta.
        var answer = section[(index + question.Length)..];
        int next = answer.IndexOf("¿", 1, StringComparison.Ordinal);
        Assert.True(Words(next > 0 ? answer[..next] : answer) >= 25, $"La respuesta a «{question}» es demasiado corta.");
    }

    [Fact]
    public void Readme_RelativeLinksPointToExistingFiles()
    {
        foreach (Match link in RelativeLink().Matches(Readme()))
        {
            var target = link.Groups[1].Value.Split('#')[0];
            if (target.Length == 0) continue;
            Assert.True(File.Exists(Path.Combine(Root, target)) || Directory.Exists(Path.Combine(Root, target)),
                $"El enlace relativo {target} del README no existe.");
        }
    }

    [GeneratedRegex(@"^#{4}\s+Prompt\s+[123]\b", RegexOptions.Multiline)]
    private static partial Regex PromptHeading();

    [GeneratedRegex(@"\]\((?!https?://|#|mailto:)([^)\s]+)\)")]
    private static partial Regex RelativeLink();
}
