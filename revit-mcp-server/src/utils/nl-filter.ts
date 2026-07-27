/**
 * Deterministic natural-language → structured-filter parser for Revit
 * element queries, e.g. "all doors wider than 900mm in Level 1".
 *
 * MCP clients (Claude, Cursor, …) usually translate user intent into the
 * structured `filters` argument themselves; this parser makes plain-English
 * queries work too, without any AI dependency. The C# plugin evaluates the
 * conditions numerically in Revit's internal units.
 */

export interface FilterCondition {
    parameter: string;
    operator: string; // > >= < <= = != contains levelmatch
    value: string | number;
    unit?: string;
}

export interface ParsedQuery {
    category?: string;
    level?: string;
    conditions: FilterCondition[];
}

const CATEGORY_WORDS: Record<string, string> = {
    door: "Doors", doors: "Doors",
    window: "Windows", windows: "Windows",
    wall: "Walls", walls: "Walls",
    floor: "Floors", floors: "Floors",
    room: "Rooms", rooms: "Rooms",
    ceiling: "Ceilings", ceilings: "Ceilings",
    column: "Columns", columns: "Columns",
    beam: "Structural Framing", beams: "Structural Framing",
    pipe: "Pipes", pipes: "Pipes",
    duct: "Ducts", ducts: "Ducts",
    stair: "Stairs", stairs: "Stairs",
    roof: "Roofs", roofs: "Roofs",
    furniture: "Furniture",
};

// Comparative adjectives → the Revit parameter they refer to
const DIMENSION_WORDS: Record<string, string> = {
    wider: "Width", narrower: "Width",
    taller: "Height", shorter: "Height", higher: "Height",
    longer: "Length",
    thicker: "Thickness", thinner: "Thickness",
    bigger: "Area", larger: "Area", smaller: "Area",
};

// Plain nouns usable in "width over 900mm" phrasing
const DIMENSION_NOUNS: Record<string, string> = {
    width: "Width", height: "Height", length: "Length",
    thickness: "Thickness", area: "Area", volume: "Volume",
};

const LESS_WORDS = new Set(["narrower", "shorter", "thinner", "smaller"]);

const UNIT_PATTERN =
    "mm|millimeters?|cm|centimeters?|m2|m²|sqm|ft2|ft²|sqft|m3|m³|ft3|ft³|m|meters?|metres?|ft|feet|foot|in|inch|inches";

function normalizeUnit(u: string | undefined): string | undefined {
    if (!u) return undefined;
    const unit = u.toLowerCase();
    if (unit.startsWith("mill")) return "mm";
    if (unit.startsWith("cent")) return "cm";
    if (unit.startsWith("met")) return "m";
    if (unit === "feet" || unit === "foot") return "ft";
    if (unit.startsWith("inch")) return "in";
    if (unit === "sqm" || unit === "m²") return "m2";
    if (unit === "sqft" || unit === "ft²") return "ft2";
    if (unit === "m³") return "m3";
    if (unit === "ft³") return "ft3";
    return unit;
}

/** Parse a natural-language element query into category/level/conditions. */
export function parseElementQuery(query: string): ParsedQuery {
    const text = (query || "").toLowerCase();
    const result: ParsedQuery = { conditions: [] };

    // Category: first recognized noun wins
    for (const word of text.split(/[^a-z]+/)) {
        if (CATEGORY_WORDS[word]) {
            result.category = CATEGORY_WORDS[word];
            break;
        }
    }

    // Level: "in/on/at (the) level 1", "in level L1", quoted names too
    const levelMatch = text.match(
        /(?:in|on|at)\s+(?:the\s+)?["']?level\s+([\w.-]+)["']?/
    );
    if (levelMatch) {
        result.level = `Level ${levelMatch[1]}`;
    }

    // "wider than 900mm", "taller than 2.1 m", "at least 900 mm wide"
    const cmpRe = new RegExp(
        `(${Object.keys(DIMENSION_WORDS).join("|")})\\s+than\\s+(\\d+(?:\\.\\d+)?)\\s*(${UNIT_PATTERN})?\\b`,
        "g"
    );
    for (const m of text.matchAll(cmpRe)) {
        result.conditions.push({
            parameter: DIMENSION_WORDS[m[1]],
            operator: LESS_WORDS.has(m[1]) ? "<" : ">",
            value: parseFloat(m[2]),
            unit: normalizeUnit(m[3]),
        });
    }

    // "width over/above/under/below/at least/at most/= 900mm",
    // "area > 20 m2"
    const nounRe = new RegExp(
        `(${Object.keys(DIMENSION_NOUNS).join("|")})\\s*(?:is\\s+)?(over|above|greater than|more than|under|below|less than|at least|at most|>=|<=|=|>|<)\\s*(\\d+(?:\\.\\d+)?)\\s*(${UNIT_PATTERN})?\\b`,
        "g"
    );
    const OP_WORDS: Record<string, string> = {
        over: ">", above: ">", "greater than": ">", "more than": ">",
        under: "<", below: "<", "less than": "<",
        "at least": ">=", "at most": "<=",
    };
    for (const m of text.matchAll(nounRe)) {
        result.conditions.push({
            parameter: DIMENSION_NOUNS[m[1]],
            operator: OP_WORDS[m[2]] ?? m[2],
            value: parseFloat(m[3]),
            unit: normalizeUnit(m[4]),
        });
    }

    return result;
}

/** Human-readable one-liner of what a parsed/structured filter does. */
export function describeFilter(
    category: string,
    conditions: FilterCondition[],
    level?: string
): string {
    const parts = conditions.map(
        (c) => `${c.parameter} ${c.operator} ${c.value}${c.unit ?? ""}`
    );
    if (level) parts.push(`on ${level}`);
    return parts.length > 0 ? `${category} where ${parts.join(" and ")}` : `all ${category}`;
}
