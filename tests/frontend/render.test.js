// Pruebas de renderizado sobre el index.html real cargado en jsdom.
"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { JSDOM } = require("jsdom");
const app = require("../../script.js");
const f = require("./fixtures.js");

const html = fs.readFileSync(path.join(__dirname, "..", "..", "index.html"), "utf8");

function loadPage() {
  const dom = new JSDOM(html, { url: "https://juulren.github.io/Web-Project-Rhodes-AI/" });
  return dom.window.document;
}

function memoryStorage() {
  const data = new Map();
  return { getItem: k => (data.has(k) ? data.get(k) : null), setItem: (k, v) => data.set(k, String(v)), removeItem: k => data.delete(k) };
}

const sample = () => app.normalizeList(f.manyOperators(12), "rhodesapi", f.portraitMap);

test("renderOperators pinta al menos 10 tarjetas con imagen, nombre, rareza, clase y descripción", () => {
  const doc = loadPage();
  const count = app.renderOperators(doc, sample());

  const cards = doc.querySelectorAll("#operator-grid .operator-card");
  assert.equal(count, 12);
  assert.ok(cards.length >= 10);
  const first = cards[0];
  assert.match(first.querySelector(".card-art").getAttribute("alt"), /Retrato de Operador A/);
  assert.ok(first.querySelector(".card-art").getAttribute("src").endsWith(".png"));
  assert.equal(first.querySelector(".card-name").textContent, "Operador A");
  assert.match(first.querySelector(".card-rarity").getAttribute("aria-label"), /1 estrella/);
  assert.equal(first.querySelector(".card-class span").textContent, "Vanguard");
  assert.equal(first.querySelector(".card-description").textContent, "Descripción del operador 1.");
  assert.equal(first.querySelector("[data-field='hp']").textContent, "1.673");
  assert.equal(first.dataset.operatorId, "operador-a");
});

test("renderOperators reemplaza los skeletons y marca la grilla como lista", () => {
  const doc = loadPage();
  app.renderOperators(doc, sample());

  const grid = doc.getElementById("operator-grid");
  assert.equal(grid.querySelectorAll(".card-skeleton").length, 0);
  assert.equal(grid.getAttribute("aria-busy"), "false");
});

test("una imagen rota se sustituye por el placeholder local", () => {
  const doc = loadPage();
  app.renderOperators(doc, sample());

  const img = doc.querySelector(".card-art");
  img.dispatchEvent(new doc.defaultView.Event("error"));
  assert.equal(img.getAttribute("src"), app.CONFIG.placeholderArt);
});

test("showState('error') muestra el aviso con el mensaje y oculta la grilla", () => {
  const doc = loadPage();
  app.showState(doc, "error", "RhodesAPI respondió con un error interno (500).");

  assert.equal(doc.getElementById("error-state").hidden, false);
  assert.equal(doc.getElementById("operator-grid").hidden, true);
  assert.equal(doc.getElementById("empty-state").hidden, true);
  assert.match(doc.getElementById("error-message").textContent, /500/);
  assert.match(doc.getElementById("results-count").textContent, /No se pudo/);
});

test("showState('empty') muestra la vista vacía y showState('ready') la oculta", () => {
  const doc = loadPage();

  app.showState(doc, "empty");
  assert.equal(doc.getElementById("empty-state").hidden, false);
  assert.equal(doc.getElementById("operator-grid").hidden, true);

  app.showState(doc, "ready");
  assert.equal(doc.getElementById("empty-state").hidden, true);
  assert.equal(doc.getElementById("operator-grid").hidden, false);
});

test("renderOperators con lista vacía activa el estado vacío", () => {
  const doc = loadPage();
  assert.equal(app.renderOperators(doc, []), 0);
  assert.equal(doc.getElementById("empty-state").hidden, false);
});

test("boot muestra el error con reintento y luego carga al reintentar", async () => {
  const doc = loadPage();
  let healthy = false;
  const fetchImpl = async url => {
    if (url.includes("operator-portraits.json")) return new Response(JSON.stringify(f.portraitMap));
    if (!healthy) return new Response("{}", { status: 500 });
    return new Response(JSON.stringify(f.manyOperators(12)));
  };

  const controller = app.boot(doc, { fetchImpl, storage: memoryStorage(), location: { hostname: "juulren.github.io", protocol: "https:" } });
  await controller.ready;

  assert.equal(doc.getElementById("error-state").hidden, false);
  assert.match(doc.getElementById("error-message").textContent, /500/);

  healthy = true;
  doc.getElementById("retry-button").click();
  await controller.ready;

  assert.equal(doc.getElementById("error-state").hidden, true);
  assert.ok(doc.querySelectorAll(".operator-card").length >= 10);
  assert.match(doc.getElementById("results-count").textContent, /12 operadores/);
  assert.equal(doc.getElementById("hero-total").textContent, "12");
});

test("boot rellena la vista previa del hero con tres operadores", async () => {
  const doc = loadPage();
  const fetchImpl = async url => new Response(JSON.stringify(url.includes("portraits") ? f.portraitMap : f.manyOperators(12)));

  const controller = app.boot(doc, { fetchImpl, storage: memoryStorage(), location: { hostname: "juulren.github.io", protocol: "https:" } });
  await controller.ready;

  const items = doc.querySelectorAll("#hero-showcase .showcase-item");
  assert.equal(items.length, 3);
  assert.equal(doc.querySelectorAll("#hero-showcase .is-loading").length, 0);
});

test("el menú móvil alterna aria-expanded y la clase is-open", () => {
  const doc = loadPage();
  app.initNavigation(doc);

  const toggle = doc.getElementById("menu-toggle");
  toggle.click();
  assert.equal(toggle.getAttribute("aria-expanded"), "true");
  assert.ok(doc.getElementById("site-menu").classList.contains("is-open"));

  doc.querySelector(".menu-link").click();
  assert.equal(toggle.getAttribute("aria-expanded"), "false");
});

test("el tagline se divide en palabras individuales para el revelado", () => {
  const doc = loadPage();
  app.splitTagline(doc);

  const words = [...doc.querySelectorAll("[data-tagline] .tagline-word")].map(w => w.textContent);
  assert.deepEqual(words.slice(0, 4), ["Cada", "operador", "tiene", "un"]);
  assert.equal(doc.querySelectorAll("[data-tagline] br").length, 4);
});
