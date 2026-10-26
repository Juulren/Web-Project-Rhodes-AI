// Pruebas de la lógica pura de los 5 retos: orden, estadísticas dinámicas, comparador, historial y tema.
"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const app = require("../../script.js");

function op(name, rarity, className, stats = {}) {
  return {
    id: name.toLowerCase().replace(/[^a-z0-9]+/g, "-"),
    name,
    rarity,
    className,
    affiliation: "Rhodes Island",
    affiliations: ["Rhodes Island"],
    stats: { hp: 1000, atk: 400, def: 200, res: 0, cost: 15, block: 1, interval: 1.2, redeploy: 70, ...stats }
  };
}

const catalog = [
  op("Saria", 6, "Defender", { hp: 3000, atk: 400, def: 600, res: 10, cost: 20, block: 3, interval: 1.2 }),
  op("'Justice Knight'", 1, "Sniper", { hp: 500, atk: 170, def: 40, cost: 3 }),
  op("Exusiai", 6, "Sniper", { hp: 1600, atk: 540, def: 160, cost: 12, interval: 1.0 }),
  op("Myrtle", 4, "Vanguard", { hp: 1300, atk: 330, def: 230, cost: 8 }),
  op("Ángel", 5, "Guard", { hp: 2000, atk: 500, def: 300, cost: 16 })
];

const names = list => list.map(o => o.name);

// =====================================================================
// Reto 2: orden alfabético y por rareza
// =====================================================================

test("sortOperators ordena de la A a la Z ignorando comillas y acentos", () => {
  assert.deepEqual(names(app.sortOperators(catalog, "name-asc")), ["Ángel", "Exusiai", "'Justice Knight'", "Myrtle", "Saria"]);
});

test("sortOperators ordena de la Z a la A", () => {
  assert.deepEqual(names(app.sortOperators(catalog, "name-desc")), ["Saria", "Myrtle", "'Justice Knight'", "Exusiai", "Ángel"]);
});

test("sortOperators ordena por rareza y desempata por nombre", () => {
  assert.deepEqual(names(app.sortOperators(catalog, "rarity-desc")), ["Exusiai", "Saria", "Ángel", "Myrtle", "'Justice Knight'"]);
  assert.deepEqual(names(app.sortOperators(catalog, "rarity-asc")), ["'Justice Knight'", "Myrtle", "Ángel", "Exusiai", "Saria"]);
});

test("sortOperators usa A a Z ante una clave desconocida y no muta la lista", () => {
  const copy = [...catalog];
  assert.deepEqual(names(app.sortOperators(catalog, "inventado")), names(app.sortOperators(catalog, "name-asc")));
  assert.deepEqual(catalog, copy);
});

// =====================================================================
// Reto 3: estadísticas dinámicas
// =====================================================================

test("computeStats promedia HP, ATK, DEF y coste del grupo visible", () => {
  const stats = app.computeStats(catalog);

  assert.equal(stats.total, 5);
  assert.equal(stats.avgHp, 1680);
  assert.equal(stats.avgAtk, 388);
  assert.equal(stats.avgDef, 266);
  assert.equal(stats.avgCost, 11.8);
});

test("computeStats calcula la distribución por clase en porcentaje, de mayor a menor", () => {
  const { classDistribution } = app.computeStats(catalog);

  assert.deepEqual(classDistribution, [
    { label: "Sniper", count: 2, percent: 40 },
    { label: "Defender", count: 1, percent: 20 },
    { label: "Guard", count: 1, percent: 20 },
    { label: "Vanguard", count: 1, percent: 20 }
  ]);
});

test("computeStats incluye las seis rarezas, de 6 a 1 estrella, aunque alguna tenga cero", () => {
  const { rarityDistribution } = app.computeStats(catalog);

  assert.deepEqual(rarityDistribution.map(r => r.label), ["6★", "5★", "4★", "3★", "2★", "1★"]);
  assert.deepEqual(rarityDistribution.map(r => r.count), [2, 1, 1, 0, 0, 1]);
  assert.deepEqual(rarityDistribution.map(r => r.percent), [40, 20, 20, 0, 0, 20]);
});

test("computeStats redondea porcentajes a un decimal", () => {
  const three = catalog.slice(0, 3);
  const { classDistribution } = app.computeStats(three);
  assert.deepEqual(classDistribution.map(c => c.percent), [66.7, 33.3]);
});

test("computeStats con lista vacía devuelve ceros y no divide por cero", () => {
  const stats = app.computeStats([]);

  assert.equal(stats.total, 0);
  assert.equal(stats.avgHp, 0);
  assert.equal(stats.avgCost, 0);
  assert.deepEqual(stats.classDistribution, []);
  assert.ok(stats.rarityDistribution.every(r => r.count === 0 && r.percent === 0));
});

// =====================================================================
// Reto 4: comparador de dos operadores
// =====================================================================

test("compareOperators enfrenta los 7 atributos en el orden de la tarea", () => {
  const result = app.compareOperators(catalog[0], catalog[2]);
  assert.deepEqual(result.attributes.map(a => a.label), ["HP", "ATK", "DEF", "RES", "Coste", "Bloqueo", "Intervalo de ataque"]);
});

test("en HP, ATK, DEF, RES y bloqueo gana el valor mayor", () => {
  const { attributes } = app.compareOperators(catalog[0], catalog[2]);
  const byKey = Object.fromEntries(attributes.map(a => [a.key, a]));

  assert.equal(byKey.hp.winner, "A");
  assert.equal(byKey.atk.winner, "B");
  assert.equal(byKey.def.winner, "A");
  assert.equal(byKey.res.winner, "A");
  assert.equal(byKey.block.winner, "A");
});

test("en coste e intervalo de ataque gana el valor menor", () => {
  const { attributes } = app.compareOperators(catalog[0], catalog[2]);
  const byKey = Object.fromEntries(attributes.map(a => [a.key, a]));

  assert.equal(byKey.cost.winner, "B");
  assert.equal(byKey.interval.winner, "B");
  assert.equal(byKey.cost.lowerIsBetter, true);
});

test("las barras se escalan respecto al mayor valor del atributo", () => {
  const { attributes } = app.compareOperators(catalog[0], catalog[2]);
  const hp = attributes.find(a => a.key === "hp");

  assert.equal(hp.ratioA, 1);
  assert.equal(hp.ratioB, 1600 / 3000);
  const res = attributes.find(a => a.key === "res");
  assert.equal(res.ratioB, 0);
});

test("compareOperators cuenta victorias y decide el ganador general", () => {
  const result = app.compareOperators(catalog[0], catalog[2]);

  assert.equal(result.winsA, 4);
  assert.equal(result.winsB, 3);
  assert.equal(result.overall, "A");
});

test("comparar un operador consigo mismo es empate en todo", () => {
  const result = app.compareOperators(catalog[1], catalog[1]);

  assert.ok(result.attributes.every(a => a.winner === "Empate"));
  assert.equal(result.overall, "Empate");
  assert.ok(result.attributes.every(a => a.ratioA === a.ratioB));
});

// =====================================================================
// Reto 5: historial de búsquedas
// =====================================================================

function memoryStorage(initial = {}) {
  const data = new Map(Object.entries(initial));
  return { getItem: k => (data.has(k) ? data.get(k) : null), setItem: (k, v) => data.set(k, String(v)), removeItem: k => data.delete(k) };
}

test("el historial guarda la búsqueda más reciente primero y la persiste", () => {
  const storage = memoryStorage();
  const history = app.createSearchHistory(storage);

  history.add("Exusiai");
  history.add("  lungmen  ");

  assert.deepEqual(history.list(), ["lungmen", "Exusiai"]);
  assert.deepEqual(app.createSearchHistory(storage).list(), ["lungmen", "Exusiai"]);
});

test("el historial no duplica términos aunque cambien mayúsculas o acentos", () => {
  const history = app.createSearchHistory(memoryStorage());

  history.add("pozëmka");
  history.add("Exusiai");
  history.add("POZEMKA");

  assert.deepEqual(history.list(), ["POZEMKA", "Exusiai"]);
});

test("el historial ignora términos de menos de 2 caracteres", () => {
  const history = app.createSearchHistory(memoryStorage());
  history.add("a");
  history.add("   ");
  assert.deepEqual(history.list(), []);
});

test("el historial conserva como máximo 8 búsquedas", () => {
  const history = app.createSearchHistory(memoryStorage());
  for (let i = 1; i <= 10; i++) history.add(`busqueda ${i}`);

  assert.equal(history.list().length, 8);
  assert.equal(history.list()[0], "busqueda 10");
  assert.equal(history.list()[7], "busqueda 3");
});

test("clear vacía el historial y una entrada corrupta se ignora", () => {
  const storage = memoryStorage({ [app.CONFIG.historyKey]: "[1, null, \"saria\"" });
  const history = app.createSearchHistory(storage);
  assert.deepEqual(history.list(), []);

  history.add("saria");
  history.clear();
  assert.deepEqual(app.createSearchHistory(storage).list(), []);
});

// =====================================================================
// Reto 1: tema oscuro y claro
// =====================================================================

const media = matches => () => ({ matches });

test("resolveInitialTheme prefiere la elección guardada", () => {
  assert.equal(app.resolveInitialTheme(memoryStorage({ [app.CONFIG.themeKey]: "light" }), media(false)), "light");
  assert.equal(app.resolveInitialTheme(memoryStorage({ [app.CONFIG.themeKey]: "dark" }), media(true)), "dark");
});

test("sin elección guardada respeta prefers-color-scheme y por defecto usa el tema oscuro", () => {
  assert.equal(app.resolveInitialTheme(memoryStorage(), media(true)), "light");
  assert.equal(app.resolveInitialTheme(memoryStorage(), media(false)), "dark");
  assert.equal(app.resolveInitialTheme(memoryStorage({ [app.CONFIG.themeKey]: "rosa" }), undefined), "dark");
});
