// Pruebas del CRUD de favoritos persistido en localStorage y de su sincronización con el backend C#.
"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const app = require("../../script.js");

function memoryStorage(initial = {}) {
  const data = new Map(Object.entries(initial));
  return { getItem: k => (data.has(k) ? data.get(k) : null), setItem: (k, v) => data.set(k, String(v)), removeItem: k => data.delete(k) };
}

function clock(start = 1_700_000_000_000) {
  let t = start;
  const now = () => t;
  now.advance = ms => { t += ms; };
  return now;
}

const exusiai = { id: "exusiai", name: "Exusiai", className: "Sniper", rarity: 6, art: "exusiai.png" };
const saria = { id: "saria", name: "Saria", className: "Defender", rarity: 6, art: "saria.png" };
const myrtle = { id: "myrtle", name: "Myrtle", className: "Vanguard", rarity: 4, art: "myrtle.png" };

function newStore(storage = memoryStorage(), now = clock()) {
  let n = 0;
  return app.createFavoritesStore(storage, { now, idFactory: () => `fav-${++n}` });
}

// ---------- CREATE ----------

test("add guarda el operador con valores por defecto y lo persiste en localStorage", () => {
  const storage = memoryStorage();
  const store = newStore(storage);

  const fav = store.add(exusiai);

  assert.equal(fav.id, "fav-1");
  assert.equal(fav.operatorId, "exusiai");
  assert.equal(fav.operatorName, "Exusiai");
  assert.equal(fav.squadRole, "");
  assert.equal(fav.priority, 3);
  assert.equal(fav.rating, 3);
  assert.equal(fav.preferredPromotion, "Elite2");
  assert.equal(fav.tacticalNotes, "");
  assert.equal(fav.createdAt, fav.updatedAt);

  const reopened = newStore(storage);
  assert.equal(reopened.list().length, 1);
  assert.equal(reopened.findByOperator("exusiai").operatorName, "Exusiai");
});

test("add rechaza un operador repetido", () => {
  const store = newStore();
  store.add(exusiai);

  assert.throws(() => store.add({ ...exusiai }), err => err instanceof app.FavoriteError && err.code === "duplicate");
  assert.equal(store.list().length, 1);
});

test("add acepta valores iniciales validados", () => {
  const store = newStore();
  const fav = store.add(saria, { squadRole: "Tanque sanador", priority: 1, tacticalNotes: "  Junto a los Guards  " });

  assert.equal(fav.squadRole, "Tanque sanador");
  assert.equal(fav.priority, 1);
  assert.equal(fav.tacticalNotes, "Junto a los Guards");
});

// ---------- READ ----------

test("list ordena por prioridad y luego por fecha de alta", () => {
  const now = clock();
  const store = newStore(memoryStorage(), now);
  store.add(exusiai, { priority: 3 });
  now.advance(10);
  store.add(saria, { priority: 1 });
  now.advance(10);
  store.add(myrtle, { priority: 3 });

  assert.deepEqual(store.list().map(f => f.operatorName), ["Saria", "Exusiai", "Myrtle"]);
});

test("get, has y findByOperator localizan registros", () => {
  const store = newStore();
  const fav = store.add(exusiai);

  assert.equal(store.get(fav.id).operatorName, "Exusiai");
  assert.equal(store.get("nada"), null);
  assert.equal(store.has("exusiai"), true);
  assert.equal(store.has("saria"), false);
});

test("una entrada corrupta en localStorage deja la lista vacía sin romper la app", () => {
  const store = newStore(memoryStorage({ [app.CONFIG.favoritesKey]: "{no es json" }));
  assert.deepEqual(store.list(), []);
  const filtered = newStore(memoryStorage({ [app.CONFIG.favoritesKey]: JSON.stringify([{ id: "x" }, null, 5]) }));
  assert.deepEqual(filtered.list(), []);
});

// ---------- UPDATE ----------

test("update edita la nota táctica, recorta espacios y actualiza updatedAt", () => {
  const now = clock();
  const storage = memoryStorage();
  const store = newStore(storage, now);
  const fav = store.add(exusiai);
  now.advance(5000);

  const updated = store.update(fav.id, { tacticalNotes: "  S3 contra drones  ", rating: 5 });

  assert.equal(updated.tacticalNotes, "S3 contra drones");
  assert.equal(updated.rating, 5);
  assert.equal(updated.priority, 3);
  assert.ok(updated.updatedAt > updated.createdAt);
  assert.equal(newStore(storage).get(fav.id).tacticalNotes, "S3 contra drones");
});

test("update cambia rol, prioridad y promoción preferida", () => {
  const store = newStore();
  const fav = store.add(myrtle);

  const updated = store.update(fav.id, { squadRole: "Generador de DP", priority: "2", preferredPromotion: "Elite1" });

  assert.equal(updated.squadRole, "Generador de DP");
  assert.equal(updated.priority, 2);
  assert.equal(updated.preferredPromotion, "Elite1");
});

test("update valida los mismos límites que el dominio C#", () => {
  const store = newStore();
  const fav = store.add(exusiai);
  const isValidation = err => err instanceof app.FavoriteError && err.code === "validation";

  assert.throws(() => store.update(fav.id, { tacticalNotes: "x".repeat(501) }), isValidation);
  assert.throws(() => store.update(fav.id, { priority: 0 }), isValidation);
  assert.throws(() => store.update(fav.id, { rating: 6 }), isValidation);
  assert.throws(() => store.update(fav.id, { preferredPromotion: "Elite3" }), isValidation);
  assert.throws(() => store.update(fav.id, { squadRole: "r".repeat(61) }), isValidation);
  assert.equal(store.get(fav.id).tacticalNotes, "");
});

test("update devuelve null si el favorito no existe", () => {
  assert.equal(newStore().update("nada", { rating: 2 }), null);
});

// ---------- DELETE ----------

test("remove elimina el favorito y permite volver a agregarlo", () => {
  const storage = memoryStorage();
  const store = newStore(storage);
  const fav = store.add(exusiai);

  assert.equal(store.remove(fav.id), true);
  assert.equal(store.remove(fav.id), false);
  assert.equal(store.has("exusiai"), false);
  assert.equal(newStore(storage).list().length, 0);
  assert.ok(store.add(exusiai));
});

test("subscribe avisa de cada cambio", () => {
  const store = newStore();
  const events = [];
  store.subscribe(list => events.push(list.length));

  const fav = store.add(exusiai);
  store.update(fav.id, { rating: 4 });
  store.remove(fav.id);

  assert.deepEqual(events, [1, 1, 0]);
});

// ---------- Sincronización con FavoritesController ----------

function recordingFetch(responder) {
  const calls = [];
  const impl = async (url, options = {}) => {
    calls.push({ url, method: options.method || "GET", body: options.body ? JSON.parse(options.body) : null });
    return responder(url, options);
  };
  impl.calls = calls;
  return impl;
}

const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

test("sync.create hace POST con los campos del DTO y devuelve el id remoto", async () => {
  const fetchImpl = recordingFetch(() => json({ id: "remote-1" }, 201));
  const sync = app.createFavoritesSync({ fetchImpl, baseUrl: "http://localhost:5000/api/favorites" });
  const fav = newStore().add(exusiai, { tacticalNotes: "Nota" });

  const remoteId = await sync.create(fav);

  assert.equal(remoteId, "remote-1");
  assert.deepEqual(fetchImpl.calls[0], {
    url: "http://localhost:5000/api/favorites",
    method: "POST",
    body: { operatorName: "Exusiai", squadRole: "", priority: 3, rating: 3, preferredPromotion: "Elite2", tacticalNotes: "Nota" }
  });
});

test("sync.create recupera el id remoto cuando el backend responde 409", async () => {
  const fetchImpl = recordingFetch((url, options) =>
    options.method === "POST" ? json({ title: "duplicado" }, 409) : json([{ id: "remote-9", operatorName: "Exusiai" }]));
  const sync = app.createFavoritesSync({ fetchImpl, baseUrl: "http://localhost:5000/api/favorites" });

  assert.equal(await sync.create(newStore().add(exusiai)), "remote-9");
});

test("sync.update hace PUT y sync.remove hace DELETE sobre el id remoto", async () => {
  const fetchImpl = recordingFetch((url, options) => (options.method === "DELETE" ? new Response(null, { status: 204 }) : json({})));
  const sync = app.createFavoritesSync({ fetchImpl, baseUrl: "http://localhost:5000/api/favorites" });
  const fav = { ...newStore().add(saria), remoteId: "remote-2", tacticalNotes: "Cubrir el flanco" };

  assert.equal(await sync.update(fav), true);
  assert.equal(await sync.remove(fav), true);

  assert.equal(fetchImpl.calls[0].method, "PUT");
  assert.equal(fetchImpl.calls[0].url, "http://localhost:5000/api/favorites/remote-2");
  assert.equal(fetchImpl.calls[0].body.tacticalNotes, "Cubrir el flanco");
  assert.equal(fetchImpl.calls[1].method, "DELETE");
});

test("sin id remoto no se llama al backend y un fallo de red no rompe nada", async () => {
  const fetchImpl = recordingFetch(() => { throw new TypeError("Failed to fetch"); });
  const sync = app.createFavoritesSync({ fetchImpl, baseUrl: "http://localhost:5000/api/favorites" });
  const fav = newStore().add(exusiai);

  assert.equal(await sync.update(fav), false);
  assert.equal(await sync.remove(fav), false);
  assert.equal(fetchImpl.calls.length, 0);
  assert.equal(await sync.create(fav), null);
});

test("attachRemote guarda el id remoto sin alterar updatedAt", () => {
  const now = clock();
  const storage = memoryStorage();
  const store = newStore(storage, now);
  const fav = store.add(exusiai);
  now.advance(1000);

  store.attachRemote(fav.id, "remote-3");

  assert.equal(newStore(storage).get(fav.id).remoteId, "remote-3");
  assert.equal(store.get(fav.id).updatedAt, fav.updatedAt);
});
