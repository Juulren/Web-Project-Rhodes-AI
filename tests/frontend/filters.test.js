// Pruebas del motor de búsqueda y filtros combinados.
"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const app = require("../../script.js");

function op(name, overrides = {}) {
  return {
    id: name.toLowerCase().replace(/[^a-z0-9]+/g, "-"),
    name,
    rarity: 6,
    className: "Guard",
    archetype: "",
    affiliation: "Rhodes Island",
    affiliations: ["Rhodes Island"],
    description: "",
    biography: "",
    stats: { hp: 1000, atk: 500, def: 300, res: 0, cost: 15, block: 2, interval: 1.2, redeploy: 70 },
    ...overrides
  };
}

const catalog = [
  op("Ch'en", { affiliation: "Great Lungmen", affiliations: ["Great Lungmen", "L.G.D."], description: "Commander of the L.G.D. Special Inspection Unit." }),
  op("Hoshiguma", { className: "Defender", affiliation: "Great Lungmen", affiliations: ["Great Lungmen", "L.G.D."] }),
  op("Swire", { rarity: 5, affiliation: "Great Lungmen", affiliations: ["Great Lungmen", "L.G.D."] }),
  op("Exusiai", { className: "Sniper", affiliation: "Penguin Logistics", affiliations: ["Penguin Logistics"], description: "Shoots enemies with rapid bursts." }),
  op("Ptilopsis", { rarity: 5, className: "Medic", archetype: "Multi-target", affiliation: "Rhine Lab", affiliations: ["Rhine Lab"], biography: "Heals allies over a wide area." }),
  op("Pozëmka", { className: "Sniper", affiliation: "Ursus", affiliations: ["Ursus"] }),
  op("Lancet-2", { rarity: 1, className: "Medic", affiliation: "Rhodes Island", affiliations: ["Rhodes Island"] })
];

const names = list => list.map(o => o.name);

test("normalizeSearch quita acentos, mayúsculas y espacios sobrantes", () => {
  assert.equal(app.normalizeSearch("  PozËmka   Ursus "), "pozemka ursus");
  assert.equal(app.normalizeSearch(undefined), "");
});

test("sin criterios se devuelven todos los operadores", () => {
  assert.equal(app.filterOperators(catalog, {}).length, catalog.length);
  assert.equal(app.filterOperators(catalog, { search: "   " }).length, catalog.length);
});

test("la búsqueda encuentra coincidencias parciales del nombre sin importar mayúsculas ni acentos", () => {
  assert.deepEqual(names(app.filterOperators(catalog, { search: "HOSHI" })), ["Hoshiguma"]);
  assert.deepEqual(names(app.filterOperators(catalog, { search: "pozem" })), ["Pozëmka"]);
  assert.deepEqual(names(app.filterOperators(catalog, { search: "ch'en" })), ["Ch'en"]);
});

test("la búsqueda incluye facción, descripción, biografía y arquetipo", () => {
  assert.deepEqual(names(app.filterOperators(catalog, { search: "penguin" })), ["Exusiai"]);
  assert.deepEqual(names(app.filterOperators(catalog, { search: "rapid bursts" })), ["Exusiai"]);
  assert.deepEqual(names(app.filterOperators(catalog, { search: "heals" })), ["Ptilopsis"]);
  assert.deepEqual(names(app.filterOperators(catalog, { search: "multi" })), ["Ptilopsis"]);
  assert.equal(app.filterOperators(catalog, { search: "l.g.d." }).length, 3);
});

test("varias palabras deben coincidir todas", () => {
  assert.deepEqual(names(app.filterOperators(catalog, { search: "lungmen defender" })), ["Hoshiguma"]);
  assert.deepEqual(app.filterOperators(catalog, { search: "lungmen penguin" }), []);
});

test("los filtros de clase, rareza y afiliación se combinan entre sí y con la búsqueda", () => {
  assert.deepEqual(names(app.filterOperators(catalog, { className: "Sniper" })), ["Exusiai", "Pozëmka"]);
  assert.deepEqual(names(app.filterOperators(catalog, { rarity: "5" })), ["Swire", "Ptilopsis"]);
  assert.deepEqual(names(app.filterOperators(catalog, { affiliation: "Great Lungmen", rarity: 6 })), ["Ch'en", "Hoshiguma"]);
  assert.deepEqual(names(app.filterOperators(catalog, { search: "lungmen", className: "Guard", rarity: 6 })), ["Ch'en"]);
  assert.deepEqual(names(app.filterOperators(catalog, { className: "Medic", rarity: 1 })), ["Lancet-2"]);
});

test("la afiliación coincide también con afiliaciones secundarias", () => {
  assert.equal(app.filterOperators(catalog, { affiliation: "l.g.d." }).length, 3);
});

test("una combinación sin resultados devuelve una lista vacía", () => {
  assert.deepEqual(app.filterOperators(catalog, { className: "Defender", rarity: 1 }), []);
  assert.deepEqual(app.filterOperators(catalog, { search: "nadie se llama así" }), []);
});

test("filterOperators no modifica la lista original", () => {
  const copy = [...catalog];
  app.filterOperators(catalog, { className: "Sniper" });
  assert.deepEqual(catalog, copy);
});

test("collectAffiliations lista cada afiliación principal una vez, con su conteo y en orden alfabético", () => {
  assert.deepEqual(app.collectAffiliations(catalog), [
    { name: "Great Lungmen", count: 3 },
    { name: "Penguin Logistics", count: 1 },
    { name: "Rhine Lab", count: 1 },
    { name: "Rhodes Island", count: 1 },
    { name: "Ursus", count: 1 }
  ]);
});

test("hasActiveFilters detecta cualquier criterio activo", () => {
  assert.equal(app.hasActiveFilters({}), false);
  assert.equal(app.hasActiveFilters({ search: "  ", className: "", rarity: "", affiliation: "" }), false);
  assert.equal(app.hasActiveFilters({ rarity: "6" }), true);
  assert.equal(app.hasActiveFilters({ search: "amiya" }), true);
});
