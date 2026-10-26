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

    [Theory]
    [MemberData(nameof(PagesData))]
    public void Pages_LoadTheDistinctiveFontTrio(string page)
    {
        var html = ReadFile(page);

        Assert.Contains("family=Big+Shoulders+Display", html);
        Assert.Contains("family=Familjen+Grotesk", html);
        Assert.Contains("family=Martian+Mono", html);
        Assert.DoesNotMatch(new Regex(@"family=(Inter|Roboto|Geist|Space\+Grotesk|Open\+Sans)\b"), html);
    }

    [Fact]
    public void Index_UsesPhosphorIconsOnly()
    {
        var html = ReadFile("index.html");

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
    // Sistema visual: docs/design/frontend-aesthetics.md
    // (reemplaza a SKILLS.md en tipografía, color, fondos, esquinas y movimiento)
    // =====================================================================

    private static readonly Regex GenericFonts = new(
        @"\b(Inter|Roboto|Arial|Helvetica|Open Sans|Space Grotesk|Segoe UI|system-ui|BlinkMacSystemFont|Geist)\b|-apple-system",
        RegexOptions.IgnoreCase);

    private static readonly string[] ThemeKeys = ["--ink-0", "--ink-1", "--ink-2", "--ink-3", "--line", "--fg", "--fg-muted", "--accent", "--hot"];

    private static Dictionary<string, string> LightTokens()
    {
        var match = LightRule().Match(Css());
        Assert.True(match.Success, "style.css debe redefinir los tokens en :root[data-theme=\"light\"].");
        return CssDeclaration().Matches(match.Groups[1].Value)
            .Where(m => m.Groups[1].Value.Trim().StartsWith("--"))
            .ToDictionary(m => m.Groups[1].Value.Trim(), m => m.Groups[2].Value.Trim());
    }

    /// <summary>La hoja sin los bloques de tokens: aquí no debe quedar ningún color literal.</summary>
    private static string CssWithoutTokenBlocks() => LightRule().Replace(RootRule().Replace(Css(), "", 1), "", 1);

    [Fact]
    public void Css_IsResponsiveAndRespectsReducedMotion()
    {
        var css = Css();

        Assert.True(MinWidthMedia().Matches(css).Count >= 2, "Se esperan al menos dos breakpoints mobile first (min-width).");
        Assert.Contains("prefers-reduced-motion", css);
        Assert.Contains("[data-theme=\"light\"]", css);
    }

    [Fact]
    public void Css_UsesDistinctiveFontTrioAndNoGenericFamilies()
    {
        var tokens = RootTokens();
        Assert.Contains("Big Shoulders Display", tokens["--font-display"]);
        Assert.Contains("Familjen Grotesk", tokens["--font-body"]);
        Assert.Contains("Martian Mono", tokens["--font-mono"]);

        foreach (var (property, value) in Declarations())
        {
            if (property.StartsWith("--font-"))
                Assert.DoesNotMatch(GenericFonts, value);
            if (property == "font-family")
                Assert.True(value.StartsWith("var(--font-") || value == "inherit", $"font-family debe usar un token: {value}");
        }
        Assert.DoesNotContain(Declarations(), d => d.Property == "font-style" && d.Value.Contains("italic"));
    }

    [Fact]
    public void Css_FontSizesUseTypeTokens()
    {
        var tokens = RootTokens().Where(t => t.Key.StartsWith("--fs-")).ToList();
        Assert.True(tokens.Count >= 7, "Se espera una escala tipográfica de al menos 7 tokens --fs-*.");
        Assert.All(tokens, t => Assert.Matches(@"^(\d*\.?\d+rem|clamp\(.+\))$", t.Value));

        foreach (var (property, value) in Declarations())
        {
            if (property == "font-size")
                Assert.True(value.StartsWith("var(--fs-") || value == "inherit", $"font-size fuera de la escala: {value}");
        }
    }

    [Fact]
    public void Css_SpacingUsesSpaceTokens()
    {
        Assert.True(RootTokens().Count(t => t.Key.StartsWith("--sp-")) >= 8, "Se esperan al menos 8 tokens de espaciado --sp-*.");

        foreach (var (property, value) in Declarations())
        {
            if (!SpacingProperty().IsMatch(property)) continue;
            var rest = SpaceToken().Replace(value, " ");
            rest = Regex.Replace(rest, @"\b(auto|0)\b", " ");
            Assert.True(rest.Trim().Length == 0, $"Espaciado sin token en {property}: {value}");
        }
    }

    [Fact]
    public void Css_UsesChamferedCornersInsteadOfRoundedOnes()
    {
        var css = Css();

        Assert.True(Regex.Matches(css, @"clip-path:\s*polygon\([^;]*var\(--cut").Count >= 4,
            "Las piezas principales deben usar esquinas biseladas con clip-path y la variable --cut.");
        foreach (var (property, value) in Declarations())
        {
            if (property.Contains("radius") && !property.StartsWith("--"))
                Assert.True(value is "0" or "50%", $"Solo se permiten esquinas rectas o círculos: {property}: {value}");
        }
    }

    [Fact]
    public void Css_BuildsAnAtmosphericLayeredBackground()
    {
        var body = CssRule().Matches(Css()).First(m => m.Groups[1].Value.Trim() == "body").Groups[2].Value;
        var image = CssDeclaration().Matches(body).First(m => m.Groups[1].Value.Trim() == "background-image").Groups[2].Value;

        Assert.True(Regex.Matches(image, "gradient\\(").Count >= 3, "El fondo debe combinar al menos tres capas de degradados.");
        Assert.True(Regex.Matches(Css(), "repeating-linear-gradient\\(").Count >= 2, "Faltan las bandas de patrón geométrico.");
    }

    [Fact]
    public void Css_ColorsLiveInThemeTokens()
    {
        var dark = RootTokens();
        var light = LightTokens();
        foreach (var key in ThemeKeys)
        {
            Assert.True(dark.ContainsKey(key), $"Falta {key} en el tema oscuro.");
            Assert.True(light.ContainsKey(key), $"Falta {key} en el tema claro.");
        }
        Assert.NotEqual(dark["--ink-0"], light["--ink-0"]);

        foreach (Match decl in CssDeclaration().Matches(CssWithoutTokenBlocks()))
        {
            var value = decl.Groups[2].Value;
            Assert.DoesNotMatch(new Regex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\("), value);
        }
    }

    [Fact]
    public void Css_OrchestratesAStaggeredPageLoad()
    {
        var css = Css();
        var tokens = RootTokens();

        Assert.StartsWith("cubic-bezier(", tokens["--ease-out"]);
        Assert.True(Keyframes().Matches(css).Count >= 3, "Se esperan al menos tres animaciones @keyframes.");
        var delays = Declarations().Where(d => d.Property == "animation-delay").Select(d => d.Value).ToList();
        Assert.Contains(delays, v => v.Contains("var(--d"));
        Assert.Contains(delays, v => v.Contains("var(--i"));

        foreach (var (property, value) in Declarations())
        {
            if (property is "transition" or "transition-property")
                Assert.DoesNotMatch(new Regex(@"(?<![\w-])(width|height|top|left|all)(?![\w-])"), value);
        }
        foreach (Match keyframes in Keyframes().Matches(css))
        {
            foreach (Match decl in CssDeclaration().Matches(keyframes.Groups[1].Value))
                Assert.Contains(decl.Groups[1].Value.Trim(), new[] { "transform", "opacity", "clip-path" });
        }
    }

    [Fact]
    public void Hero_StaggersItsEntrance()
    {
        var hero = Required(Page(), "section#hero");
        Assert.True(hero.QuerySelectorAll("[style*='--d:']").Length >= 4, "El hero debe escalonar al menos cuatro elementos con --d.");
        Assert.Equal(3, hero.QuerySelectorAll("h1 .hero-line").Length);
    }

    [Fact]
    public void Css_PreventsOrphansAndOutlinesAHeroLine()
    {
        var css = Css();

        Assert.Contains("text-wrap: balance", css);
        Assert.Contains("text-wrap: pretty", css);
        Assert.Contains("-webkit-text-stroke", css);
    }

    [Fact]
    public void Css_DefinesFocusActiveAndSkeletonStates()
    {
        var css = Css();

        Assert.Contains(":focus-visible", css);
        Assert.Contains(":active", css);
        Assert.Contains(".card-skeleton", css);
        Assert.Contains("backdrop-filter", css);
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CssComments();

    // Los nombres de propiedad admiten dígitos para leer tokens como --ink-0 o --sp-1.
    [GeneratedRegex(@"([-a-zA-Z0-9]+)\s*:\s*([^;{}]+);")]
    private static partial Regex CssDeclaration();

    [GeneratedRegex(@":root\s*\{([^}]*)\}")]
    private static partial Regex RootRule();

    [GeneratedRegex(@":root\[data-theme=""light""\]\s*\{([^}]*)\}")]
    private static partial Regex LightRule();

    [GeneratedRegex(@"var\(--sp-[\w-]+\)")]
    private static partial Regex SpaceToken();

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
