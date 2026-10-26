// Pruebas de la capa de normalización: convierten el JSON irregular de RhodesAPI en un modelo estable.
"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const app = require("../../script.js");
const f = require("./fixtures.js");

test("parseNumber acepta números, texto con espacios, prefijos y ceros a la izquierda", () => {
  assert.equal(app.parseNumber(12), 12);
  assert.equal(app.parseNumber(" 1"), 1);
  assert.equal(app.parseNumber("block: 2"), 2);
  assert.equal(app.parseNumber("09"), 9);
  assert.equal(app.parseNumber("1.05"), 1.05);
  assert.equal(app.parseNumber(""), null);
  assert.equal(app.parseNumber(undefined), null);
  assert.equal(app.parseNumber("sin datos"), null);
});

test("normalizeOperator mapea un operador completo usando el tier más alto", () => {
  const op = app.normalizeOperator(f.saria, f.portraitMap);

  assert.equal(op.id, "saria");
  assert.equal(op.name, "Saria");
  assert.equal(op.rarity, 6);
  assert.equal(op.className, "Defender");
  assert.equal(op.archetype, "Guardian");
  assert.equal(op.promotion, "Elite2");
  assert.deepEqual(op.stats, { hp: 2630, atk: 390, def: 575, res: 18, cost: 9, block: 3, interval: 1.2, redeploy: 70 });
});

test("la afiliación puede llegar como texto o como arreglo", () => {
  const saria = app.normalizeOperator(f.saria, f.portraitMap);
  const exusiai = app.normalizeOperator(f.exusiai, f.portraitMap);

  assert.equal(saria.affiliation, "Columbia");
  assert.deepEqual(saria.affiliations, ["Columbia", "Rhine Lab"]);
  assert.equal(exusiai.affiliation, "Penguin Logistics");
  assert.deepEqual(exusiai.affiliations, ["Penguin Logistics"]);
});

test("block con formato 'block: 2' se usa cuando el tier no lo trae", () => {
  const raw = structuredClone(f.saria);
  delete raw.statistics.e2max.block;
  assert.equal(app.normalizeOperator(raw, f.portraitMap).stats.block, 2);
});

test("sin bloque e2max se usa el de Elite 1 y se informa la promoción real", () => {
  const op = app.normalizeOperator(f.myrtle, f.portraitMap);

  assert.equal(op.promotion, "Elite1");
  assert.equal(op.stats.hp, 1250);
  assert.equal(op.stats.cost, 10);
});

test("un tier con valores vacíos se ignora y nunca produce NaN", () => {
  const op = app.normalizeOperator(f.ulpianus, f.portraitMap);

  assert.equal(op.promotion, "Base");
  assert.equal(op.stats.hp, 1900);
  for (const value of Object.values(op.stats)) assert.ok(Number.isFinite(value), `valor no numérico: ${value}`);
  assert.equal(op.stats.interval, 1);
});

test("se descarta un operador cuyos tiers sólo traen el objeto de error de RhodesAPI", () => {
  const gpError = { error: "something went wrong, most likely operator does not have stats yet on gp" };
  const raw = { ...f.ulpianus, statistics: { base: gpError, e0max: gpError, e1max: gpError, e2max: gpError } };
  assert.equal(app.normalizeOperator(raw, f.portraitMap), null);
});

test("los retratos usan el mapa local porque ak.gamepress.gg ya no existe", () => {
  const op = app.normalizeOperator(f.saria, f.portraitMap);
  assert.equal(op.art, `${f.portraitMap.base}char_202_demkni_2.png`);
  assert.ok(!op.art.includes("gamepress"));
});

test("sin entrada en el mapa, el charId del enlace genera la ruta del retrato", () => {
  const op = app.normalizeOperator(f.myrtle, f.portraitMap);
  assert.equal(op.art, `${f.portraitMap.base}char_151_myrtle_2.png`);
});

test("sin charId ni entrada en el mapa se usa el placeholder local", () => {
  const op = app.normalizeOperator(f.ines, { base: f.portraitMap.base, portraits: {} });
  assert.equal(op.art, app.CONFIG.placeholderArt);
});

test("la descripción vacía cae en la biografía", () => {
  const op = app.normalizeOperator(f.ines, f.portraitMap);
  assert.equal(op.description, "Ines is a mercenary from Kazdel who works for Rhodes Island.");
});

test("se descartan registros con rareza fuera de 1 a 6, clase desconocida o sin nombre", () => {
  assert.equal(app.normalizeOperator(f.reserveDefender, f.portraitMap), null);
  assert.equal(app.normalizeOperator(f.unknownClass, f.portraitMap), null);
  assert.equal(app.normalizeOperator({ ...f.exusiai, name: "  " }, f.portraitMap), null);
  assert.equal(app.normalizeOperator(null, f.portraitMap), null);
});

test("normalizeBackendOperator adapta el OperatorDto del backend C#", () => {
  const dto = {
    id: "saria", name: "Saria", rarity: 6, class: "Defender", archetype: "Guardian", affiliation: "Columbia",
    description: "", biography: "Bio de Saria", artUrl: "", promotion: "Elite2",
    stats: { hp: 2630, atk: 390, def: 575, res: 18, cost: 9, block: 3, attackInterval: 1.2, redeployTime: 70 },
    combatPower: 3402.5
  };

  const op = app.normalizeBackendOperator(dto, f.portraitMap);

  assert.equal(op.className, "Defender");
  assert.equal(op.description, "Bio de Saria");
  assert.equal(op.art, `${f.portraitMap.base}char_202_demkni_2.png`);
  assert.deepEqual(op.stats, { hp: 2630, atk: 390, def: 575, res: 18, cost: 9, block: 3, interval: 1.2, redeploy: 70 });
  assert.equal(op.combatPower, 3402.5);
});

test("normalizeList elimina inválidos y duplicados y ordena por nombre", () => {
  const list = app.normalizeList([f.saria, f.reserveDefender, f.exusiai, f.saria, f.myrtle], "rhodesapi", f.portraitMap);
  assert.deepEqual(list.map(o => o.name), ["Exusiai", "Myrtle", "Saria"]);
});

test("normalizeList ordena ignorando comillas y acentos", () => {
  const raw = ["'Justice Knight'", "Ángel", "Amiya", "Kal'tsit"].map(name => ({ ...f.exusiai, name }));
  const names = app.normalizeList(raw, "rhodesapi", f.portraitMap).map(o => o.name);
  assert.deepEqual(names, ["Amiya", "Ángel", "'Justice Knight'", "Kal'tsit"]);
});

test("normalizeList rechaza respuestas que no son arreglos", () => {
  assert.throws(() => app.normalizeList({ error: "x" }, "rhodesapi", f.portraitMap), app.ApiError);
});
