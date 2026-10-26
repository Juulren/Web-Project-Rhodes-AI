// Pruebas de la capa de datos: fetch con tiempo límite, errores HTTP y fallback híbrido.
"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const app = require("../../script.js");
const f = require("./fixtures.js");

const LOCAL = { hostname: "localhost", protocol: "http:" };
const PAGES = { hostname: "juulren.github.io", protocol: "https:" };

function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function memoryStorage(initial = {}) {
  const data = new Map(Object.entries(initial));
  return {
    getItem: key => (data.has(key) ? data.get(key) : null),
    setItem: (key, value) => data.set(key, String(value)),
    removeItem: key => data.delete(key),
    dump: () => Object.fromEntries(data)
  };
}

/** fetch simulado que responde según la URL y registra las llamadas. */
function fakeFetch(routes) {
  const calls = [];
  const impl = async (url, options = {}) => {
    calls.push(url);
    for (const [fragment, handler] of Object.entries(routes)) {
      if (url.includes(fragment)) return handler(options);
    }
    throw new TypeError("Failed to fetch");
  };
  impl.calls = calls;
  return impl;
}

const portraitRoute = { "operator-portraits.json": () => jsonResponse(f.portraitMap) };

test("fetchJson convierte un 500 en ApiError de tipo http con su código", async () => {
  const fetchImpl = fakeFetch({ api: () => jsonResponse({ error: "boom" }, 500) });
  await assert.rejects(app.fetchJson("https://x/api", { fetchImpl }), err => {
    assert.ok(err instanceof app.ApiError);
    assert.equal(err.kind, "http");
    assert.equal(err.status, 500);
    return true;
  });
});

test("fetchJson convierte un 404 en ApiError con status 404", async () => {
  const fetchImpl = fakeFetch({ api: () => new Response("Not found", { status: 404 }) });
  await assert.rejects(app.fetchJson("https://x/api", { fetchImpl }), { name: "ApiError", status: 404 });
});

test("fetchJson aborta cuando se supera el tiempo límite", async () => {
  const fetchImpl = (url, { signal }) => new Promise((_, reject) => {
    signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError")));
  });
  await assert.rejects(app.fetchJson("https://x/api", { fetchImpl, timeoutMs: 20 }), { name: "ApiError", kind: "timeout" });
});

test("fetchJson distingue fallos de red y JSON inválido", async () => {
  await assert.rejects(app.fetchJson("https://x/api", { fetchImpl: fakeFetch({}) }), { kind: "network" });
  const broken = fakeFetch({ api: () => new Response("<html>", { status: 200 }) });
  await assert.rejects(app.fetchJson("https://x/api", { fetchImpl: broken }), { kind: "parse" });
});

test("en localhost se consulta primero el backend C#", async () => {
  const fetchImpl = fakeFetch({
    ...portraitRoute,
    "localhost:5000/api/operators": () => jsonResponse([
      { id: "saria", name: "Saria", rarity: 6, class: "Defender", archetype: "", affiliation: "Columbia", description: "d",
        biography: "b", artUrl: "", promotion: "Elite2", stats: { hp: 1, atk: 1, def: 1, res: 0, cost: 9, block: 3, attackInterval: 1.2, redeployTime: 70 } }
    ])
  });

  const result = await app.loadOperators({ fetchImpl, storage: memoryStorage(), location: LOCAL });

  assert.equal(result.source, "backend");
  assert.equal(result.operators[0].name, "Saria");
  assert.ok(!fetchImpl.calls.some(u => u.includes("railway")));
});

test("si el backend no responde se usa RhodesAPI y se guarda la caché", async () => {
  const storage = memoryStorage();
  const fetchImpl = fakeFetch({ ...portraitRoute, "rhodesapi.up.railway.app": () => jsonResponse([f.saria, f.exusiai]) });

  const result = await app.loadOperators({ fetchImpl, storage, location: LOCAL });

  assert.equal(result.source, "rhodesapi");
  assert.equal(result.operators.length, 2);
  assert.ok(fetchImpl.calls[0].includes("localhost:5000") || fetchImpl.calls.some(u => u.includes("localhost:5000")));
  const cached = JSON.parse(storage.getItem(app.CONFIG.cacheKey));
  assert.equal(cached.operators.length, 2);
});

test("en GitHub Pages nunca se intenta el backend local", async () => {
  const fetchImpl = fakeFetch({ ...portraitRoute, "rhodesapi.up.railway.app": () => jsonResponse([f.saria]) });

  await app.loadOperators({ fetchImpl, storage: memoryStorage(), location: PAGES });

  assert.ok(!fetchImpl.calls.some(u => u.includes("localhost")));
});

test("una caché vigente evita descargar de nuevo los 5 MB del catálogo", async () => {
  const operators = app.normalizeList([f.saria], "rhodesapi", f.portraitMap);
  const storage = memoryStorage({ [app.CONFIG.cacheKey]: JSON.stringify({ savedAt: Date.now(), operators }) });
  const fetchImpl = fakeFetch({});

  const result = await app.loadOperators({ fetchImpl, storage, location: PAGES });

  assert.equal(result.source, "cache");
  assert.equal(fetchImpl.calls.length, 0);
});

test("forceRefresh ignora la caché vigente", async () => {
  const operators = app.normalizeList([f.saria], "rhodesapi", f.portraitMap);
  const storage = memoryStorage({ [app.CONFIG.cacheKey]: JSON.stringify({ savedAt: Date.now(), operators }) });
  const fetchImpl = fakeFetch({ ...portraitRoute, "rhodesapi.up.railway.app": () => jsonResponse([f.saria, f.exusiai]) });

  const result = await app.loadOperators({ fetchImpl, storage, location: PAGES, forceRefresh: true });

  assert.equal(result.source, "rhodesapi");
  assert.equal(result.operators.length, 2);
});

test("si todas las fuentes fallan se usa la caché vencida y se marca como desactualizada", async () => {
  const operators = app.normalizeList([f.saria], "rhodesapi", f.portraitMap);
  const old = Date.now() - app.CONFIG.cacheTtlMs - 1000;
  const storage = memoryStorage({ [app.CONFIG.cacheKey]: JSON.stringify({ savedAt: old, operators }) });
  const fetchImpl = fakeFetch({ "rhodesapi.up.railway.app": () => jsonResponse({}, 503) });

  const result = await app.loadOperators({ fetchImpl, storage, location: PAGES });

  assert.equal(result.source, "cache");
  assert.equal(result.stale, true);
  assert.equal(result.operators[0].name, "Saria");
});

test("sin fuentes ni caché se propaga el último ApiError", async () => {
  const fetchImpl = fakeFetch({ "rhodesapi.up.railway.app": () => jsonResponse({}, 500) });
  await assert.rejects(app.loadOperators({ fetchImpl, storage: memoryStorage(), location: PAGES }), { name: "ApiError", status: 500 });
});

test("si el mapa de retratos falla el catálogo carga igual", async () => {
  const fetchImpl = fakeFetch({ "rhodesapi.up.railway.app": () => jsonResponse([f.saria]) });
  const result = await app.loadOperators({ fetchImpl, storage: memoryStorage(), location: PAGES });
  assert.equal(result.operators.length, 1);
});

test("describeError explica cada fallo con un mensaje específico", () => {
  assert.match(app.describeError(new app.ApiError("x", { kind: "http", status: 500 })), /500/);
  assert.match(app.describeError(new app.ApiError("x", { kind: "http", status: 404 })), /404/);
  assert.match(app.describeError(new app.ApiError("x", { kind: "timeout" })), /tardó/);
  assert.match(app.describeError(new app.ApiError("x", { kind: "network" })), /conexión/);
  assert.match(app.describeError(new Error("otro")), /RhodesAPI/);
});
