/* =====================================================================
   Rhodes Terminal · script.js
   Capas:
     1. Configuración y errores
     2. Normalización del JSON irregular de RhodesAPI y del backend C#
     3. Datos: fetch() con tiempo límite, fallback híbrido y caché local
     4. Renderizado de tarjetas y estados (carga, vacío, error)
     5. Interfaz base: navegación, revelado al hacer scroll y tagline
     6. Arranque
   El archivo funciona en el navegador (window.RhodesTerminal) y en Node
   (module.exports) para poder probar cada capa por separado.
   ===================================================================== */
(function (root, factory) {
  "use strict";
  const api = factory();
  if (typeof module === "object" && module.exports) {
    module.exports = api;
  } else {
    root.RhodesTerminal = api;
    if (root.document) {
      const start = () => { root.RhodesTerminal.app = api.boot(root.document); };
      if (root.document.readyState === "loading") root.document.addEventListener("DOMContentLoaded", start);
      else start();
    }
  }
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";

  // ===================================================================
  // 1. Configuración y errores
  // ===================================================================

  const CONFIG = Object.freeze({
    // El backend C# sólo se consulta cuando la página corre en el equipo local.
    backendUrl: "http://localhost:5000/api/operators",
    // api.rhodesapi.com ya no resuelve DNS; esta es la instancia pública vigente.
    publicApiUrl: "https://rhodesapi.up.railway.app/api/operator",
    portraitMapUrl: "assets/data/operator-portraits.json",
    portraitBase: "https://raw.githubusercontent.com/yuanyan3060/ArknightsGameResource/main/portrait/",
    placeholderArt: "assets/operator-placeholder.svg",
    cacheKey: "rhodes.operators.v1",
    cacheTtlMs: 6 * 60 * 60 * 1000,
    backendTimeoutMs: 2500,
    publicTimeoutMs: 60000,
    portraitTimeoutMs: 8000,
    skeletonCount: 10
  });

  const CLASSES = ["Vanguard", "Guard", "Defender", "Sniper", "Caster", "Medic", "Supporter", "Specialist"];

  const CLASS_ICONS = {
    Vanguard: "ph-flag-banner",
    Guard: "ph-sword",
    Defender: "ph-shield",
    Sniper: "ph-crosshair",
    Caster: "ph-magic-wand",
    Medic: "ph-first-aid-kit",
    Supporter: "ph-hand-heart",
    Specialist: "ph-lightning"
  };

  const SOURCE_LABELS = { backend: "backend C# local", rhodesapi: "RhodesAPI", cache: "copia local" };

  /** Error de la capa de datos. kind: http | timeout | network | parse. */
  class ApiError extends Error {
    constructor(message, { kind = "http", status = null, source = null, cause } = {}) {
      super(message, cause ? { cause } : undefined);
      this.name = "ApiError";
      this.kind = kind;
      this.status = status;
      this.source = source;
    }
  }

  // ===================================================================
  // 2. Normalización
  // ===================================================================

  /** Extrae el primer número de valores como 12, " 1", "09" o "block: 2". */
  function parseNumber(value) {
    if (typeof value === "number") return Number.isFinite(value) ? value : null;
    if (typeof value !== "string") return null;
    const match = value.match(/-?\d+(?:\.\d+)?/);
    return match ? Number(match[0]) : null;
  }

  function cleanText(value) {
    return typeof value === "string" ? value.trim() : "";
  }

  /** Campos que RhodesAPI entrega como texto o como arreglo de textos. */
  function toList(value) {
    const items = Array.isArray(value) ? value : [value];
    return items.map(cleanText).filter(Boolean);
  }

  function slugify(name) {
    return name
      .normalize("NFKD")
      .replace(/[̀-ͯ]/g, "")
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "");
  }

  const STAT_TIERS = [["e2max", "Elite2"], ["e1max", "Elite1"], ["e0max", "Base"]];

  /**
   * Elige el tier más alto con HP válido. "base" aporta RES, coste, intervalo y redespliegue,
   * que los bloques eXmax no traen.
   */
  function normalizeStats(statistics) {
    const tiers = statistics && typeof statistics === "object" ? statistics : {};
    const base = tiers.base && typeof tiers.base === "object" ? tiers.base : {};
    let tier = null;
    let promotion = "Base";

    for (const [key, level] of STAT_TIERS) {
      const candidate = tiers[key];
      if (candidate && parseNumber(candidate.hp) > 0) {
        tier = candidate;
        promotion = level;
        break;
      }
    }
    if (!tier) tier = parseNumber(base.hp) > 0 ? base : null;
    // Sin ningún tier válido (RhodesAPI devuelve {"error": ...} para operadores sin datos) no hay estadísticas.
    if (!tier) return null;

    const num = (value, fallback = 0) => parseNumber(value) ?? fallback;
    const interval = parseNumber(base.interval);
    return {
      promotion,
      stats: {
        hp: num(tier.hp),
        atk: num(tier.atk),
        def: num(tier.def),
        res: num(base.resist),
        cost: num(base.cost),
        block: parseNumber(tier.block) ?? num(base.block, 1),
        interval: interval > 0 ? interval : 1,
        redeploy: num(base.deploy, 70)
      }
    };
  }

  function portraitUrl(file, portraitMap) {
    return `${(portraitMap && portraitMap.base) || CONFIG.portraitBase}${file}.png`;
  }

  /**
   * Los enlaces de RhodesAPI apuntan a ak.gamepress.gg, que dejó de existir.
   * Orden de resolución: mapa verificado por nombre, charId del enlace y placeholder local.
   */
  function resolveArt(name, links, rarity, portraitMap) {
    const mapped = portraitMap && portraitMap.portraits && portraitMap.portraits[name];
    if (mapped) return portraitUrl(mapped, portraitMap);

    const charId = links.join(" ").match(/char_\d+_[a-z0-9]+/i);
    if (charId) return portraitUrl(`${charId[0].toLowerCase()}${rarity >= 4 ? "_2" : "_1"}`, portraitMap);

    const usable = links.find(link => /^https?:\/\//.test(link) && !link.includes("gamepress.gg"));
    return usable || CONFIG.placeholderArt;
  }

  function findClass(value) {
    const wanted = cleanText(value).toLowerCase();
    return CLASSES.find(cls => cls.toLowerCase() === wanted) || null;
  }

  function isValidRarity(rarity) {
    return Number.isInteger(rarity) && rarity >= 1 && rarity <= 6;
  }

  /** Convierte un registro crudo de RhodesAPI al modelo de la vista, o null si no es utilizable. */
  function normalizeOperator(raw, portraitMap) {
    if (!raw || typeof raw !== "object" || Array.isArray(raw)) return null;

    const name = cleanText(raw.name);
    const rarity = parseNumber(raw.rarity);
    const classes = toList(raw.class);
    const className = findClass(classes[0]);
    if (!name || !isValidRarity(rarity) || !className) return null;

    const normalized = normalizeStats(raw.statistics);
    if (!normalized) return null;
    const { promotion, stats } = normalized;

    const affiliations = toList(raw.affiliation);
    const links = Array.isArray(raw.art)
      ? raw.art.filter(item => item && typeof item.link === "string").map(item => item.link)
      : [];
    const biography = cleanText(raw.biography);

    return {
      id: slugify(name),
      name,
      rarity,
      className,
      archetype: classes.length > 1 ? classes[classes.length - 1] : "",
      affiliation: affiliations[0] || "",
      affiliations,
      description: cleanText(raw.description) || biography,
      biography,
      art: resolveArt(name, links, rarity, portraitMap),
      promotion,
      stats,
      combatPower: null
    };
  }

  /** Convierte el OperatorDto del backend C# al mismo modelo de la vista. */
  function normalizeBackendOperator(dto, portraitMap) {
    if (!dto || typeof dto !== "object") return null;

    const name = cleanText(dto.name);
    const rarity = parseNumber(dto.rarity);
    const className = findClass(dto.class);
    if (!name || !isValidRarity(rarity) || !className) return null;

    const stats = dto.stats || {};
    const num = (value, fallback = 0) => parseNumber(value) ?? fallback;
    const affiliation = cleanText(dto.affiliation);
    const biography = cleanText(dto.biography);
    const artUrl = cleanText(dto.artUrl);

    return {
      id: cleanText(dto.id) || slugify(name),
      name,
      rarity,
      className,
      archetype: cleanText(dto.archetype),
      affiliation,
      affiliations: affiliation ? [affiliation] : [],
      description: cleanText(dto.description) || biography,
      biography,
      art: resolveArt(name, artUrl ? [artUrl] : [], rarity, portraitMap),
      promotion: cleanText(dto.promotion) || "Base",
      stats: {
        hp: num(stats.hp),
        atk: num(stats.atk),
        def: num(stats.def),
        res: num(stats.res),
        cost: num(stats.cost),
        block: num(stats.block, 1),
        interval: num(stats.attackInterval, 1) || 1,
        redeploy: num(stats.redeployTime, 70)
      },
      combatPower: parseNumber(dto.combatPower)
    };
  }

  /** Normaliza una respuesta completa: descarta inválidos y duplicados y ordena por nombre. */
  function normalizeList(payload, source, portraitMap) {
    if (!Array.isArray(payload)) {
      throw new ApiError("La respuesta no contiene una lista de operadores.", { kind: "parse", source });
    }
    const normalize = source === "backend" ? normalizeBackendOperator : normalizeOperator;
    const byId = new Map();
    for (const raw of payload) {
      const op = normalize(raw, portraitMap);
      if (op && !byId.has(op.id)) byId.set(op.id, op);
    }
    return [...byId.values()].sort((a, b) => compareNames(a.name, b.name));
  }

  /** Orden alfabético que ignora comillas y acentos ('Justice Knight' va con la J). */
  function compareNames(a, b) {
    return a.localeCompare(b, "es", { sensitivity: "base", ignorePunctuation: true });
  }

  // ===================================================================
  // 3. Datos: fetch(), fallback híbrido y caché
  // ===================================================================

  /** fetch() con tiempo límite que siempre falla con un ApiError descriptivo. */
  async function fetchJson(url, { fetchImpl = globalThis.fetch, timeoutMs = 15000, source = null } = {}) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    const timeoutError = cause =>
      new ApiError(`${url} no respondió en ${timeoutMs} ms.`, { kind: "timeout", source, cause });

    try {
      let response;
      try {
        response = await fetchImpl(url, { signal: controller.signal, headers: { Accept: "application/json" } });
      } catch (error) {
        if (controller.signal.aborted || (error && error.name === "AbortError")) throw timeoutError(error);
        throw new ApiError(`No se pudo conectar con ${url}.`, { kind: "network", source, cause: error });
      }

      if (!response.ok) {
        throw new ApiError(`${url} respondió ${response.status}.`, { kind: "http", status: response.status, source });
      }

      try {
        return await response.json();
      } catch (error) {
        if (controller.signal.aborted) throw timeoutError(error);
        throw new ApiError(`${url} envió una respuesta que no es JSON.`, { kind: "parse", source, cause: error });
      }
    } finally {
      clearTimeout(timer);
    }
  }

  /** El mapa de retratos es opcional: si falla, el catálogo carga igual con el charId o el placeholder. */
  async function loadPortraitMap(fetchImpl) {
    try {
      return await fetchJson(CONFIG.portraitMapUrl, { fetchImpl, timeoutMs: CONFIG.portraitTimeoutMs });
    } catch {
      return null;
    }
  }

  function canUseBackend(location) {
    if (!location) return false;
    return location.protocol === "file:" || ["localhost", "127.0.0.1", "[::1]"].includes(location.hostname);
  }

  function readCache(storage) {
    try {
      const cached = JSON.parse(storage.getItem(CONFIG.cacheKey));
      if (cached && Array.isArray(cached.operators) && cached.operators.length && Number.isFinite(cached.savedAt)) return cached;
    } catch {
      /* caché ausente o corrupta */
    }
    return null;
  }

  function writeCache(storage, operators, savedAt) {
    try {
      storage.setItem(CONFIG.cacheKey, JSON.stringify({ savedAt, operators }));
    } catch {
      /* almacenamiento lleno o bloqueado: la app sigue sin caché */
    }
  }

  /**
   * Estrategia híbrida:
   *   1. Caché local vigente (evita descargar 5 MB en cada visita).
   *   2. Backend C# en http://localhost:5000, sólo en desarrollo local.
   *   3. RhodesAPI pública (la fuente en GitHub Pages).
   *   4. Caché vencida como último recurso, marcada como desactualizada.
   */
  async function loadOperators({ fetchImpl = globalThis.fetch, storage, location, forceRefresh = false, now = Date.now } = {}) {
    const cached = storage ? readCache(storage) : null;
    if (!forceRefresh && cached && now() - cached.savedAt < CONFIG.cacheTtlMs) {
      return { operators: cached.operators, source: "cache", stale: false, savedAt: cached.savedAt };
    }

    const portraitMap = await loadPortraitMap(fetchImpl);
    const attempts = [];
    if (canUseBackend(location)) {
      attempts.push({ source: "backend", url: CONFIG.backendUrl, timeoutMs: CONFIG.backendTimeoutMs });
    }
    attempts.push({ source: "rhodesapi", url: CONFIG.publicApiUrl, timeoutMs: CONFIG.publicTimeoutMs });

    let lastError = null;
    for (const attempt of attempts) {
      try {
        const payload = await fetchJson(attempt.url, { fetchImpl, timeoutMs: attempt.timeoutMs, source: attempt.source });
        const operators = normalizeList(payload, attempt.source, portraitMap);
        if (operators.length === 0) {
          throw new ApiError("La respuesta no trae operadores válidos.", { kind: "parse", source: attempt.source });
        }
        if (storage) writeCache(storage, operators, now());
        return { operators, source: attempt.source, stale: false, savedAt: now() };
      } catch (error) {
        lastError = error;
      }
    }

    if (cached) return { operators: cached.operators, source: "cache", stale: true, savedAt: cached.savedAt };
    throw lastError;
  }

  /** Mensaje visible para cada tipo de fallo. */
  function describeError(error) {
    if (!(error instanceof ApiError)) {
      return "Algo falló al procesar la respuesta de RhodesAPI. Intenta otra vez en unos segundos.";
    }
    switch (error.kind) {
      case "http":
        if (error.status === 404) return "RhodesAPI no encontró el catálogo (404). Es posible que la dirección de la API haya cambiado.";
        if (error.status >= 500) return `RhodesAPI respondió con un error interno (${error.status}). Suele resolverse solo; intenta otra vez en un minuto.`;
        return `RhodesAPI rechazó la solicitud (${error.status}). Intenta otra vez en unos segundos.`;
      case "timeout":
        return "RhodesAPI tardó demasiado en responder. El catálogo pesa unos 5 MB; revisa tu conexión e intenta otra vez.";
      case "network":
        return "No hay conexión con RhodesAPI. Revisa tu red o tus bloqueadores e intenta otra vez.";
      default:
        return "RhodesAPI envió una respuesta que no se pudo leer. Intenta otra vez en unos minutos.";
    }
  }

  // ===================================================================
  // 4. Renderizado
  // ===================================================================

  const numberFormat = new Intl.NumberFormat("es-ES", { maximumFractionDigits: 2, useGrouping: "always" });

  function formatNumber(value) {
    return numberFormat.format(value);
  }

  function rarityLabel(rarity) {
    return `${rarity} ${rarity === 1 ? "estrella" : "estrellas"}`;
  }

  /** Sustituye una imagen rota por el placeholder una sola vez. */
  function withFallbackImage(img, src) {
    img.addEventListener("error", () => {
      if (img.getAttribute("src") !== CONFIG.placeholderArt) img.setAttribute("src", CONFIG.placeholderArt);
    });
    img.setAttribute("src", src || CONFIG.placeholderArt);
  }

  function createCard(doc, op, favorites) {
    const template = doc.getElementById("operator-card-template");
    const card = template.content.firstElementChild.cloneNode(true);
    const $ = selector => card.querySelector(selector);

    card.dataset.operatorId = op.id;
    card.dataset.class = op.className;

    const img = $(".card-art");
    img.setAttribute("alt", `Retrato de ${op.name}`);
    withFallbackImage(img, op.art);

    const rarity = $(".card-rarity");
    rarity.textContent = `${op.rarity}★`;
    rarity.setAttribute("aria-label", rarityLabel(op.rarity));

    $(".card-class i").className = `ph ${CLASS_ICONS[op.className] || "ph-user"}`;
    $(".card-class span").textContent = op.className;
    $(".card-name").textContent = op.name;
    $(".card-affiliation").textContent = op.affiliation || "Sin afiliación registrada";
    $(".card-description").textContent = op.description || "RhodesAPI no tiene descripción para este operador.";

    for (const field of ["hp", "atk", "def", "cost"]) {
      card.querySelector(`[data-field='${field}']`).textContent = formatNumber(op.stats[field]);
    }

    for (const button of card.querySelectorAll("[data-action]")) button.dataset.operatorId = op.id;
    const favoriteButton = $("[data-action='favorite']");
    const isFavorite = favorites.has(op.id);
    favoriteButton.setAttribute("aria-pressed", String(isFavorite));
    favoriteButton.setAttribute("aria-label", `${isFavorite ? "Quitar de" : "Guardar en"} favoritos a ${op.name}`);
    $("[data-action='compare']").setAttribute("aria-label", `Comparar a ${op.name}`);
    return card;
  }

  /** Pinta las tarjetas y devuelve cuántas se mostraron. */
  function renderOperators(doc, operators, { favorites = new Set() } = {}) {
    const grid = doc.getElementById("operator-grid");
    const fragment = doc.createDocumentFragment();
    for (const op of operators) fragment.appendChild(createCard(doc, op, favorites));
    grid.replaceChildren(fragment);
    grid.setAttribute("aria-busy", "false");
    showState(doc, operators.length ? "ready" : "empty");
    return operators.length;
  }

  function renderSkeletons(doc, count = CONFIG.skeletonCount) {
    const grid = doc.getElementById("operator-grid");
    const skeletons = Array.from({ length: count }, () => {
      const item = doc.createElement("div");
      item.className = "card-skeleton";
      item.setAttribute("aria-hidden", "true");
      for (const cls of ["sk sk-media", "sk sk-line", "sk sk-line short", "sk sk-block"]) {
        const span = doc.createElement("span");
        span.className = cls;
        item.appendChild(span);
      }
      return item;
    });
    grid.replaceChildren(...skeletons);
  }

  /** Alterna entre los estados loading, ready, empty y error de la grilla. */
  function showState(doc, state, message) {
    const grid = doc.getElementById("operator-grid");
    const empty = doc.getElementById("empty-state");
    const error = doc.getElementById("error-state");
    const count = doc.getElementById("results-count");

    grid.hidden = state === "empty" || state === "error";
    empty.hidden = state !== "empty";
    error.hidden = state !== "error";

    if (state === "loading") {
      if (!grid.querySelector(".operator-card")) renderSkeletons(doc);
      grid.setAttribute("aria-busy", "true");
      count.textContent = "Cargando operadores";
    } else if (state === "error") {
      grid.setAttribute("aria-busy", "false");
      doc.getElementById("error-message").textContent = message || describeError(null);
      count.textContent = "No se pudo cargar el catálogo";
    } else if (state === "empty") {
      count.textContent = "Ningún operador coincide";
    }
  }

  function setResultsCount(doc, shown, { source, stale } = {}) {
    const origin = SOURCE_LABELS[source] ? ` · datos de ${SOURCE_LABELS[source]}` : "";
    const warning = stale ? " (copia guardada, RhodesAPI no respondió)" : "";
    doc.getElementById("results-count").textContent =
      `${formatNumber(shown)} ${shown === 1 ? "operador" : "operadores"}${origin}${warning}`;
  }

  /** Rellena el total del hero y la vista previa con tres operadores de 6 estrellas. */
  function updateHero(doc, operators) {
    const total = doc.getElementById("hero-total");
    if (total) total.textContent = formatNumber(operators.length);

    const list = doc.getElementById("hero-showcase");
    if (!list) return;
    const picks = [...operators]
      .sort((a, b) => b.rarity - a.rarity || compareNames(a.name, b.name))
      .filter(op => op.art !== CONFIG.placeholderArt)
      .slice(0, 3);

    list.replaceChildren(...picks.map(op => {
      const item = doc.createElement("li");
      item.className = "showcase-item";
      const img = doc.createElement("img");
      img.setAttribute("alt", "");
      img.setAttribute("width", "48");
      img.setAttribute("height", "48");
      withFallbackImage(img, op.art);
      const text = doc.createElement("div");
      const name = doc.createElement("strong");
      name.textContent = op.name;
      const meta = doc.createElement("span");
      meta.textContent = `${op.rarity}★ · ${op.className} · ${op.affiliation || "Sin afiliación"}`;
      text.append(name, meta);
      item.append(img, text);
      return item;
    }));
  }

  // ===================================================================
  // 5. Interfaz base
  // ===================================================================

  /** Menú móvil de pantalla completa y resaltado de la sección visible. */
  function initNavigation(doc) {
    const toggle = doc.getElementById("menu-toggle");
    const menu = doc.getElementById("site-menu");
    if (!toggle || !menu) return;

    const setOpen = open => {
      toggle.setAttribute("aria-expanded", String(open));
      toggle.setAttribute("aria-label", open ? "Cerrar menú" : "Abrir menú");
      menu.classList.toggle("is-open", open);
      doc.body.style.overflow = open ? "hidden" : "";
    };

    toggle.addEventListener("click", () => setOpen(toggle.getAttribute("aria-expanded") !== "true"));
    for (const link of menu.querySelectorAll("a")) link.addEventListener("click", () => setOpen(false));
    doc.addEventListener("keydown", event => {
      if (event.key === "Escape" && toggle.getAttribute("aria-expanded") === "true") {
        setOpen(false);
        toggle.focus();
      }
    });

    const view = doc.defaultView;
    if (!view || !("IntersectionObserver" in view)) return;
    const links = new Map([...menu.querySelectorAll("a[href^='#']")].map(link => [link.getAttribute("href").slice(1), link]));
    const observer = new view.IntersectionObserver(entries => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        for (const link of links.values()) link.removeAttribute("aria-current");
        links.get(entry.target.id).setAttribute("aria-current", "page");
      }
    }, { rootMargin: "-45% 0px -50% 0px" });
    for (const id of links.keys()) {
      const section = doc.getElementById(id);
      if (section) observer.observe(section);
    }
  }

  /** Las secciones .reveal suben 32px y aparecen una sola vez al entrar en pantalla. */
  function initReveal(doc) {
    const sections = [...doc.querySelectorAll(".reveal")];
    const view = doc.defaultView;
    if (!view || !("IntersectionObserver" in view)) {
      for (const section of sections) section.classList.add("is-visible");
      return;
    }
    const observer = new view.IntersectionObserver(entries => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        entry.target.classList.add("is-visible");
        observer.unobserve(entry.target);
      }
    }, { threshold: 0.12 });
    for (const section of sections) observer.observe(section);
  }

  /** Envuelve cada palabra del tagline en un span, conservando los saltos de línea. */
  function splitTagline(doc) {
    const tagline = doc.querySelector("[data-tagline]");
    if (!tagline || tagline.querySelector(".tagline-word")) return [];

    const fragment = doc.createDocumentFragment();
    const words = [];
    for (const node of [...tagline.childNodes]) {
      if (node.nodeType !== 3) {
        fragment.appendChild(node.cloneNode(true));
        continue;
      }
      for (const word of node.textContent.match(/\S+/g) || []) {
        const span = doc.createElement("span");
        span.className = "tagline-word";
        span.textContent = word;
        if (fragment.lastChild && fragment.lastChild.nodeName === "SPAN") fragment.appendChild(doc.createTextNode(" "));
        fragment.appendChild(span);
        words.push(span);
      }
    }
    tagline.replaceChildren(fragment);
    return words;
  }

  /**
   * Cada palabra se activa al cruzar una línea a 65% de la pantalla. Las que cruzan en el mismo
   * cuadro (una línea completa) se escalonan 60ms para que se lean una a una.
   */
  function initTaglineReveal(doc, words) {
    const view = doc.defaultView;
    if (!view || !("IntersectionObserver" in view) || words.length === 0) {
      for (const word of words) word.classList.add("is-active");
      return;
    }
    const order = new Map(words.map((word, index) => [word, index]));
    const observer = new view.IntersectionObserver(entries => {
      entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => order.get(a.target) - order.get(b.target))
        .forEach((entry, step) => {
          entry.target.style.transitionDelay = `${Math.min(step * 60, 400)}ms`;
          entry.target.classList.add("is-active");
          observer.unobserve(entry.target);
        });
    }, { rootMargin: "0px 0px -35% 0px" });
    for (const word of words) observer.observe(word);
  }

  // ===================================================================
  // 6. Arranque
  // ===================================================================

  /** localStorage cuando está disponible; si el navegador lo bloquea, un almacén en memoria. */
  function safeStorage() {
    try {
      const storage = globalThis.localStorage;
      storage.setItem("rhodes.probe", "1");
      storage.removeItem("rhodes.probe");
      return storage;
    } catch {
      const data = new Map();
      return {
        getItem: key => (data.has(key) ? data.get(key) : null),
        setItem: (key, value) => data.set(key, String(value)),
        removeItem: key => data.delete(key)
      };
    }
  }

  function boot(doc, {
    fetchImpl = globalThis.fetch && globalThis.fetch.bind(globalThis),
    storage = safeStorage(),
    location = globalThis.location
  } = {}) {
    doc.documentElement.classList.add("js");
    initNavigation(doc);
    initReveal(doc);
    initTaglineReveal(doc, splitTagline(doc));

    const state = { operators: [], source: null, stale: false };
    const controller = { state, ready: Promise.resolve(), reload };

    async function reload({ forceRefresh = false } = {}) {
      showState(doc, "loading");
      try {
        const result = await loadOperators({ fetchImpl, storage, location, forceRefresh });
        Object.assign(state, { operators: result.operators, source: result.source, stale: result.stale });
        renderOperators(doc, result.operators);
        setResultsCount(doc, result.operators.length, result);
        updateHero(doc, result.operators);
      } catch (error) {
        showState(doc, "error", describeError(error));
      }
    }

    const retry = doc.getElementById("retry-button");
    if (retry) retry.addEventListener("click", () => { controller.ready = reload({ forceRefresh: true }); });

    controller.ready = reload();
    return controller;
  }

  return {
    CONFIG,
    CLASSES,
    ApiError,
    parseNumber,
    normalizeStats,
    normalizeOperator,
    normalizeBackendOperator,
    normalizeList,
    fetchJson,
    loadOperators,
    describeError,
    formatNumber,
    renderOperators,
    showState,
    setResultsCount,
    updateHero,
    initNavigation,
    splitTagline,
    boot
  };
});
