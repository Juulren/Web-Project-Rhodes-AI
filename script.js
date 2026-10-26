/* =====================================================================
   Rhodes Terminal · script.js
   Capas:
     1. Configuración y errores
     2. Normalización del JSON irregular de RhodesAPI y del backend C#
     3. Datos: fetch() con tiempo límite, fallback híbrido y caché local
     4. Búsqueda y filtros combinados
     5. Favoritos: CRUD en localStorage y sincronización con el backend C#
     6. Renderizado de tarjetas, favoritos y estados (carga, vacío, error)
     7. Interfaz base: navegación, revelado al hacer scroll y tagline
     8. Arranque
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
    skeletonCount: 10,
    searchDelayMs: 150,
    favoritesKey: "rhodes.favorites.v1",
    favoritesApiUrl: "http://localhost:5000/api/favorites",
    maxNotesLength: 500,
    maxRoleLength: 60,
    toastMs: 4000
  });

  const PROMOTIONS = { Base: "Base", Elite1: "Élite 1", Elite2: "Élite 2" };

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
  // 4. Búsqueda y filtros combinados
  // ===================================================================

  /** Texto comparable: minúsculas, sin acentos y con espacios simples. */
  function normalizeSearch(text) {
    return String(text ?? "")
      .normalize("NFKD")
      .replace(/[̀-ͯ]/g, "")
      .toLowerCase()
      .replace(/\s+/g, " ")
      .trim();
  }

  // El texto indexable de cada operador se calcula una sola vez.
  const searchIndex = new WeakMap();

  function haystackOf(op) {
    let text = searchIndex.get(op);
    if (text === undefined) {
      text = normalizeSearch([op.name, op.className, op.archetype, ...(op.affiliations || [op.affiliation]),
        op.description, op.biography].join(" "));
      searchIndex.set(op, text);
    }
    return text;
  }

  /**
   * Aplica búsqueda libre y filtros con lógica AND. Cada palabra de la búsqueda debe aparecer en el nombre,
   * la clase, el arquetipo, las afiliaciones, la descripción o la biografía.
   */
  function filterOperators(operators, { search = "", className = "", rarity = "", affiliation = "" } = {}) {
    const tokens = normalizeSearch(search).split(" ").filter(Boolean);
    const wantedRarity = rarity === "" || rarity === null || rarity === undefined ? null : Number(rarity);
    const wantedAffiliation = normalizeSearch(affiliation);

    return operators.filter(op =>
      (!className || op.className === className)
      && (wantedRarity === null || op.rarity === wantedRarity)
      && (!wantedAffiliation || (op.affiliations || [op.affiliation]).some(a => normalizeSearch(a) === wantedAffiliation))
      && tokens.every(token => haystackOf(op).includes(token)));
  }

  /** Afiliaciones principales únicas con su número de operadores, en orden alfabético. */
  function collectAffiliations(operators) {
    const counts = new Map();
    for (const op of operators) {
      if (op.affiliation) counts.set(op.affiliation, (counts.get(op.affiliation) || 0) + 1);
    }
    return [...counts.entries()]
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => compareNames(a.name, b.name));
  }

  function hasActiveFilters(filters = {}) {
    return Boolean(normalizeSearch(filters.search) || filters.className || filters.rarity || filters.affiliation);
  }

  // ===================================================================
  // 5. Favoritos: CRUD en localStorage y sincronización con el backend
  // ===================================================================

  /** Error de favoritos. code: duplicate | validation. */
  class FavoriteError extends Error {
    constructor(message, code, field = null) {
      super(message);
      this.name = "FavoriteError";
      this.code = code;
      this.field = field;
    }
  }

  function newId() {
    if (globalThis.crypto && typeof globalThis.crypto.randomUUID === "function") return globalThis.crypto.randomUUID();
    return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`;
  }

  function scoreInRange(value, field, label) {
    const number = Number(value);
    if (!Number.isInteger(number) || number < 1 || number > 5) {
      throw new FavoriteError(`${label} debe estar entre 1 y 5.`, "validation", field);
    }
    return number;
  }

  /** Valida un cambio parcial con las mismas reglas que FavoriteSquadMember en C#. */
  function validateFavoritePatch(patch = {}) {
    const clean = {};
    if (patch.squadRole !== undefined) {
      clean.squadRole = cleanText(String(patch.squadRole ?? ""));
      if (clean.squadRole.length > CONFIG.maxRoleLength) {
        throw new FavoriteError(`El rol no puede superar ${CONFIG.maxRoleLength} caracteres.`, "validation", "squadRole");
      }
    }
    if (patch.priority !== undefined) clean.priority = scoreInRange(patch.priority, "priority", "La prioridad");
    if (patch.rating !== undefined) clean.rating = scoreInRange(patch.rating, "rating", "La calificación");
    if (patch.preferredPromotion !== undefined) {
      if (!Object.hasOwn(PROMOTIONS, patch.preferredPromotion)) {
        throw new FavoriteError("La promoción preferida no es válida.", "validation", "preferredPromotion");
      }
      clean.preferredPromotion = patch.preferredPromotion;
    }
    if (patch.tacticalNotes !== undefined) {
      clean.tacticalNotes = cleanText(String(patch.tacticalNotes ?? ""));
      if (clean.tacticalNotes.length > CONFIG.maxNotesLength) {
        throw new FavoriteError(`La nota táctica no puede superar ${CONFIG.maxNotesLength} caracteres.`, "validation", "tacticalNotes");
      }
    }
    return clean;
  }

  function isFavoriteRecord(value) {
    return Boolean(value) && typeof value === "object"
      && typeof value.id === "string" && typeof value.operatorId === "string" && typeof value.operatorName === "string";
  }

  /**
   * CRUD de favoritos con persistencia en localStorage. Es la fuente de verdad en GitHub Pages;
   * en local, cada cambio se replica además en FavoritesController.
   */
  function createFavoritesStore(storage, { now = Date.now, idFactory = newId, key = CONFIG.favoritesKey } = {}) {
    const listeners = new Set();
    let records = load();

    function load() {
      try {
        const parsed = JSON.parse(storage.getItem(key));
        return Array.isArray(parsed) ? parsed.filter(isFavoriteRecord) : [];
      } catch {
        return [];
      }
    }

    function persist() {
      try {
        storage.setItem(key, JSON.stringify(records));
      } catch {
        /* almacenamiento lleno o bloqueado: los cambios viven sólo en esta sesión */
      }
    }

    function emit() {
      const snapshot = list();
      for (const listener of listeners) listener(snapshot);
    }

    const find = id => records.find(record => record.id === id);

    function list() {
      return records.map(record => ({ ...record })).sort((a, b) => a.priority - b.priority || a.createdAt - b.createdAt);
    }

    function get(id) {
      const record = find(id);
      return record ? { ...record } : null;
    }

    function findByOperator(operatorId) {
      const record = records.find(item => item.operatorId === operatorId);
      return record ? { ...record } : null;
    }

    function has(operatorId) {
      return records.some(record => record.operatorId === operatorId);
    }

    // CREATE
    function add(op, initial = {}) {
      if (has(op.id)) throw new FavoriteError(`${op.name} ya está en tu escuadrón.`, "duplicate");
      const timestamp = now();
      const record = {
        id: idFactory(),
        operatorId: op.id,
        operatorName: op.name,
        className: op.className || "",
        rarity: op.rarity || 0,
        art: op.art || CONFIG.placeholderArt,
        squadRole: "",
        priority: 3,
        rating: 3,
        preferredPromotion: "Elite2",
        tacticalNotes: "",
        remoteId: null,
        createdAt: timestamp,
        updatedAt: timestamp,
        ...validateFavoritePatch(initial)
      };
      records.push(record);
      persist();
      emit();
      return { ...record };
    }

    // UPDATE
    function update(id, patch) {
      const record = find(id);
      if (!record) return null;
      Object.assign(record, validateFavoritePatch(patch), { updatedAt: now() });
      persist();
      emit();
      return { ...record };
    }

    // DELETE
    function remove(id) {
      const before = records.length;
      records = records.filter(record => record.id !== id);
      if (records.length === before) return false;
      persist();
      emit();
      return true;
    }

    /** Guarda el id que asignó el backend sin tocar la fecha de edición. */
    function attachRemote(id, remoteId) {
      const record = find(id);
      if (!record) return;
      record.remoteId = remoteId;
      persist();
    }

    function subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    }

    return { list, get, findByOperator, has, add, update, remove, attachRemote, subscribe };
  }

  /**
   * Replica el CRUD en FavoritesController (POST, PUT, DELETE). Nunca lanza: si el backend no está,
   * los favoritos siguen guardados en localStorage.
   */
  function createFavoritesSync({ fetchImpl = globalThis.fetch, baseUrl = CONFIG.favoritesApiUrl, timeoutMs = CONFIG.backendTimeoutMs } = {}) {
    const editable = fav => ({
      squadRole: fav.squadRole,
      priority: fav.priority,
      rating: fav.rating,
      preferredPromotion: fav.preferredPromotion,
      tacticalNotes: fav.tacticalNotes
    });

    async function send(url, method, payload) {
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), timeoutMs);
      try {
        return await fetchImpl(url, {
          method,
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: payload ? JSON.stringify(payload) : undefined,
          signal: controller.signal
        });
      } finally {
        clearTimeout(timer);
      }
    }

    const itemUrl = fav => `${baseUrl}/${encodeURIComponent(fav.remoteId)}`;

    async function create(fav) {
      try {
        const response = await send(baseUrl, "POST", { operatorName: fav.operatorName, ...editable(fav) });
        if (response.status === 409) {
          // Ya existía en el backend (por ejemplo, tras limpiar el navegador): recuperamos su id.
          const all = await (await send(baseUrl, "GET")).json();
          const match = Array.isArray(all)
            ? all.find(item => normalizeSearch(item.operatorName) === normalizeSearch(fav.operatorName))
            : null;
          return match ? match.id : null;
        }
        if (!response.ok) return null;
        const created = await response.json();
        return created && created.id ? created.id : null;
      } catch {
        return null;
      }
    }

    async function update(fav) {
      if (!fav.remoteId) return false;
      try {
        return (await send(itemUrl(fav), "PUT", editable(fav))).ok;
      } catch {
        return false;
      }
    }

    async function remove(fav) {
      if (!fav.remoteId) return false;
      try {
        const response = await send(itemUrl(fav), "DELETE");
        return response.ok || response.status === 404;
      } catch {
        return false;
      }
    }

    return { create, update, remove };
  }

  // ===================================================================
  // 6. Renderizado
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
    paintFavoriteButton($("[data-action='favorite']"), favorites.has(op.id), op.name);
    $("[data-action='compare']").setAttribute("aria-label", `Comparar a ${op.name}`);
    return card;
  }

  /** Estado visual del botón Guardar: estrella rellena y texto "Guardado" cuando ya es favorito. */
  function paintFavoriteButton(button, isFavorite, name) {
    button.setAttribute("aria-pressed", String(isFavorite));
    button.setAttribute("aria-label", `${isFavorite ? "Quitar de" : "Guardar en"} favoritos a ${name}`);
    const icon = button.querySelector("i");
    if (icon) icon.className = isFavorite ? "ph-fill ph-star" : "ph ph-star";
    const label = button.querySelector("span");
    if (label) label.textContent = isFavorite ? "Guardado" : "Guardar";
  }

  /** Actualiza los botones de las tarjetas visibles sin volver a pintar la grilla. */
  function syncFavoriteButtons(doc, favoriteIds, operatorsById) {
    for (const button of doc.querySelectorAll("#operator-grid [data-action='favorite']")) {
      const op = operatorsById.get(button.dataset.operatorId);
      if (op) paintFavoriteButton(button, favoriteIds.has(op.id), op.name);
    }
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

  function setResultsCount(doc, shown, { source, stale, total } = {}) {
    const origin = SOURCE_LABELS[source] ? ` · datos de ${SOURCE_LABELS[source]}` : "";
    const warning = stale ? " (copia guardada, RhodesAPI no respondió)" : "";
    const filtered = typeof total === "number" && total !== shown;
    const noun = (filtered ? total : shown) === 1 ? "operador" : "operadores";
    const amount = filtered ? `${formatNumber(shown)} de ${formatNumber(total)}` : formatNumber(shown);
    doc.getElementById("results-count").textContent = `${amount} ${noun}${origin}${warning}`;
  }

  function favoriteMeta(fav) {
    const parts = [
      fav.squadRole || "Sin rol asignado",
      `Prioridad ${fav.priority}`,
      `Calificación ${fav.rating} de 5`,
      `Promoción ${PROMOTIONS[fav.preferredPromotion] || fav.preferredPromotion}`
    ];
    return parts.join(" · ");
  }

  /** Pinta la sección de favoritos (Read del CRUD). */
  function renderFavorites(doc, favorites) {
    const list = doc.getElementById("favorites-list");
    const empty = doc.getElementById("favorites-empty");
    const count = doc.getElementById("favorites-count");
    if (!list) return;

    list.replaceChildren(...favorites.map(fav => {
      const item = doc.createElement("li");
      item.className = "favorite-item";
      item.dataset.favoriteId = fav.id;

      const img = doc.createElement("img");
      img.setAttribute("alt", "");
      img.setAttribute("width", "64");
      img.setAttribute("height", "64");
      img.setAttribute("loading", "lazy");
      withFallbackImage(img, fav.art);

      const info = doc.createElement("div");
      info.className = "favorite-info";
      const name = doc.createElement("h3");
      name.textContent = fav.operatorName;
      const meta = doc.createElement("p");
      meta.className = "favorite-meta";
      meta.textContent = favoriteMeta(fav);
      const note = doc.createElement("p");
      note.className = fav.tacticalNotes ? "favorite-note" : "favorite-note is-empty";
      note.textContent = fav.tacticalNotes || "Sin nota táctica todavía.";
      info.append(name, meta, note);

      const actions = doc.createElement("div");
      actions.className = "favorite-actions";
      actions.append(
        actionButton(doc, "edit", "btn btn-secondary btn-sm", "ph-pencil-simple", "Editar ficha", `Editar la ficha de ${fav.operatorName}`),
        actionButton(doc, "remove", "btn btn-ghost btn-danger btn-sm", "ph-trash", "Quitar", `Quitar a ${fav.operatorName} del escuadrón`)
      );

      item.append(img, info, actions);
      return item;
    }));

    count.textContent = String(favorites.length);
    empty.hidden = favorites.length > 0;
    list.hidden = favorites.length === 0;
  }

  function actionButton(doc, action, className, icon, text, label) {
    const button = doc.createElement("button");
    button.type = "button";
    button.className = className;
    button.dataset.action = action;
    button.setAttribute("aria-label", label);
    const i = doc.createElement("i");
    i.className = `ph ${icon}`;
    i.setAttribute("aria-hidden", "true");
    const span = doc.createElement("span");
    span.textContent = text;
    button.append(i, span);
    return button;
  }

  /** Aviso breve y accesible para confirmar acciones. */
  function createToast(doc) {
    const element = doc.getElementById("status-toast");
    let timer = null;
    return function show(message) {
      if (!element) return;
      element.textContent = message;
      element.classList.add("is-visible");
      clearTimeout(timer);
      timer = setTimeout(() => element.classList.remove("is-visible"), CONFIG.toastMs);
    };
  }

  function openDialog(dialog) {
    if (typeof dialog.showModal === "function") {
      if (!dialog.open) dialog.showModal();
    } else {
      dialog.setAttribute("open", "");
    }
  }

  function closeDialog(dialog) {
    if (typeof dialog.close === "function" && dialog.open) dialog.close();
    else dialog.removeAttribute("open");
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
  // 7. Interfaz base
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
  // 8. Arranque
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

  function readFilters(doc) {
    return {
      search: doc.getElementById("search-input").value,
      className: doc.getElementById("filter-class").value,
      rarity: doc.getElementById("filter-rarity").value,
      affiliation: doc.getElementById("filter-affiliation").value
    };
  }

  function clearFilters(doc) {
    doc.getElementById("search-input").value = "";
    for (const id of ["filter-class", "filter-rarity", "filter-affiliation"]) doc.getElementById(id).value = "";
  }

  function fillAffiliations(doc, operators) {
    const select = doc.getElementById("filter-affiliation");
    const current = select.value;
    const first = select.querySelector("option[value='']");
    const options = collectAffiliations(operators).map(({ name, count }) => {
      const option = doc.createElement("option");
      option.value = name;
      option.textContent = `${name} (${count})`;
      return option;
    });
    select.replaceChildren(first, ...options);
    select.value = options.some(option => option.value === current) ? current : "";
  }

  /** Buscador en tiempo real y filtros combinados sobre el catálogo cargado. */
  function initCatalog(doc, ctx, { searchDelayMs }) {
    const { state } = ctx;
    let timer = null;

    function apply() {
      state.filters = readFilters(doc);
      state.visible = filterOperators(state.operators, state.filters);
      renderOperators(doc, state.visible, { favorites: ctx.favoriteIds() });
      if (state.visible.length > 0) {
        setResultsCount(doc, state.visible.length, { source: state.source, stale: state.stale, total: state.operators.length });
      }
    }

    function schedule() {
      clearTimeout(timer);
      if (searchDelayMs <= 0) apply();
      else timer = setTimeout(apply, searchDelayMs);
    }

    function reset() {
      clearFilters(doc);
      apply();
      doc.getElementById("search-input").focus();
    }

    doc.getElementById("search-input").addEventListener("input", schedule);
    for (const id of ["filter-class", "filter-rarity", "filter-affiliation"]) {
      doc.getElementById(id).addEventListener("change", apply);
    }
    doc.getElementById("reset-filters").addEventListener("click", reset);
    doc.getElementById("empty-reset").addEventListener("click", reset);

    return {
      apply,
      loaded(operators) {
        state.byId = new Map(operators.map(op => [op.id, op]));
        fillAffiliations(doc, operators);
        apply();
      }
    };
  }

  /** CRUD de favoritos en la interfaz: guardar desde la tarjeta, editar ficha, quitar. */
  function initFavorites(doc, ctx, { store, sync, toast }) {
    const modal = doc.getElementById("note-modal");
    const form = doc.getElementById("note-form");
    const fields = {
      squadRole: doc.getElementById("note-role"),
      priority: doc.getElementById("note-priority"),
      rating: doc.getElementById("note-rating"),
      preferredPromotion: doc.getElementById("note-promotion"),
      tacticalNotes: doc.getElementById("note-text")
    };
    const counter = doc.getElementById("note-counter");
    const errorBox = doc.getElementById("note-error");
    let editingId = null;

    const refresh = list => {
      renderFavorites(doc, list);
      syncFavoriteButtons(doc, ctx.favoriteIds(), ctx.state.byId || new Map());
    };
    store.subscribe(refresh);
    refresh(store.list());

    function toggle(operatorId) {
      const op = ctx.state.byId && ctx.state.byId.get(operatorId);
      if (!op) return;
      const existing = store.findByOperator(op.id);
      if (existing) {
        store.remove(existing.id);
        toast(`${op.name} salió de tu escuadrón.`);
        if (sync) sync.remove(existing);
        return;
      }
      const created = store.add(op);
      toast(`${op.name} se agregó a tu escuadrón.`);
      if (sync) sync.create(created).then(remoteId => remoteId && store.attachRemote(created.id, remoteId));
    }

    function remove(favoriteId) {
      const fav = store.get(favoriteId);
      if (!fav || !store.remove(favoriteId)) return;
      toast(`${fav.operatorName} salió de tu escuadrón.`);
      if (sync) sync.remove(fav);
    }

    function showError(message, field) {
      errorBox.textContent = message;
      errorBox.hidden = !message;
      for (const [name, input] of Object.entries(fields)) {
        if (name === field) input.setAttribute("aria-invalid", "true");
        else input.removeAttribute("aria-invalid");
      }
    }

    function openEditor(favoriteId) {
      const fav = store.get(favoriteId);
      if (!fav) return;
      editingId = fav.id;
      fields.squadRole.value = fav.squadRole;
      fields.priority.value = String(fav.priority);
      fields.rating.value = String(fav.rating);
      fields.preferredPromotion.value = fav.preferredPromotion;
      fields.tacticalNotes.value = fav.tacticalNotes;
      counter.textContent = String(fav.tacticalNotes.length);
      doc.getElementById("note-operator").textContent =
        `Ficha de ${fav.operatorName} · ${fav.className} · ${fav.rarity}★`;
      showError("", null);
      openDialog(modal);
      fields.tacticalNotes.focus();
    }

    // UPDATE desde el modal, con validación en línea antes de guardar.
    form.addEventListener("submit", event => {
      event.preventDefault();
      if (!editingId) return;
      try {
        const updated = store.update(editingId, {
          squadRole: fields.squadRole.value,
          priority: fields.priority.value,
          rating: fields.rating.value,
          preferredPromotion: fields.preferredPromotion.value,
          tacticalNotes: fields.tacticalNotes.value
        });
        closeDialog(modal);
        if (updated) {
          toast(`Ficha de ${updated.operatorName} actualizada.`);
          if (sync) sync.update(updated);
        }
        editingId = null;
      } catch (error) {
        if (!(error instanceof FavoriteError)) throw error;
        showError(error.message, error.field);
      }
    });

    fields.tacticalNotes.addEventListener("input", () => {
      counter.textContent = String(fields.tacticalNotes.value.length);
    });

    for (const button of modal.querySelectorAll("[data-close-modal]")) {
      button.addEventListener("click", () => { closeDialog(modal); editingId = null; });
    }

    doc.getElementById("operator-grid").addEventListener("click", event => {
      const button = event.target.closest("[data-action='favorite']");
      if (button) toggle(button.dataset.operatorId);
    });

    doc.getElementById("favorites-list").addEventListener("click", event => {
      const button = event.target.closest("[data-action]");
      const item = button && button.closest(".favorite-item");
      if (!item) return;
      if (button.dataset.action === "edit") openEditor(item.dataset.favoriteId);
      if (button.dataset.action === "remove") remove(item.dataset.favoriteId);
    });

    /** En local, sube al backend los favoritos que todavía no tienen id remoto. */
    async function reconcile() {
      if (!sync) return;
      for (const fav of store.list().filter(item => !item.remoteId)) {
        const remoteId = await sync.create(fav);
        if (remoteId) store.attachRemote(fav.id, remoteId);
      }
    }

    return { reconcile };
  }

  function boot(doc, {
    fetchImpl = globalThis.fetch && globalThis.fetch.bind(globalThis),
    storage = safeStorage(),
    location = globalThis.location,
    searchDelayMs = CONFIG.searchDelayMs
  } = {}) {
    doc.documentElement.classList.add("js");
    initNavigation(doc);
    initReveal(doc);
    initTaglineReveal(doc, splitTagline(doc));

    const store = createFavoritesStore(storage);
    const sync = canUseBackend(location) ? createFavoritesSync({ fetchImpl }) : null;
    const state = { operators: [], visible: [], byId: new Map(), filters: {}, source: null, stale: false };
    const ctx = {
      state,
      favoriteIds: () => new Set(store.list().map(fav => fav.operatorId))
    };
    const catalog = initCatalog(doc, ctx, { searchDelayMs });
    const favorites = initFavorites(doc, ctx, { store, sync, toast: createToast(doc) });
    const controller = { state, store, ready: Promise.resolve(), reload };

    async function reload({ forceRefresh = false } = {}) {
      showState(doc, "loading");
      try {
        const result = await loadOperators({ fetchImpl, storage, location, forceRefresh });
        Object.assign(state, { operators: result.operators, source: result.source, stale: result.stale });
        catalog.loaded(result.operators);
        updateHero(doc, result.operators);
      } catch (error) {
        showState(doc, "error", describeError(error));
      }
    }

    const retry = doc.getElementById("retry-button");
    if (retry) retry.addEventListener("click", () => { controller.ready = reload({ forceRefresh: true }); });

    controller.ready = reload();
    favorites.reconcile();
    return controller;
  }

  return {
    CONFIG,
    CLASSES,
    ApiError,
    FavoriteError,
    parseNumber,
    normalizeStats,
    normalizeOperator,
    normalizeBackendOperator,
    normalizeList,
    normalizeSearch,
    filterOperators,
    collectAffiliations,
    hasActiveFilters,
    createFavoritesStore,
    createFavoritesSync,
    fetchJson,
    loadOperators,
    describeError,
    formatNumber,
    renderOperators,
    renderFavorites,
    showState,
    setResultsCount,
    updateHero,
    initNavigation,
    splitTagline,
    boot
  };
});
