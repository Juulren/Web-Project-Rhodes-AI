// Pruebas de integración en jsdom: buscador en tiempo real, filtros y sección de favoritos sobre el index.html real.
"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { JSDOM } = require("jsdom");
const app = require("../../script.js");
const f = require("./fixtures.js");

const html = fs.readFileSync(path.join(__dirname, "..", "..", "index.html"), "utf8");
const PAGES = { hostname: "juulren.github.io", protocol: "https:" };

function memoryStorage() {
  const data = new Map();
  return { getItem: k => (data.has(k) ? data.get(k) : null), setItem: (k, v) => data.set(k, String(v)), removeItem: k => data.delete(k), data };
}

const rawCatalog = [f.saria, f.exusiai, f.ines, f.myrtle, ...f.manyOperators(10)];

async function bootPage(storage = memoryStorage()) {
  const dom = new JSDOM(html, { url: "https://juulren.github.io/Web-Project-Rhodes-AI/" });
  const doc = dom.window.document;
  const fetchImpl = async url => new Response(JSON.stringify(url.includes("portraits") ? f.portraitMap : rawCatalog));
  const controller = app.boot(doc, { fetchImpl, storage, location: PAGES, searchDelayMs: 0 });
  await controller.ready;
  return { doc, window: dom.window, controller, storage };
}

const visibleNames = doc => [...doc.querySelectorAll("#operator-grid .card-name")].map(n => n.textContent);

function type(doc, window, value) {
  const input = doc.getElementById("search-input");
  input.value = value;
  input.dispatchEvent(new window.Event("input", { bubbles: true }));
}

function choose(doc, window, id, value) {
  const select = doc.getElementById(id);
  select.value = value;
  select.dispatchEvent(new window.Event("change", { bubbles: true }));
}

// ---------- Buscador y filtros ----------

test("el selector de afiliación se llena con las facciones del catálogo", async () => {
  const { doc } = await bootPage();
  const options = [...doc.querySelectorAll("#filter-affiliation option")].map(o => o.value);

  assert.equal(options[0], "");
  assert.ok(options.includes("Columbia"));
  assert.ok(options.includes("Penguin Logistics"));
  assert.equal(new Set(options).size, options.length);
});

test("escribir en el buscador filtra la grilla en tiempo real", async () => {
  const { doc, window } = await bootPage();

  type(doc, window, "penguin");

  assert.deepEqual(visibleNames(doc), ["Exusiai"]);
  assert.match(doc.getElementById("results-count").textContent, /^1 de 14 operadores/);
});

test("los filtros de clase y rareza se combinan con la búsqueda", async () => {
  const { doc, window } = await bootPage();

  choose(doc, window, "filter-class", "Vanguard");
  assert.deepEqual(visibleNames(doc), ["Ines", "Myrtle", "Operador A", "Operador I"]);

  choose(doc, window, "filter-rarity", "6");
  assert.deepEqual(visibleNames(doc), ["Ines"]);

  choose(doc, window, "filter-rarity", "");
  type(doc, window, "liberi");
  assert.deepEqual(visibleNames(doc), ["Myrtle"]);
});

test("el filtro de afiliación limita los resultados", async () => {
  const { doc, window } = await bootPage();
  choose(doc, window, "filter-affiliation", "Columbia");
  assert.deepEqual(visibleNames(doc), ["Saria"]);
});

test("sin coincidencias aparece el estado vacío y Quitar filtros restablece todo", async () => {
  const { doc, window } = await bootPage();

  type(doc, window, "nadie se llama así");
  assert.equal(doc.getElementById("empty-state").hidden, false);
  assert.equal(doc.getElementById("operator-grid").hidden, true);

  doc.getElementById("empty-reset").click();
  assert.equal(doc.getElementById("search-input").value, "");
  assert.equal(doc.getElementById("empty-state").hidden, true);
  assert.equal(visibleNames(doc).length, 14);
});

test("el botón Quitar filtros de la barra limpia búsqueda y selectores", async () => {
  const { doc, window } = await bootPage();
  type(doc, window, "saria");
  choose(doc, window, "filter-class", "Defender");

  doc.getElementById("reset-filters").click();

  assert.equal(doc.getElementById("filter-class").value, "");
  assert.equal(visibleNames(doc).length, 14);
  assert.match(doc.getElementById("results-count").textContent, /^14 operadores/);
});

// ---------- Favoritos ----------

const cardButton = (doc, id, action) => doc.querySelector(`.operator-card[data-operator-id='${id}'] [data-action='${action}']`);

test("Guardar agrega el operador a favoritos, lo persiste y actualiza la tarjeta", async () => {
  const { doc, storage } = await bootPage();

  cardButton(doc, "exusiai", "favorite").click();

  const items = doc.querySelectorAll("#favorites-list .favorite-item");
  assert.equal(items.length, 1);
  assert.match(items[0].textContent, /Exusiai/);
  assert.equal(doc.getElementById("favorites-count").textContent, "1");
  assert.equal(doc.getElementById("favorites-empty").hidden, true);
  assert.equal(cardButton(doc, "exusiai", "favorite").getAttribute("aria-pressed"), "true");
  assert.equal(JSON.parse(storage.getItem(app.CONFIG.favoritesKey)).length, 1);
  assert.match(doc.getElementById("status-toast").textContent, /Exusiai se agregó a tu escuadrón/);
});

test("pulsar de nuevo Guardar quita el favorito", async () => {
  const { doc } = await bootPage();

  cardButton(doc, "exusiai", "favorite").click();
  cardButton(doc, "exusiai", "favorite").click();

  assert.equal(doc.querySelectorAll("#favorites-list .favorite-item").length, 0);
  assert.equal(doc.getElementById("favorites-empty").hidden, false);
  assert.equal(cardButton(doc, "exusiai", "favorite").getAttribute("aria-pressed"), "false");
});

test("el estado de favorito se conserva al filtrar y volver a pintar la grilla", async () => {
  const { doc, window } = await bootPage();
  cardButton(doc, "saria", "favorite").click();

  type(doc, window, "sar");

  assert.equal(cardButton(doc, "saria", "favorite").getAttribute("aria-pressed"), "true");
});

test("Editar ficha abre el modal con los datos y guardar actualiza la nota táctica", async () => {
  const { doc, window, storage } = await bootPage();
  cardButton(doc, "saria", "favorite").click();

  doc.querySelector("#favorites-list [data-action='edit']").click();
  const modal = doc.getElementById("note-modal");
  assert.equal(modal.hasAttribute("open"), true);
  assert.match(doc.getElementById("note-operator").textContent, /Saria/);

  doc.getElementById("note-text").value = "Mantener la S2 para curar a los Guards";
  doc.getElementById("note-role").value = "Tanque sanador";
  doc.getElementById("note-priority").value = "1";
  doc.getElementById("note-form").dispatchEvent(new window.Event("submit", { bubbles: true, cancelable: true }));

  assert.equal(modal.hasAttribute("open"), false);
  const item = doc.querySelector("#favorites-list .favorite-item");
  assert.match(item.textContent, /Mantener la S2 para curar a los Guards/);
  assert.match(item.textContent, /Tanque sanador/);
  const saved = JSON.parse(storage.getItem(app.CONFIG.favoritesKey))[0];
  assert.equal(saved.priority, 1);
});

test("el modal muestra un error en línea si la nota supera 500 caracteres", async () => {
  const { doc, window } = await bootPage();
  cardButton(doc, "saria", "favorite").click();
  doc.querySelector("#favorites-list [data-action='edit']").click();

  doc.getElementById("note-text").value = "x".repeat(501);
  doc.getElementById("note-form").dispatchEvent(new window.Event("submit", { bubbles: true, cancelable: true }));

  const error = doc.getElementById("note-error");
  assert.equal(error.hidden, false);
  assert.match(error.textContent, /500/);
  assert.equal(doc.getElementById("note-modal").hasAttribute("open"), true);
  assert.equal(doc.getElementById("note-text").getAttribute("aria-invalid"), "true");
});

test("el contador de caracteres sigue lo que se escribe", async () => {
  const { doc, window } = await bootPage();
  cardButton(doc, "saria", "favorite").click();
  doc.querySelector("#favorites-list [data-action='edit']").click();

  const textarea = doc.getElementById("note-text");
  textarea.value = "Hola";
  textarea.dispatchEvent(new window.Event("input", { bubbles: true }));

  assert.equal(doc.getElementById("note-counter").textContent, "4");
});

test("Quitar en la lista elimina el favorito", async () => {
  const { doc } = await bootPage();
  cardButton(doc, "exusiai", "favorite").click();
  cardButton(doc, "saria", "favorite").click();

  doc.querySelector("#favorites-list .favorite-item [data-action='remove']").click();

  assert.equal(doc.querySelectorAll("#favorites-list .favorite-item").length, 1);
  assert.equal(doc.getElementById("favorites-count").textContent, "1");
});

test("los favoritos siguen ahí al recargar la página", async () => {
  const storage = memoryStorage();
  const first = await bootPage(storage);
  cardButton(first.doc, "myrtle", "favorite").click();

  const second = await bootPage(storage);

  assert.match(second.doc.querySelector("#favorites-list .favorite-item").textContent, /Myrtle/);
  assert.equal(cardButton(second.doc, "myrtle", "favorite").getAttribute("aria-pressed"), "true");
});
