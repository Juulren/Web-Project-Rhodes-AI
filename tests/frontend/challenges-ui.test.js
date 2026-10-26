// Integración de los 5 retos en el index.html real (jsdom).
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
const rawCatalog = [f.saria, f.exusiai, f.ines, f.myrtle, ...f.manyOperators(10)];

function memoryStorage(initial = {}) {
  const data = new Map(Object.entries(initial));
  return { getItem: k => (data.has(k) ? data.get(k) : null), setItem: (k, v) => data.set(k, String(v)), removeItem: k => data.delete(k) };
}

async function bootPage(storage = memoryStorage(), { prefersLight = false } = {}) {
  const dom = new JSDOM(html, { url: "https://juulren.github.io/Web-Project-Rhodes-AI/" });
  dom.window.matchMedia = query => ({ matches: prefersLight && query.includes("light"), media: query, addEventListener() {} });
  const doc = dom.window.document;
  const fetchImpl = async url => new Response(JSON.stringify(url.includes("portraits") ? f.portraitMap : rawCatalog));
  const controller = app.boot(doc, { fetchImpl, storage, location: PAGES, searchDelayMs: 0 });
  await controller.ready;
  return { doc, window: dom.window, storage };
}

const visibleNames = doc => [...doc.querySelectorAll("#operator-grid .card-name")].map(n => n.textContent);
const stat = (doc, key) => doc.querySelector(`[data-stat='${key}']`).textContent;

function fire(window, element, type) {
  element.dispatchEvent(new window.Event(type, { bubbles: true, cancelable: true }));
}

function search(doc, window, value) {
  const input = doc.getElementById("search-input");
  input.value = value;
  fire(window, input, "input");
  fire(window, input, "change");
}

function choose(doc, window, id, value) {
  const select = doc.getElementById(id);
  select.value = value;
  fire(window, select, "change");
}

// ---------- Reto 1: tema ----------

test("el botón de tema alterna entre oscuro y claro y guarda la preferencia", async () => {
  const { doc, storage } = await bootPage();
  const html = doc.documentElement;
  const toggle = doc.getElementById("theme-toggle");

  assert.equal(html.dataset.theme, "dark");
  assert.equal(toggle.getAttribute("aria-pressed"), "false");

  toggle.click();

  assert.equal(html.dataset.theme, "light");
  assert.equal(toggle.getAttribute("aria-pressed"), "true");
  assert.equal(toggle.getAttribute("aria-label"), "Cambiar a tema oscuro");
  assert.equal(storage.getItem(app.CONFIG.themeKey), "light");
  assert.equal(doc.querySelector("meta[name='theme-color']").getAttribute("content"), "#ECEAE3");

  toggle.click();
  assert.equal(doc.querySelector("meta[name='theme-color']").getAttribute("content"), "#07090A");
});

test("el tema guardado se aplica al cargar", async () => {
  const { doc } = await bootPage(memoryStorage({ [app.CONFIG.themeKey]: "light" }));
  assert.equal(doc.documentElement.dataset.theme, "light");
  assert.equal(doc.getElementById("theme-toggle").getAttribute("aria-pressed"), "true");
});

test("sin preferencia guardada se respeta prefers-color-scheme", async () => {
  const { doc } = await bootPage(memoryStorage(), { prefersLight: true });
  assert.equal(doc.documentElement.dataset.theme, "light");
});

// ---------- Reto 2: orden ----------

test("el selector Ordenar por reordena la grilla visible", async () => {
  const { doc, window } = await bootPage();

  choose(doc, window, "sort-select", "name-desc");
  assert.equal(visibleNames(doc)[0], "Saria");

  choose(doc, window, "sort-select", "rarity-desc");
  assert.deepEqual(visibleNames(doc).slice(0, 4), ["Exusiai", "Ines", "Operador F", "Saria"]);

  choose(doc, window, "sort-select", "rarity-asc");
  assert.deepEqual(visibleNames(doc).slice(0, 2), ["Operador A", "Operador G"]);
});

test("el orden se mantiene al filtrar", async () => {
  const { doc, window } = await bootPage();
  choose(doc, window, "sort-select", "name-desc");
  choose(doc, window, "filter-class", "Vanguard");

  assert.deepEqual(visibleNames(doc), ["Operador I", "Operador A", "Myrtle", "Ines"]);
});

// ---------- Reto 3: estadísticas dinámicas ----------

test("el panel de inteligencia refleja el grupo visible y se recalcula con cada filtro", async () => {
  const { doc, window } = await bootPage();
  assert.equal(stat(doc, "total"), "14");

  choose(doc, window, "filter-class", "Defender");

  assert.equal(stat(doc, "total"), "2");
  const defenders = app.normalizeList(rawCatalog, "rhodesapi", f.portraitMap).filter(o => o.className === "Defender");
  const expected = app.computeStats(defenders);
  assert.equal(stat(doc, "hp"), app.formatNumber(expected.avgHp));
  assert.equal(stat(doc, "cost"), app.formatNumber(expected.avgCost));
});

test("las distribuciones por clase y rareza muestran filas con porcentaje", async () => {
  const { doc, window } = await bootPage();
  choose(doc, window, "filter-class", "Vanguard");

  const classRows = doc.querySelectorAll("#class-distribution .dist-row");
  assert.equal(classRows.length, 1);
  assert.match(classRows[0].textContent, /Vanguard/);
  assert.match(classRows[0].textContent, /100 %/);
  assert.equal(classRows[0].querySelector(".dist-fill").style.getPropertyValue("--value"), "1");

  const rarityRows = doc.querySelectorAll("#rarity-distribution .dist-row");
  assert.equal(rarityRows.length, 6);
  assert.match(rarityRows[0].textContent, /6★/);
});

test("sin resultados el panel muestra ceros", async () => {
  const { doc, window } = await bootPage();
  assert.equal(stat(doc, "total"), "14");
  assert.notEqual(stat(doc, "hp"), "0");

  search(doc, window, "nadie se llama así");

  assert.equal(stat(doc, "total"), "0");
  assert.equal(stat(doc, "hp"), "0");
  assert.equal(doc.querySelectorAll("#class-distribution .dist-row").length, 0);
});

// ---------- Reto 4: comparador ----------

const cardButton = (doc, id, action) => doc.querySelector(`.operator-card[data-operator-id='${id}'] [data-action='${action}']`);

test("el botón Comparar de la barra abre el modal con los selectores llenos", async () => {
  const { doc } = await bootPage();

  doc.querySelector("[data-open-compare]").click();

  assert.equal(doc.getElementById("compare-modal").hasAttribute("open"), true);
  assert.equal(doc.querySelectorAll("#compare-a option").length, 15);
  assert.equal(doc.querySelectorAll("#compare-b option").length, 15);
});

test("Comparar en dos tarjetas llena A y B y muestra el cara a cara", async () => {
  const { doc } = await bootPage();

  cardButton(doc, "saria", "compare").click();
  assert.equal(doc.getElementById("compare-a").value, "saria");

  cardButton(doc, "exusiai", "compare").click();
  assert.equal(doc.getElementById("compare-b").value, "exusiai");

  const rows = doc.querySelectorAll("#compare-results .compare-row");
  assert.equal(rows.length, 7);
  const hp = [...rows].find(r => r.dataset.attribute === "hp");
  assert.ok(hp.classList.contains("winner-a"));
  const cost = [...rows].find(r => r.dataset.attribute === "cost");
  assert.ok(cost.classList.contains("winner-b"), "en coste gana el valor menor");
  assert.match(doc.getElementById("compare-results").textContent, /Saria gana en \d de 7 atributos/);
});

test("cambiar un selector actualiza el resultado e Intercambiar invierte A y B", async () => {
  const { doc, window } = await bootPage();
  doc.querySelector("[data-open-compare]").click();

  choose(doc, window, "compare-a", "myrtle");
  assert.match(doc.getElementById("compare-results").textContent, /Elige dos operadores/);

  choose(doc, window, "compare-b", "saria");
  assert.equal(doc.querySelectorAll("#compare-results .compare-row").length, 7);

  doc.getElementById("compare-swap").click();
  assert.equal(doc.getElementById("compare-a").value, "saria");
  assert.equal(doc.getElementById("compare-b").value, "myrtle");
});

test("elegir el mismo operador en A y B muestra un aviso en lugar del cara a cara", async () => {
  const { doc, window } = await bootPage();
  doc.querySelector("[data-open-compare]").click();
  choose(doc, window, "compare-a", "saria");
  choose(doc, window, "compare-b", "saria");

  assert.equal(doc.querySelectorAll("#compare-results .compare-row").length, 0);
  assert.match(doc.getElementById("compare-results").textContent, /dos operadores distintos/);
});

// ---------- Reto 5: historial ----------

test("una búsqueda confirmada aparece como chip y al pulsarlo se repite", async () => {
  const { doc, window } = await bootPage();

  search(doc, window, "penguin");
  search(doc, window, "");

  const chips = doc.querySelectorAll("#search-history .chip");
  assert.equal(chips.length, 1);
  assert.equal(chips[0].textContent.trim(), "penguin");
  assert.equal(doc.getElementById("clear-history").hidden, false);

  chips[0].click();
  assert.equal(doc.getElementById("search-input").value, "penguin");
  assert.deepEqual(visibleNames(doc), ["Exusiai"]);
});

test("Enter en el buscador también registra la búsqueda", async () => {
  const { doc, window } = await bootPage();
  const input = doc.getElementById("search-input");
  input.value = "saria";
  fire(window, input, "input");
  input.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Enter", bubbles: true }));

  assert.equal(doc.querySelectorAll("#search-history .chip").length, 1);
});

test("Limpiar historial borra los chips y el almacenamiento", async () => {
  const storage = memoryStorage();
  const { doc, window } = await bootPage(storage);
  search(doc, window, "penguin");
  search(doc, window, "lungmen");

  doc.getElementById("clear-history").click();

  assert.equal(doc.querySelectorAll("#search-history .chip").length, 0);
  assert.equal(doc.getElementById("clear-history").hidden, true);
  assert.deepEqual(JSON.parse(storage.getItem(app.CONFIG.historyKey)), []);
});

test("el historial guardado se muestra al recargar", async () => {
  const storage = memoryStorage({ [app.CONFIG.historyKey]: JSON.stringify(["lungmen", "exusiai"]) });
  const { doc } = await bootPage(storage);

  assert.deepEqual([...doc.querySelectorAll("#search-history .chip")].map(c => c.textContent.trim()), ["lungmen", "exusiai"]);
});
