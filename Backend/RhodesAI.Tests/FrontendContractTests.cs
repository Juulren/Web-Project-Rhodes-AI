using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace RhodesAI.Tests;

/// <summary>
/// Contrato del frontend estático (index.html + style.css): contenedores obligatorios de la tarea
/// y reglas visuales de SKILLS.md verificadas de forma automática.
/// </summary>
public partial class FrontendContractTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string[] StaticPages = ["index.html", "404.html", "privacidad.html", "terminos.html"];

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SKILLS.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio (SKILLS.md).");
    }

    private static string ReadFile(string relative)
    {
        var path = Path.Combine(Root, relative);
        Assert.True(File.Exists(path), $"Falta el archivo {relative} en la raíz del repositorio.");
        return File.ReadAllText(path);
    }

    private static IHtmlDocument Page(string relative = "index.html") => new HtmlParser().ParseDocument(ReadFile(relative));

    private static IElement Required(IParentNode doc, string selector)
    {
        var element = doc.QuerySelector(selector);
        Assert.True(element is not null, $"No existe el elemento obligatorio '{selector}'.");
        return element!;
    }

    private static string Css() => CssComments().Replace(ReadFile("style.css"), string.Empty);

    /// <summary>Todas las declaraciones "propiedad: valor" de la hoja de estilos.</summary>
    private static IEnumerable<(string Property, string Value)> Declarations() =>
        CssDeclaration().Matches(Css()).Select(m => (m.Groups[1].Value.Trim().ToLowerInvariant(), m.Groups[2].Value.Trim()));

    private static string RootBlock()
    {
        var match = RootRule().Match(Css());
        Assert.True(match.Success, "style.css debe declarar los tokens en :root.");
        return match.Groups[1].Value;
    }

    private static Dictionary<string, string> RootTokens() =>
        CssDeclaration().Matches(RootBlock())
            .Where(m => m.Groups[1].Value.Trim().StartsWith("--"))
            .ToDictionary(m => m.Groups[1].Value.Trim(), m => m.Groups[2].Value.Trim());

    // =====================================================================
    // Estructura y metadatos
    // =====================================================================

    [Fact]
    public void Index_DeclaresLanguageTitleAndSocialMetadata()
    {
        var doc = Page();

        Assert.Equal("es", doc.DocumentElement.GetAttribute("lang"));
        Assert.False(string.IsNullOrWhiteSpace(doc.Title));
        Assert.NotNull(doc.QuerySelector("meta[name='viewport']"));
        Assert.False(string.IsNullOrWhiteSpace(Required(doc, "meta[name='description']").GetAttribute("content")));
        foreach (var og in new[] { "og:title", "og:description", "og:image", "og:type" })
            Assert.NotNull(doc.QuerySelector($"meta[property='{og}']"));
        Assert.NotNull(doc.QuerySelector("meta[name='twitter:card']"));
        Assert.NotNull(doc.QuerySelector("link[rel='icon']"));
    }

    [Fact]
    public void Index_UsesRelativeAssetPathsForGitHubPages()
    {
        var doc = Page();

        Assert.Equal("style.css", Required(doc, "link[rel='stylesheet'][href='style.css']").GetAttribute("href"));
        var script = Required(doc, "script[src='script.js']");
        Assert.True(script.HasAttribute("defer"), "script.js debe cargarse con defer.");

        var localRefs = doc.QuerySelectorAll("[src], [href]")
            .Select(e => e.GetAttribute("src") ?? e.GetAttribute("href")!)
            .Where(v => !v.StartsWith("http") && !v.StartsWith('#') && !v.StartsWith("mailto:"));
        Assert.All(localRefs, v => Assert.False(v.StartsWith('/'), $"La ruta {v} debe ser relativa."));
    }

    [Fact]
    public void Index_HasSemanticLandmarksAndSkipLink()
    {
        var doc = Page();

        Required(doc, "header nav");
        Required(doc, "main#main");
        Required(doc, "footer");
        Assert.True(doc.QuerySelectorAll("main section").Length >= 5, "El contenido debe dividirse en secciones semánticas.");

        var firstLink = doc.Body!.QuerySelector("a");
        Assert.Equal("#main", firstLink?.GetAttribute("href"));
        Assert.Contains("skip-link", firstLink!.ClassList);
    }

    [Fact]
    public void Index_LoadsApprovedFontsAndPhosphorIcons()
    {
        var html = ReadFile("index.html");

        Assert.Contains("family=Geist", html);
        Assert.Contains("@phosphor-icons/web", html);
        Assert.DoesNotMatch(new Regex("Material\\+Icons|Material\\+Symbols|material-icons", RegexOptions.IgnoreCase), html);
    }

    // =====================================================================
    // Contenedores obligatorios de la tarea
    // =====================================================================

    [Fact]
    public void Search_HasLabelledSearchInputAndHistory()
    {
        var doc = Page();

        var input = Required(doc, "input#search-input");
        Assert.Equal("search", input.GetAttribute("type"));
        Assert.NotNull(doc.QuerySelector("label[for='search-input']"));
        Required(doc, "#search-history");
        Required(doc, "#clear-history");
    }

    [Fact]
    public void Filters_CoverEveryClassRarityAffiliationAndSortOrder()
    {
        var doc = Page();

        var classValues = Required(doc, "select#filter-class").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToList();
        foreach (var cls in new[] { "Guard", "Sniper", "Medic", "Caster", "Defender", "Vanguard", "Supporter", "Specialist" })
            Assert.Contains(cls, classValues);
        Assert.Contains("", classValues);

        var rarityValues = Required(doc, "select#filter-rarity").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToList();
        for (int stars = 1; stars <= 6; stars++)
            Assert.Contains(stars.ToString(CultureInfo.InvariantCulture), rarityValues);

        Required(doc, "select#filter-affiliation");

        var sortValues = Required(doc, "select#sort-select").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToList();
        Assert.Equal(new[] { "name-asc", "name-desc", "rarity-desc", "rarity-asc" }, sortValues);

        foreach (var id in new[] { "filter-class", "filter-rarity", "filter-affiliation", "sort-select" })
            Assert.NotNull(doc.QuerySelector($"label[for='{id}']"));

        Required(doc, "#reset-filters");
        Required(doc, "#results-count");
    }

    [Fact]
    public void Grid_ShowsSkeletonsAndProvidesCardTemplate()
    {
        var doc = Page();

        var grid = Required(doc, "#operator-grid");
        Assert.Equal("polite", grid.GetAttribute("aria-live"));
        Assert.Equal("true", grid.GetAttribute("aria-busy"));
        Assert.True(grid.QuerySelectorAll(".card-skeleton").Length >= 10, "La grilla debe mostrar al menos 10 skeletons mientras carga.");

        var template = (IHtmlTemplateElement)Required(doc, "template#operator-card-template");
        var card = template.Content.QuerySelector("article.operator-card");
        Assert.NotNull(card);
        foreach (var part in new[] { "img.card-art", ".card-name", ".card-rarity", ".card-class", ".card-affiliation", ".card-description",
                     "button[data-action='favorite']", "button[data-action='compare']" })
            Assert.True(card!.QuerySelector(part) is not null, $"La plantilla de tarjeta no contiene '{part}'.");
    }

    [Fact]
    public void States_ProvideEmptyAndErrorViewsWithRetry()
    {
        var doc = Page();

        var empty = Required(doc, "#empty-state");
        Assert.True(empty.HasAttribute("hidden"));
        Assert.False(string.IsNullOrWhiteSpace(empty.TextContent));

        var error = Required(doc, "#error-state");
        Assert.True(error.HasAttribute("hidden"));
        Assert.Equal("alert", error.GetAttribute("role"));
        Assert.Equal("button", Required(error, "button#retry-button").GetAttribute("type"));
        Required(error, "#error-message");
    }

    [Fact]
    public void Favorites_HaveListEmptyStateAndNoteEditor()
    {
        var doc = Page();

        Required(doc, "section#favorites");
        Required(doc, "#favorites-list");
        Required(doc, "#favorites-empty");
        Required(doc, "#favorites-count");

        var modal = Required(doc, "dialog#note-modal");
        var textarea = Required(modal, "form textarea#note-text");
        Assert.Equal("500", textarea.GetAttribute("maxlength"));
        Assert.NotNull(modal.QuerySelector("label[for='note-text']"));
        Required(modal, "select#note-priority");
        Required(modal, "select#note-rating");
        Required(modal, "input#note-role");
        Required(modal, "button[type='submit']");
    }

    [Fact]
    public void StatusToast_IsPoliteLiveRegion()
    {
        var toast = Required(Page(), "#status-toast");
        Assert.Equal("status", toast.GetAttribute("role"));
        Assert.Equal("polite", toast.GetAttribute("aria-live"));
    }

    [Fact]
    public void StatsPanel_ExposesEveryDynamicMetric()
    {
        var doc = Page();

        var panel = Required(doc, "section#stats-panel");
        foreach (var stat in new[] { "total", "hp", "atk", "def", "cost" })
            Assert.True(panel.QuerySelector($"[data-stat='{stat}']") is not null, $"Falta la métrica {stat}.");
        Required(panel, "#class-distribution");
        Required(panel, "#rarity-distribution");
    }

    [Fact]
    public void Comparator_IsDialogWithTwoOperatorSelectors()
    {
        var doc = Page();

        var modal = Required(doc, "dialog#compare-modal");
        Required(modal, "select#compare-a");
        Required(modal, "select#compare-b");
        Required(modal, "#compare-results");
        Assert.NotNull(modal.QuerySelector("label[for='compare-a']"));
        Assert.NotNull(modal.QuerySelector("label[for='compare-b']"));
        Assert.NotEmpty(doc.QuerySelectorAll("[data-open-compare]"));
    }

    [Fact]
    public void ThemeToggle_IsAccessibleButton()
    {
        var toggle = Required(Page(), "button#theme-toggle");

        Assert.Equal("button", toggle.GetAttribute("type"));
        Assert.NotNull(toggle.GetAttribute("aria-pressed"));
        Assert.False(string.IsNullOrWhiteSpace(toggle.GetAttribute("aria-label")));
    }

    [Fact]
    public void Navigation_HasMobileMenuToggleAndCurrentItem()
    {
        var doc = Page();

        var toggle = Required(doc, "button#menu-toggle");
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Equal("site-menu", toggle.GetAttribute("aria-controls"));
        Required(doc, "#site-menu");
        Assert.NotNull(doc.QuerySelector("nav [aria-current='page']"));
    }

    [Fact]
    public void TaglineSection_HasAtLeastTwoLines()
    {
        var tagline = Required(Page(), "[data-tagline]");

        Assert.True(tagline.QuerySelectorAll("br").Length >= 1, "El tagline debe ocupar al menos dos líneas.");
        Assert.True(tagline.TextContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 10);
    }

    [Fact]
    public void Hero_HasSinglePrimaryCallToAction()
    {
        var hero = Required(Page(), "section#hero");

        Required(hero, "h1");
        Assert.Single(hero.QuerySelectorAll(".btn-primary"));
        Required(hero, ".hero-proof");
    }

    [Fact]
    public void Faq_HasSixToTwelveQuestions()
    {
        var count = Page().QuerySelectorAll("section#faq details").Length;
        Assert.InRange(count, 6, 12);
    }

    // =====================================================================
    // Páginas auxiliares, enlaces e imágenes
    // =====================================================================

    [Fact]
    public void Footer_LinksToPrivacyAndTermsPages()
    {
        var footer = Required(Page(), "footer");

        Assert.NotNull(footer.QuerySelector("a[href='privacidad.html']"));
        Assert.NotNull(footer.QuerySelector("a[href='terminos.html']"));
    }

    [Theory]
    [InlineData("404.html")]
    [InlineData("privacidad.html")]
    [InlineData("terminos.html")]
    public void AuxiliaryPages_ExistWithWayBack(string page)
    {
        var doc = Page(page);

        Assert.Equal("es", doc.DocumentElement.GetAttribute("lang"));
        Required(doc, "main");
        Assert.NotNull(doc.QuerySelector("a.btn-primary"));
    }

    [Fact]
    public void Favicon_FileExists()
    {
        var href = Required(Page(), "link[rel='icon']").GetAttribute("href")!;
        Assert.True(File.Exists(Path.Combine(Root, href)), $"No existe {href}.");
    }

    [Theory]
    [MemberData(nameof(PagesData))]
    public void Links_AreNeverDead(string page)
    {
        var doc = Page(page);

        foreach (var anchor in doc.QuerySelectorAll("a"))
        {
            var href = anchor.GetAttribute("href");
            Assert.False(string.IsNullOrWhiteSpace(href) || href == "#", $"Enlace muerto en {page}: {anchor.OuterHtml}");
            if (href!.StartsWith('#'))
                Assert.True(doc.GetElementById(href[1..]) is not null, $"El ancla {href} no existe en {page}.");
        }
    }

    [Theory]
    [MemberData(nameof(PagesData))]
    public void Images_AlwaysDeclareAlt(string page)
    {
        var doc = Page(page);
        var images = doc.QuerySelectorAll("img").Concat(
            doc.QuerySelectorAll("template").Cast<IHtmlTemplateElement>().SelectMany(t => t.Content.QuerySelectorAll("img")));

        Assert.All(images, img => Assert.True(img.HasAttribute("alt"), $"Imagen sin alt en {page}: {img.OuterHtml}"));
    }

    // =====================================================================
    // Reglas de redacción (SKILLS.md B1, A5, B8)
    // =====================================================================

    public static TheoryData<string> PagesData()
    {
        var data = new TheoryData<string>();
        foreach (var page in StaticPages) data.Add(page);
        return data;
    }

    private static IEnumerable<string> VisibleCopy(IHtmlDocument doc)
    {
        var roots = new List<INode> { doc.Body! };
        roots.AddRange(doc.QuerySelectorAll("template").Cast<IHtmlTemplateElement>().Select(t => (INode)t.Content));

        foreach (var root in roots)
        {
            foreach (var text in root.GetDescendants().OfType<IText>())
            {
                if (text.ParentElement?.LocalName is "script" or "style")
                    continue;
                if (!string.IsNullOrWhiteSpace(text.Data))
                    yield return text.Data.Trim();
            }
        }

        foreach (var element in doc.All)
        {
            foreach (var attr in new[] { "aria-label", "placeholder", "title", "alt" })
            {
                var value = element.GetAttribute(attr);
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value;
            }
        }
        yield return doc.Title ?? string.Empty;
        foreach (var meta in doc.QuerySelectorAll("meta[name='description'], meta[property^='og:'][content]"))
        {
            if (meta.GetAttribute("property") is "og:image" or "og:url" or "og:type") continue;
            yield return meta.GetAttribute("content") ?? string.Empty;
        }
    }

    [Theory]
    [MemberData(nameof(PagesData))]
    public void Copy_ContainsNoHyphensOrDashes(string page)
    {
        var offenders = VisibleCopy(Page(page)).Where(t => t.IndexOfAny(['-', '–', '—']) >= 0).ToList();
        Assert.True(offenders.Count == 0, $"Texto con guiones en {page}: {string.Join(" | ", offenders)}");
    }

    [Theory]
    [MemberData(nameof(PagesData))]
    public void Copy_AvoidsBannedFillerAndGenericButtons(string page)
    {
        string[] banned = ["revolucion", "supercharge", "cutting edge", "vanguardista", "sin esfuerzo", "effortless", "world class",
            "de clase mundial", "al siguiente nivel", "learn more", "más información", "submit", "lorem ipsum", "!"];
        var copy = string.Join(" ", VisibleCopy(Page(page))).ToLowerInvariant();

        Assert.All(banned, word => Assert.DoesNotContain(word, copy));
    }

    // =====================================================================
    // Sistema visual (SKILLS.md parte B)
    // =====================================================================

    [Fact]
    public void Css_IsResponsiveAndRespectsReducedMotion()
    {
        var css = Css();

        Assert.True(MinWidthMedia().Matches(css).Count >= 2, "Se esperan al menos dos breakpoints mobile first (min-width).");
        Assert.Contains("prefers-reduced-motion", css);
        Assert.Contains("[data-theme=\"light\"]", css);
    }

    [Fact]
    public void Css_UsesSingleApprovedTypefaceWithoutItalicsOrBlackWeights()
    {
        var families = Declarations().Where(d => d.Property is "font-family" or "--font-sans" or "--font-mono").Select(d => d.Value).ToList();
        Assert.NotEmpty(families);
        Assert.All(families, f => Assert.DoesNotMatch(new Regex(@"\b(Inter|Roboto|Arial|Open Sans|Helvetica)\b", RegexOptions.IgnoreCase), f));
        Assert.Contains(families, f => f.Contains("Geist"));

        Assert.DoesNotContain(Declarations(), d => d.Property == "font-style" && d.Value.Contains("italic"));
        Assert.DoesNotContain(Declarations(), d => d.Property == "font-weight" && d.Value is "800" or "900" or "bolder" or "black");
    }

    private static readonly HashSet<string> AllowedFontSizes =
        ["0.75rem", "0.875rem", "1rem", "1.125rem", "1.25rem", "1.5rem", "1.875rem", "2.25rem", "3rem", "3.75rem", "4.5rem", "6rem", "8rem"];

    [Fact]
    public void Css_FontSizesSnapToTailwindScale()
    {
        foreach (var (property, value) in Declarations())
        {
            if (property == "font-size")
                Assert.True(value.StartsWith("var(--text-") || AllowedFontSizes.Contains(value), $"font-size fuera de escala: {value}");
            if (property.StartsWith("--text-"))
                Assert.True(AllowedFontSizes.Contains(value), $"Token {property} fuera de escala: {value}");
        }
    }

    private static readonly HashSet<int> AllowedSpacing = [0, 2, 4, 8, 12, 16, 24, 32, 40, 48, 64, 80, 96];

    [Fact]
    public void Css_SpacingUsesOnlyTokenValues()
    {
        foreach (var (property, value) in Declarations())
        {
            bool isSpacing = SpacingProperty().IsMatch(property) || property.StartsWith("--space-");
            if (!isSpacing) continue;

            foreach (Match px in PixelValue().Matches(value))
                Assert.True(AllowedSpacing.Contains(int.Parse(px.Groups[1].Value)), $"Espaciado no permitido en {property}: {value}");
            Assert.DoesNotMatch(new Regex(@"\d(rem|em)\b"), value);
        }
    }

    // "inherit" reutiliza el radio del contenedor (capas de fondo y rellenos de barras).
    private static readonly HashSet<string> AllowedRadii = ["0", "2px", "4px", "6px", "8px", "12px", "16px", "24px", "9999px", "inherit"];

    [Fact]
    public void Css_RadiiUseTailwindValues()
    {
        foreach (var (property, value) in Declarations())
        {
            if (property.StartsWith("--radius-"))
                Assert.True(AllowedRadii.Contains(value), $"Radio no permitido: {property}: {value}");
            if (property.Contains("radius") && !property.StartsWith("--"))
                Assert.True(value.StartsWith("var(--radius-") || AllowedRadii.Contains(value), $"Radio no permitido: {value}");
        }
    }

    [Fact]
    public void Css_HasNoBackgroundGradientsOrSingleSidedBorders()
    {
        var css = Css();

        foreach (Match rule in CssRule().Matches(css))
        {
            var body = rule.Groups[2].Value;
            if (body.Contains("gradient("))
                Assert.True(body.Contains("background-clip: text"), $"Gradiente fuera de un texto en '{rule.Groups[1].Value.Trim()}'.");
        }
        Assert.DoesNotMatch(new Regex(@"border-(top|right|bottom|left)\s*:"), css);
    }

    [Fact]
    public void Css_DarkThemeUsesApprovedBackgrounds()
    {
        string[] allowed = ["#000000", "#181818", "#1f1f1f", "#272727", "#313131", "#131209"];
        var tokens = RootTokens();

        foreach (var key in new[] { "--bg", "--surface", "--surface-raised", "--surface-strong" })
        {
            Assert.True(tokens.ContainsKey(key), $"Falta el token {key} en :root.");
            Assert.Contains(tokens[key].ToLowerInvariant(), allowed);
        }
    }

    [Fact]
    public void Css_MotionUsesBaseCurveAndSafeProperties()
    {
        var tokens = RootTokens();
        Assert.Equal("cubic-bezier(0.22, 1, 0.36, 1)", tokens["--ease"]);
        foreach (var duration in tokens.Where(t => t.Key.StartsWith("--duration-")))
            Assert.True(int.Parse(duration.Value.Replace("ms", "")) >= 200, $"{duration.Key} es menor a 200ms.");

        string[] animatable = ["transform", "opacity", "color", "background-color", "border-color", "visibility"];
        foreach (var (property, value) in Declarations())
        {
            if (property is not ("transition" or "transition-property" or "transition-timing-function" or "animation" or "animation-timing-function"))
                continue;

            Assert.DoesNotMatch(new Regex(@"(?<![\w-])(ease|linear|ease-in|ease-out|ease-in-out)(?![\w-])"), value);
            foreach (Match ms in Milliseconds().Matches(value))
                Assert.True(int.Parse(ms.Groups[1].Value) >= 200 || ms.Groups[1].Value == "0", $"Duración menor a 200ms: {value}");

            if (property == "transition")
            {
                foreach (var part in value.Split(','))
                    Assert.Contains(part.Trim().Split(' ')[0], animatable);
            }
        }

        foreach (Match keyframes in Keyframes().Matches(Css()))
        {
            foreach (Match decl in CssDeclaration().Matches(keyframes.Groups[1].Value))
                Assert.Contains(decl.Groups[1].Value.Trim(), new[] { "transform", "opacity" });
        }
    }

    [Fact]
    public void Css_PreventsOrphansAndStylesHeroGradientText()
    {
        var css = Css();

        Assert.Contains("text-wrap: balance", css);
        Assert.Contains("text-wrap: pretty", css);
        Assert.Contains("#ffffff, #9b9b9b", css.ToLowerInvariant());
        Assert.Contains("#000000, #666666", css.ToLowerInvariant());
        Assert.Contains("max-width: 680px", css);
    }

    [Fact]
    public void Css_DefinesFocusActiveAndSkeletonStates()
    {
        var css = Css();

        Assert.Contains(":focus-visible", css);
        Assert.Contains(":active", css);
        Assert.Contains("scale(0.98)", css);
        Assert.Contains(".card-skeleton", css);
        Assert.Contains("backdrop-filter", css);
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CssComments();

    [GeneratedRegex(@"([-a-zA-Z]+)\s*:\s*([^;{}]+);")]
    private static partial Regex CssDeclaration();

    [GeneratedRegex(@":root\s*\{([^}]*)\}")]
    private static partial Regex RootRule();

    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}")]
    private static partial Regex CssRule();

    [GeneratedRegex(@"@media[^{]*min-width")]
    private static partial Regex MinWidthMedia();

    [GeneratedRegex(@"^(padding|margin|gap|row-gap|column-gap)(-[a-z-]+)?$")]
    private static partial Regex SpacingProperty();

    [GeneratedRegex(@"(?<![\w.-])(\d+)px")]
    private static partial Regex PixelValue();

    [GeneratedRegex(@"(?<![\w.])(\d+)ms")]
    private static partial Regex Milliseconds();

    [GeneratedRegex(@"@keyframes\s+[\w-]+\s*\{((?:[^{}]*\{[^{}]*\})*)\s*\}")]
    private static partial Regex Keyframes();
}
