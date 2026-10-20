import * as path from "path";

/**
 * Pure decision logic for the Navisworks <-> Revit bridge (no I/O), so it can be unit-tested.
 * Revit Element IDs are only unique inside ONE Revit file, so every ID is matched to the Revit
 * document it came from (Navisworks source file vs Revit document title) before it is used.
 */

export interface BridgeItem {
    displayId?: string;
    displayName?: string;
    revitElementId?: string;
    revitCategory?: string;
    sourceFile?: string;
    revitSourceFile?: string;
}

export interface ClashInfo {
    id: string;
    name: string;
    status: string;
    distance: number;
}

export interface ClashDetail {
    test: string;
    clash: ClashInfo;
    item1: BridgeItem | null;
    item2: BridgeItem | null;
}

export interface RevitDoc {
    title: string;
    path: string;
    isActive: boolean;
    isLinked: boolean;
    isFamilyDocument: boolean;
}

export interface Decision {
    item: string;
    revitElementId: number | null;
    sourceFile: string;
    revitDocument: string;
    action: "select" | "skip" | "activate";
    reason: string;
}

export interface ActivationPlan {
    documentToActivate: string | null;
    decisions: Decision[];
}

/** File name without folder or .nwc/.nwd/.nwf/.rvt, lower-case. Handles both \ and / separators. */
export function modelKey(name: string): string {
    return path.win32.basename(name || "").trim().replace(/\.(nwc|nwd|nwf|rvt)$/i, "").toLowerCase();
}

/** Same rule as the plugin's SameModel: exact match, or one name is a prefix of the other. */
export function sameModel(sourceFile: string, docTitle: string): boolean {
    const a = modelKey(sourceFile);
    const b = modelKey(docTitle);
    return !!a && !!b && (a === b || a.startsWith(b) || b.startsWith(a));
}

/** Pulls numeric element ids out of whatever shape get_selected_elements returns. */
export function extractIds(resp: unknown): number[] {
    const pick = (v: unknown): unknown[] | null => (Array.isArray(v) ? v : null);
    const r = resp as Record<string, unknown> | unknown[] | null;
    const arr =
        pick(r) ??
        (r && typeof r === "object"
            ? pick((r as Record<string, unknown>).elements) ??
              pick((r as Record<string, unknown>).selected) ??
              pick((r as Record<string, unknown>).items) ??
              pick((r as Record<string, unknown>).elementIds)
            : null) ??
        [];
    return arr
        .map((e) => (typeof e === "number" ? e : Number((e as Record<string, unknown>)?.id)))
        .filter((n) => Number.isInteger(n) && n > 0);
}

/** Decide, per clash item, which Revit document its Element ID belongs to and whether to select it. */
export function decide(items: BridgeItem[], docs: RevitDoc[]): Decision[] {
    const active = docs.find((d) => d.isActive);
    return items.map((it): Decision => {
        const id = Number(it.revitElementId);
        const src = it.sourceFile ?? "";
        const label = it.displayName || it.displayId || "?";
        const rsrc = it.revitSourceFile ?? "";
        const fits = (title: string) => sameModel(src, title) || (!!rsrc && sameModel(rsrc, title));
        if (!Number.isInteger(id) || id <= 0) {
            return { item: label, revitElementId: null, sourceFile: src, revitDocument: "", action: "skip", reason: "No Revit Element ID on this item (not from a Revit NWC?)." };
        }
        if (!src) {
            return { item: label, revitElementId: id, sourceFile: "", revitDocument: active?.title ?? "", action: "select", reason: "Source file unknown — assumed to be the active Revit document." };
        }
        if (active && fits(active.title)) {
            return { item: label, revitElementId: id, sourceFile: src, revitDocument: active.title, action: "select", reason: "From the active Revit document." };
        }
        const linked = docs.find((d) => d.isLinked && fits(d.title));
        if (linked) {
            return { item: label, revitElementId: id, sourceFile: src, revitDocument: linked.title, action: "select", reason: "From a linked model. If the active model has an element with the same ID, Revit selects that one instead — check the result." };
        }
        const other = docs.find((d) => !d.isLinked && !d.isActive && !d.isFamilyDocument && fits(d.title));
        if (other) {
            return { item: label, revitElementId: id, sourceFile: src, revitDocument: other.title, action: "activate", reason: "From an open Revit document that is not active." };
        }
        return { item: label, revitElementId: id, sourceFile: src, revitDocument: "", action: "skip", reason: `Its source file '${src}' is not open in Revit (as the active model, a link, or another open document).` };
    });
}

/**
 * Turns "activate" decisions into a plan: if nothing can be selected in the active document and every
 * remaining item belongs to ONE other open document (and switching is allowed), switch to it and select
 * those items; otherwise skip them with a reason.
 */
export function planActivation(decisions: Decision[], allowActivate: boolean): ActivationPlan {
    const toActivate = [...new Set(decisions.filter((x) => x.action === "activate").map((x) => x.revitDocument))];
    const anySelect = decisions.some((x) => x.action === "select");
    if (!anySelect && toActivate.length === 1 && allowActivate) {
        return {
            documentToActivate: toActivate[0],
            decisions: decisions.map((x) => x.action === "activate"
                ? { ...x, action: "select" as const, reason: `Revit switched to '${x.revitDocument}' first.` }
                : x),
        };
    }
    return {
        documentToActivate: null,
        decisions: decisions.map((x) => x.action === "activate"
            ? { ...x, action: "skip" as const, reason: `Belongs to '${x.revitDocument}', which is not the active Revit document. Activate it and run again.` }
            : x),
    };
}

/** One CSV cell: quoted when it contains a comma, quote or line break; quotes doubled. */
export function csvCell(v: unknown): string {
    const s = String(v ?? "");
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

/** A joined Revit document (bridge slave) as far as routing needs it. */
export interface SlaveTarget {
    slot: string;
    port: number;
    documentTitle: string;
    model: string;
}

export interface SlaveDecision {
    item: string;
    revitElementId: number | null;
    sourceFile: string;
    slot: string | null;
    action: "select" | "skip";
    reason: string;
}

export interface SlaveGroup {
    slave: SlaveTarget;
    ids: number[];
}

/**
 * Which joined slave owns an item: exact model name first, then the name rule, then the item's
 * original RVT vs the slave's document title. Null when no slave owns it.
 */
export function findSlave(item: BridgeItem, slaves: SlaveTarget[]): SlaveTarget | null {
    const src = item.sourceFile ?? "";
    const rsrc = item.revitSourceFile ?? "";
    const key = modelKey(src);
    return slaves.find((s) => key !== "" && modelKey(s.model) === key)
        ?? slaves.find((s) => sameModel(src, s.model))
        ?? (rsrc ? slaves.find((s) => sameModel(rsrc, s.documentTitle)) : undefined)
        ?? null;
}

/** Routes every clash item to the slave that owns it; ids are grouped per slave (no duplicates). */
export function decideWithSlaves(items: BridgeItem[], slaves: SlaveTarget[]): { decisions: SlaveDecision[]; groups: SlaveGroup[] } {
    const decisions: SlaveDecision[] = [];
    const groups = new Map<string, SlaveGroup>();
    for (const it of items) {
        const id = Number(it.revitElementId);
        const src = it.sourceFile ?? "";
        const label = it.displayName || it.displayId || "?";
        if (!Number.isInteger(id) || id <= 0) {
            decisions.push({ item: label, revitElementId: null, sourceFile: src, slot: null, action: "skip", reason: "No Revit Element ID on this item (not from a Revit NWC?)." });
            continue;
        }
        const slave = findSlave(it, slaves);
        if (!slave) {
            decisions.push({ item: label, revitElementId: id, sourceFile: src, slot: null, action: "skip", reason: `No Revit slave has joined the bridge for '${src || "?"}'. Open that model in Revit and click BIM-Bot > Join Bridge.` });
            continue;
        }
        decisions.push({ item: label, revitElementId: id, sourceFile: src, slot: slave.slot, action: "select", reason: `Slave ${slave.slot}: '${slave.documentTitle}'.` });
        const group = groups.get(slave.slot) ?? { slave, ids: [] };
        if (!group.ids.includes(id)) group.ids.push(id);
        groups.set(slave.slot, group);
    }
    return { decisions, groups: [...groups.values()] };
}
