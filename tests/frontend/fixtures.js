// Muestras basadas en respuestas reales de RhodesAPI, con sus irregularidades:
// números como texto, "block: 2", afiliación como texto o arreglo, tiers faltantes y enlaces rotos.
"use strict";

const GAMEPRESS = "https://ak.gamepress.gg/sites/default/files";

const saria = {
  name: "Saria",
  rarity: 6,
  class: ["Defender", "Healing", "Guardian"],
  affiliation: ["Columbia", "Rhine Lab"],
  description: "Saria, former Rhine Lab Defense Department head, is a reliable defender.",
  biography: "Saria is a former head of the Rhine Lab Defense Department.",
  art: [
    { name: "Base", link: `${GAMEPRESS}/2019-10/char_202_demkni_1.png` },
    { name: "E2", link: `${GAMEPRESS}/2019-10/char_202_demkni_2.png` }
  ],
  statistics: {
    base: { hp: "1309", atk: "200", def: "248", resist: "18", deploy: "70", cost: "09", interval: "1.2", block: "block: 2" },
    e0max: { hp: "1687", atk: "260", def: "325", block: "2" },
    e1max: { hp: "2124", atk: "325", def: "414", block: "3" },
    e2max: { hp: "2630", atk: "390", def: "575", block: "3" }
  }
};

const exusiai = {
  name: "Exusiai",
  rarity: 6,
  class: ["Sniper", "Marksman", "Marksman"],
  affiliation: "Penguin Logistics",
  description: "Exusiai, an agent of Penguin Logistics, shoots enemies with rapid bursts.",
  biography: "A courier from Laterano.",
  art: [
    { name: "Base", link: `${GAMEPRESS}/2019-10/char_103_angel_1.png` },
    { name: "E2", link: `${GAMEPRESS}/2019-10/char_103_angel_2.png` }
  ],
  statistics: {
    base: { hp: "711", atk: "183", def: "57", resist: "1", deploy: "70", cost: "1", interval: "1", block: " 1" },
    e0max: { hp: "1019", atk: "353", def: "80", block: "1" },
    e1max: { hp: "1320", atk: "467", def: "123", block: "1" },
    e2max: { hp: "1673", atk: "540", def: "161", block: "1" }
  }
};

// Operador reciente: los archivos de gamepress tienen nombre en chino, sin charId.
const ines = {
  name: "Ines",
  rarity: 6,
  class: ["Vanguard", "Agent"],
  affiliation: "Rhodes Island",
  description: "",
  biography: "Ines is a mercenary from Kazdel who works for Rhodes Island.",
  art: [{ name: "Base", link: `${GAMEPRESS}/2023-04/%E7%AB%8B%E7%BB%98_%E4%BC%8A%E5%86%85%E4%B8%9D_1.png` }],
  statistics: {
    base: { hp: "1000", atk: "300", def: "200", resist: "0", deploy: "35", cost: "11", interval: "1", block: "block: 1" },
    e2max: { hp: "2000", atk: "650", def: "350", block: "1" }
  }
};

// Unidad de 4 estrellas sin bloque e2max en la API.
const myrtle = {
  name: "Myrtle",
  rarity: 4,
  class: ["Vanguard", "Standard Bearer"],
  affiliation: "Rhodes Island",
  description: "Myrtle generates DP for the team.",
  biography: "A Liberi from Laterano.",
  art: [{ name: "Base", link: `${GAMEPRESS}/2019-10/char_151_myrtle_1.png` }],
  statistics: {
    base: { hp: "800", atk: "200", def: "150", resist: "0", deploy: "70", cost: "10", interval: "1.3", block: "1" },
    e0max: { hp: "1000", atk: "260", def: "180", block: "1" },
    e1max: { hp: "1250", atk: "320", def: "220", block: "1" }
  }
};

// Registro con rareza 0 que RhodesAPI devuelve para unidades de reserva.
const reserveDefender = {
  name: "Reserve Operator - Defender",
  rarity: 0,
  class: ["Defender"],
  affiliation: "Rhodes Island",
  description: "",
  biography: "",
  art: [],
  statistics: { base: { hp: "500", atk: "100", def: "200", resist: "0", cost: "8", interval: "1.2", block: "2" } }
};

// Estadísticas vacías en el tier máximo (caso real de Ulpianus).
const ulpianus = {
  name: "Ulpianus",
  rarity: 6,
  class: ["Guard", "Primal"],
  affiliation: ["Aegir", "Abyssal"],
  description: "An Abyssal Hunter.",
  biography: "Ulpianus is one of the Abyssal Hunters.",
  art: [{ name: "Base", link: `${GAMEPRESS}/2024-01/char_4145_ulpia_1.png` }],
  statistics: {
    base: { hp: "", atk: "", def: "", resist: "", deploy: "", cost: "", interval: "", block: "" },
    e0max: { hp: "1900", atk: "500", def: "300", block: "1" },
    e2max: { hp: "", atk: "", def: "", block: "" }
  }
};

const unknownClass = { ...exusiai, name: "Mystery", class: ["Pilot"] };

const portraitMap = {
  base: "https://raw.githubusercontent.com/yuanyan3060/ArknightsGameResource/main/portrait/",
  portraits: { Saria: "char_202_demkni_2", Ines: "char_4087_ines_2", Exusiai: "char_103_angel_2" }
};

/** Genera n operadores válidos y distintos a partir de Exusiai. */
function manyOperators(n) {
  return Array.from({ length: n }, (_, i) => ({
    ...exusiai,
    name: `Operador ${String.fromCharCode(65 + i)}`,
    rarity: (i % 6) + 1,
    class: [["Vanguard", "Guard", "Defender", "Sniper", "Caster", "Medic", "Supporter", "Specialist"][i % 8]],
    affiliation: "Rhodes Island",
    description: `Descripción del operador ${i + 1}.`,
    biography: "Operador de prueba."
  }));
}

module.exports = { saria, exusiai, ines, myrtle, reserveDefender, ulpianus, unknownClass, portraitMap, manyOperators };
