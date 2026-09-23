/**
 * BIM-Bot 3D Viewer — Power BI Custom Visual
 *
 * Renders Revit 3D geometry from SQLite-exported MeshJSON data using Three.js.
 * Supports bi-directional cross-filtering:
 *   - Inbound:  When Power BI highlights data (e.g., chart click), ghost non-highlighted elements
 *   - Outbound: When user clicks a 3D element, send selection to Power BI SelectionManager
 */

import powerbi from "powerbi-visuals-api";
import * as THREE from "three";
import { parseMeshJSON, computeSceneBounds } from "./meshParser";
import { VisualSettings } from "./settings";

import VisualConstructorOptions = powerbi.extensibility.visual.VisualConstructorOptions;
import VisualUpdateOptions = powerbi.extensibility.visual.VisualUpdateOptions;
import IVisual = powerbi.extensibility.visual.IVisual;
import IVisualHost = powerbi.extensibility.visual.IVisualHost;
import ISelectionId = powerbi.visuals.ISelectionId;
import ISelectionManager = powerbi.extensibility.ISelectionManager;
import DataView = powerbi.DataView;

import "./../style/visual.less";

// ── Element data extracted from each DataView row ──
interface ElementData {
    elementId: number;
    category: string;
    meshJson: string;
    colorR: number;
    colorG: number;
    colorB: number;
    selectionId: ISelectionId;
}

// ── Raw row before chunk reassembly ──
interface RawRow {
    elementId: number;
    chunkIndex: number;
    category: string;
    meshJson: string;
    colorR: number;
    colorG: number;
    colorB: number;
    selectionId: ISelectionId;
}

// ── Three.js mesh with Revit metadata ──
type RevitUserData = {
    elementId: number;
    category: string;
    selectionId: ISelectionId;
    originalColor: THREE.Color;
    originalOpacity: number;
};
interface RevitMesh extends THREE.Mesh {
    userData: RevitUserData;
}

export class Visual implements IVisual {
    // ── Power BI context ──
    private host: IVisualHost;
    private selectionManager: ISelectionManager;
    private target: HTMLElement;

    // ── Three.js core ──
    private scene: THREE.Scene;
    private camera: THREE.PerspectiveCamera;
    private renderer: THREE.WebGLRenderer;
    private animationId: number | null = null;

    // ── Orbit control state (manual implementation — no external dependency) ──
    private isMouseDown: boolean = false;
    private mouseButton: number = -1;
    private prevMouse: { x: number; y: number } = { x: 0, y: 0 };
    private spherical: { radius: number; theta: number; phi: number } = {
        radius: 10, theta: 0, phi: Math.PI / 4
    };
    private orbitTarget: THREE.Vector3 = new THREE.Vector3();

    // ── State tracking ──
    private meshes: Map<number, RevitMesh> = new Map();
    private currentDataHash: string = "";
    private settings: VisualSettings = new VisualSettings();

    // ── Cross-filter state ──
    // Table dataViewMappings carry no per-row highlight payload, so inbound
    // filtering is detected by comparing the current page's ElementId set
    // against every ElementId seen since the last full (unfiltered) load.
    private allElementIds: Set<number> = new Set();
    private currentElementIds: Set<number> = new Set();
    private currentSelections: { elementId: number; selectionId: ISelectionId }[] = [];
    private selectedElementIds: Set<number> = new Set();
    private loadingSegments: boolean = false;

    // ── Interaction state ──
    private downPos: { x: number; y: number } = { x: 0, y: 0 };
    private lastInteraction: number = Date.now();
    private disposed: boolean = false;

    // ── UI elements ──
    private container: HTMLDivElement;
    private hud: HTMLDivElement;
    private tooltip: HTMLDivElement;
    private noDataEl: HTMLDivElement;

    // ── Raycaster for click picking ──
    private raycaster: THREE.Raycaster = new THREE.Raycaster();
    private mouse: THREE.Vector2 = new THREE.Vector2();

    constructor(options: VisualConstructorOptions) {
        this.host = options.host;
        this.selectionManager = this.host.createSelectionManager();
        this.target = options.element;

        // Create container
        this.container = document.createElement("div");
        this.container.className = "revit-mcp-viewer";
        this.target.appendChild(this.container);

        // Create HUD
        this.hud = document.createElement("div");
        this.hud.className = "viewer-hud";
        this.container.appendChild(this.hud);

        // Create tooltip
        this.tooltip = document.createElement("div");
        this.tooltip.className = "viewer-tooltip";
        this.container.appendChild(this.tooltip);

        // Create no-data message
        this.noDataEl = document.createElement("div");
        this.noDataEl.className = "no-data-message";
        const iconDiv = document.createElement("div");
        iconDiv.className = "icon";
        iconDiv.textContent = "🏗️";
        this.noDataEl.appendChild(iconDiv);
        const textDiv = document.createElement("div");
        textDiv.textContent = "Drag ElementId, Category, and MeshJSON from the Geometry table to get started.";
        this.noDataEl.appendChild(textDiv);
        this.container.appendChild(this.noDataEl);

        // Initialize Three.js
        this.initThreeJS();

        // Fired when the selection changes in another visual (or is cleared).
        this.selectionManager.registerOnSelectCallback((ids) => {
            this.onExternalSelection(ids || []);
        });

        // Mouse/touch event handlers for orbit control
        this.initOrbitControls();
    }

    // ═══════════════════════════════════════════
    //  Three.js Initialization
    // ═══════════════════════════════════════════

    private initThreeJS(): void {
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color("#1a1a2e");

        // Camera
        this.camera = new THREE.PerspectiveCamera(50, 1, 0.1, 10000);
        this.camera.position.set(10, 10, 10);

        // Renderer
        this.renderer = new THREE.WebGLRenderer({
            antialias: true,
            alpha: false,
            powerPreference: "high-performance",
        });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = false;
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = 1.2;
        this.container.appendChild(this.renderer.domElement);

        // Lights
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        this.scene.add(ambientLight);

        const dirLight1 = new THREE.DirectionalLight(0xffffff, 0.8);
        dirLight1.position.set(5, 10, 7);
        this.scene.add(dirLight1);

        const dirLight2 = new THREE.DirectionalLight(0xccddff, 0.3);
        dirLight2.position.set(-5, -2, -5);
        this.scene.add(dirLight2);

        // Hemisphere light for ambient color contrast
        const hemiLight = new THREE.HemisphereLight(0xddeeff, 0x0d0d0d, 0.4);
        this.scene.add(hemiLight);

        // Start render loop
        this.startRenderLoop();
    }

    private startRenderLoop(): void {
        const animate = () => {
            if (this.disposed) return;
            this.animationId = requestAnimationFrame(animate);
            // Idle auto-rotate (pauses for 4s after any interaction)
            if (this.settings?.interaction?.autoRotate &&
                Date.now() - this.lastInteraction > 4000) {
                const speed = this.settings.interaction.autoRotateSpeed ?? 0.5;
                this.spherical.theta += (speed * Math.PI) / 360;
            }
            this.updateCameraFromSpherical();
            this.renderer.render(this.scene, this.camera);
        };
        animate();
    }

    // ═══════════════════════════════════════════
    //  Manual Orbit Controls
    // ═══════════════════════════════════════════

    // Bound event handlers (kept for destroy() cleanup)
    private onMouseDown = (e: MouseEvent): void => {
        if (!this.settings?.interaction?.enableOrbit && !this.settings?.interaction?.enableSelection) return;
        this.isMouseDown = true;
        this.mouseButton = e.button;
        this.prevMouse = { x: e.clientX, y: e.clientY };
        this.downPos = { x: e.clientX, y: e.clientY };
        this.lastInteraction = Date.now();
        e.preventDefault();
    };

    private onMouseMove = (e: MouseEvent): void => {
        if (!this.isMouseDown) {
            // Hover tooltip
            this.handleHover(e);
            return;
        }

        const dx = e.clientX - this.prevMouse.x;
        const dy = e.clientY - this.prevMouse.y;
        this.prevMouse = { x: e.clientX, y: e.clientY };
        this.lastInteraction = Date.now();

        if (!this.settings?.interaction?.enableOrbit) return;

        if (this.mouseButton === 0) {
            // Left button → orbit (rotate)
            this.spherical.theta -= dx * 0.005;
            this.spherical.phi -= dy * 0.005;
            // Clamp phi to avoid flipping
            this.spherical.phi = Math.max(0.05, Math.min(Math.PI - 0.05, this.spherical.phi));
        } else if (this.mouseButton === 2 || this.mouseButton === 1) {
            // Right/middle button → pan
            const panSpeed = this.spherical.radius * 0.002;
            const right = new THREE.Vector3();
            const up = new THREE.Vector3(0, 1, 0);
            right.crossVectors(
                this.camera.getWorldDirection(new THREE.Vector3()),
                up
            ).normalize();
            this.orbitTarget.addScaledVector(right, -dx * panSpeed);
            this.orbitTarget.y += dy * panSpeed;
        }
    };

    private onMouseUp = (e: MouseEvent): void => {
        if (this.isMouseDown && this.mouseButton === 0) {
            // Click = press and release within a few pixels (compare
            // against the mousedown position, not the last mousemove).
            const dx = Math.abs(e.clientX - this.downPos.x);
            const dy = Math.abs(e.clientY - this.downPos.y);
            if (dx < 5 && dy < 5) {
                this.handleClick(e);
            }
        }
        this.isMouseDown = false;
        this.mouseButton = -1;
    };

    private onWheel = (e: WheelEvent): void => {
        if (!this.settings?.interaction?.enableOrbit) return;
        e.preventDefault();
        this.lastInteraction = Date.now();
        const zoomFactor = e.deltaY > 0 ? 1.1 : 0.9;
        this.spherical.radius *= zoomFactor;
        this.spherical.radius = Math.max(0.5, Math.min(5000, this.spherical.radius));
    };

    private onContextMenu = (e: Event): void => {
        e.preventDefault();
    };

    private initOrbitControls(): void {
        const canvas = this.renderer.domElement;

        canvas.addEventListener("mousedown", this.onMouseDown);
        canvas.addEventListener("mousemove", this.onMouseMove);
        canvas.addEventListener("mouseup", this.onMouseUp);
        canvas.addEventListener("wheel", this.onWheel, { passive: false });
        canvas.addEventListener("contextmenu", this.onContextMenu);
    }

    private updateCameraFromSpherical(): void {
        const { radius, theta, phi } = this.spherical;
        this.camera.position.set(
            this.orbitTarget.x + radius * Math.sin(phi) * Math.cos(theta),
            this.orbitTarget.y + radius * Math.cos(phi),
            this.orbitTarget.z + radius * Math.sin(phi) * Math.sin(theta)
        );
        this.camera.lookAt(this.orbitTarget);
    }

    // ═══════════════════════════════════════════
    //  Power BI Update Cycle
    // ═══════════════════════════════════════════

    public update(options: VisualUpdateOptions): void {
        const dataView = options.dataViews?.[0];

        // Parse formatting-pane settings first (drives background, ghosting,
        // orbit/selection gating even when there is no data).
        if (dataView) {
            this.settings = VisualSettings.parse<VisualSettings>(dataView);
            this.applyBackground();
        }

        // Resize renderer to fit container
        const width = options.viewport.width;
        const height = options.viewport.height;
        this.renderer.setSize(width, height);
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();

        if (!dataView?.table?.rows?.length) {
            // Empty page: initial load with no data → show the hint.
            // Filtered-to-empty (we already have meshes) → keep the canvas
            // and ghost everything so the user sees the filter effect.
            if (this.meshes.size === 0) {
                this.showNoData(true);
            } else {
                this.showNoData(false);
                this.currentElementIds = new Set();
                this.applyCrossFilter();
                this.updateHUD([]);
            }
            return;
        }
        this.showNoData(false);

        // Large models arrive in segments — keep fetching until Power BI
        // has delivered every row (rows accumulate in the same dataView).
        this.loadingSegments = !!dataView.metadata.segment;
        if (this.loadingSegments) {
            this.host.fetchMoreData(true);
        }

        // Extract data from DataView
        const elements = this.extractData(dataView);

        // Track the current page vs everything seen (inbound filtering).
        // A filtered page is a subset of the known ids; a disjoint page
        // means a different model; overlap with new ids means more segments.
        this.currentElementIds = new Set(elements.map((e) => e.elementId));
        this.currentSelections = elements.map((e) => ({
            elementId: e.elementId,
            selectionId: e.selectionId,
        }));
        this.mergeKnownIds(this.currentElementIds);

        // Check if data changed → rebuild scene.
        const newHash = this.computeDataHash(elements);
        if (newHash !== this.currentDataHash) {
            this.currentDataHash = newHash;
            this.rebuildScene(elements);
        } else {
            // Same data, settings may have changed (e.g. wireframe toggle).
            this.applyWireframe();
        }

        // Apply inbound cross-filter / selection state from other visuals
        this.applyCrossFilter();

        // Update HUD
        this.updateHUD(elements);
    }

    // ═══════════════════════════════════════════
    //  Data Extraction
    // ═══════════════════════════════════════════

    private extractData(dataView: DataView): ElementData[] {
        const table = dataView.table!;
        const columns = table.columns;
        const rows = table.rows!;

        // Find column indices
        const colIdx: Record<string, number> = {};
        for (let i = 0; i < columns.length; i++) {
            const roles = columns[i].roles;
            if (roles) {
                if (roles["elementId"]) colIdx.elementId = i;
                if (roles["chunkIndex"]) colIdx.chunkIndex = i;
                if (roles["category"]) colIdx.category = i;
                if (roles["meshJson"]) colIdx.meshJson = i;
                if (roles["colorR"]) colIdx.colorR = i;
                if (roles["colorG"]) colIdx.colorG = i;
                if (roles["colorB"]) colIdx.colorB = i;
            }
        }

        if (colIdx.elementId === undefined || colIdx.meshJson === undefined) {
            return [];
        }

        const rawRows: RawRow[] = [];
        for (let r = 0; r < rows.length; r++) {
            const row = rows[r];
            const meshJson = String(row[colIdx.meshJson] || "");
            if (!meshJson || meshJson === "null") continue;

            const selectionId = this.host.createSelectionIdBuilder()
                .withTable(table, r)
                .createSelectionId();

            rawRows.push({
                elementId: Number(row[colIdx.elementId]) || 0,
                chunkIndex: colIdx.chunkIndex !== undefined
                    ? Number(row[colIdx.chunkIndex] ?? 0)
                    : 0,
                category: String(row[colIdx.category] || "Unknown"),
                meshJson,
                colorR: Number(row[colIdx.colorR] ?? 150),
                colorG: Number(row[colIdx.colorG] ?? 150),
                colorB: Number(row[colIdx.colorB] ?? 150),
                selectionId,
            });
        }

        return this.assembleChunks(rawRows);
    }

    /**
     * Geometry is exported as MeshJSON chunks (Power BI truncates text
     * columns at 32,766 chars). Group rows by ElementId, order by
     * ChunkIndex, and concatenate back into complete MeshJSON strings.
     */
    private assembleChunks(rawRows: RawRow[]): ElementData[] {
        const byElement = new Map<number, RawRow[]>();
        for (const row of rawRows) {
            const list = byElement.get(row.elementId);
            if (list) list.push(row);
            else byElement.set(row.elementId, [row]);
        }

        const elements: ElementData[] = [];
        for (const chunks of byElement.values()) {
            chunks.sort((a, b) => a.chunkIndex - b.chunkIndex);
            const first = chunks[0];
            elements.push({
                elementId: first.elementId,
                category: first.category,
                meshJson: chunks.map((c) => c.meshJson).join(""),
                colorR: first.colorR,
                colorG: first.colorG,
                colorB: first.colorB,
                selectionId: first.selectionId,
            });
        }

        return elements;
    }

    // ═══════════════════════════════════════════
    //  Scene Construction
    // ═══════════════════════════════════════════

    private rebuildScene(elements: ElementData[]): void {
        // Remove old meshes
        for (const mesh of this.meshes.values()) {
            this.scene.remove(mesh);
            mesh.geometry.dispose();
            (mesh.material as THREE.Material).dispose();
        }
        this.meshes.clear();

        // Build new meshes
        for (const elem of elements) {
            const geometry = parseMeshJSON(elem.meshJson);
            if (!geometry) continue;

            const color = new THREE.Color(
                elem.colorR / 255,
                elem.colorG / 255,
                elem.colorB / 255
            );

            const material = new THREE.MeshPhongMaterial({
                color,
                transparent: false,
                opacity: 1.0,
                side: THREE.DoubleSide,
                flatShading: false,
                shininess: 30,
                wireframe: !!this.settings?.rendering?.showWireframe,
            });

            const mesh = new THREE.Mesh(geometry, material);
            (mesh as any).userData = {
                elementId: elem.elementId,
                category: elem.category,
                selectionId: elem.selectionId,
                originalColor: color.clone(),
                originalOpacity: 1.0,
            };

            this.meshes.set(elem.elementId, mesh as unknown as RevitMesh);
            this.scene.add(mesh);
        }

        // Auto-fit camera to the scene
        if (this.meshes.size > 0) {
            const meshArray = Array.from(this.meshes.values());
            const bounds = computeSceneBounds(meshArray);

            this.orbitTarget.copy(bounds.center);
            this.spherical.radius = bounds.radius * 2.5;
            this.spherical.theta = Math.PI / 4;
            this.spherical.phi = Math.PI / 3;
        }
    }

    // ═══════════════════════════════════════════
    //  Cross-Filtering
    // ═══════════════════════════════════════════

    /**
     * Inbound cross-filtering for table dataViewMappings.
     *
     * Table mappings carry no per-row highlight payload (highlights only
     * exist on categorical value columns), so a filter from another visual
     * shows up as a *smaller row set*: the current page is a strict subset
     * of everything loaded so far. While segments are still streaming we
     * never ghost (the page is incomplete by definition).
     *
     * An explicit multi-visual selection (tracked via the selection
     * callback) takes precedence over the row-subset heuristic.
     */
    private applyCrossFilter(): void {
        if (this.selectedElementIds.size > 0) {
            this.ghostExcept(this.selectedElementIds);
            return;
        }

        if (this.loadingSegments) {
            this.restoreAllMeshes();
            return;
        }

        if (this.currentElementIds.size > 0 &&
            this.allElementIds.size > this.currentElementIds.size) {
            this.ghostExcept(this.currentElementIds);
            return;
        }

        this.restoreAllMeshes();
    }

    /**
     * Called by Power BI when the selection changes in another visual.
     * The callback delivers opaque selectionIds, so they are resolved back
     * to ElementIds through the current page's (selectionId, elementId)
     * pairs. Stale ids (from a previous page layout) fall back to the
     * row-subset heuristic in applyCrossFilter().
     */
    private onExternalSelection(ids: powerbi.extensibility.ISelectionId[]): void {
        const resolved = new Set<number>();
        for (const id of ids) {
            for (const entry of this.currentSelections) {
                if (this.selectionIncludes(id, entry.selectionId)) {
                    resolved.add(entry.elementId);
                    break;
                }
            }
        }
        this.selectedElementIds = resolved;
        // A cleared selection also resets the "everything seen" baseline so
        // a subsequent filter compares against the full model again.
        if (ids.length === 0 && this.currentElementIds.size > 0) {
            for (const id of this.currentElementIds) {
                this.allElementIds.add(id);
            }
        }
        this.applyCrossFilter();
    }

    private selectionIncludes(
        a: powerbi.extensibility.ISelectionId,
        b: ISelectionId
    ): boolean {
        try {
            const includes = (a as unknown as { includes?: (o: unknown) => boolean }).includes;
            if (typeof includes === "function") {
                return includes.call(a, b) || a === b;
            }
        } catch {
            // fall through to identity comparison
        }
        return a === b;
    }

    private ghostExcept(keepIds: Set<number>): void {
        const hide = !!this.settings?.rendering?.hideUnselected;
        const ghostOpacity = this.clampedGhostOpacity();
        for (const mesh of this.meshes.values()) {
            const mat = mesh.material as THREE.MeshPhongMaterial;
            if (keepIds.has(mesh.userData.elementId)) {
                mesh.visible = true;
                mat.color.copy(mesh.userData.originalColor);
                mat.opacity = 1.0;
                mat.transparent = false;
                mat.depthWrite = true;
            } else if (hide) {
                mesh.visible = false;
            } else {
                mesh.visible = true;
                mat.opacity = ghostOpacity;
                mat.transparent = true;
                mat.depthWrite = false;
            }
        }
    }

    private mergeKnownIds(current: Set<number>): void {
        if (this.allElementIds.size === 0) {
            this.allElementIds = new Set(current);
            return;
        }
        let overlap = false;
        let hasNew = false;
        for (const id of current) {
            if (this.allElementIds.has(id)) overlap = true;
            else hasNew = true;
        }
        if (!overlap && current.size > 0) {
            // Disjoint page → different model. Reset baselines.
            this.allElementIds = new Set(current);
            this.selectedElementIds = new Set();
        } else if (hasNew) {
            // More segments arrived → accumulate.
            for (const id of current) {
                this.allElementIds.add(id);
            }
        }
        // Pure subset → cross-filter; baselines stay untouched.
    }

    private clampedGhostOpacity(): number {
        const v = this.settings?.rendering?.ghostOpacity ?? 0.08;
        if (typeof v !== "number" || isNaN(v)) return 0.08;
        return Math.max(0, Math.min(1, v));
    }

    private restoreAllMeshes(): void {
        for (const mesh of this.meshes.values()) {
            const mat = mesh.material as THREE.MeshPhongMaterial;
            mesh.visible = true;
            mat.color.copy(mesh.userData.originalColor);
            mat.opacity = 1.0;
            mat.transparent = false;
            mat.depthWrite = true;
        }
    }

    // ── Settings-driven appearance ──

    private applyBackground(): void {
        const color = this.settings?.rendering?.backgroundColor || "#1a1a2e";
        this.scene.background = new THREE.Color(color);
    }

    private applyWireframe(): void {
        const wireframe = !!this.settings?.rendering?.showWireframe;
        for (const mesh of this.meshes.values()) {
            (mesh.material as THREE.MeshPhongMaterial).wireframe = wireframe;
        }
    }

    /**
     * Outbound cross-filtering: User clicks a 3D element →
     * tell Power BI to filter other visuals.
     */
    private handleClick(event: MouseEvent): void {
        if (!this.settings?.interaction?.enableSelection) return;

        const rect = this.renderer.domElement.getBoundingClientRect();
        this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        this.raycaster.setFromCamera(this.mouse, this.camera);
        const intersects = this.raycaster.intersectObjects(
            this.visibleMeshes(),
            false
        );

        if (intersects.length > 0) {
            const mesh = intersects[0].object as RevitMesh;
            const selectionId = mesh.userData.selectionId;
            const multi = event.ctrlKey || event.metaKey;

            if (multi) {
                if (this.selectedElementIds.has(mesh.userData.elementId)) {
                    this.selectedElementIds.delete(mesh.userData.elementId);
                } else {
                    this.selectedElementIds.add(mesh.userData.elementId);
                }
            } else {
                this.selectedElementIds = new Set([mesh.userData.elementId]);
            }

            this.selectionManager.select(selectionId, multi);

            // Optimistic feedback for single select; multi-select and
            // external changes are reconciled via onExternalSelection.
            if (!multi) {
                this.highlightSelected(mesh);
            } else {
                this.ghostExcept(this.selectedElementIds);
            }
        } else {
            // Click on empty space → clear selection
            this.selectedElementIds = new Set();
            this.selectionManager.clear();
            this.applyCrossFilter();
        }
    }

    private highlightSelected(selectedMesh: RevitMesh): void {
        this.ghostExcept(new Set([selectedMesh.userData.elementId]));
        const mat = selectedMesh.material as THREE.MeshPhongMaterial;
        mat.color.set(0x00aaff); // Highlight blue
    }

    /** Meshes currently shown (hidden ones never intercept clicks/hovers). */
    private visibleMeshes(): RevitMesh[] {
        return Array.from(this.meshes.values()).filter((m) => m.visible);
    }

    // ═══════════════════════════════════════════
    //  Hover Tooltip
    // ═══════════════════════════════════════════

    private handleHover(event: MouseEvent): void {
        const rect = this.renderer.domElement.getBoundingClientRect();
        this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        this.raycaster.setFromCamera(this.mouse, this.camera);
        const intersects = this.raycaster.intersectObjects(
            this.visibleMeshes(),
            false
        );

        if (intersects.length > 0) {
            const mesh = intersects[0].object as RevitMesh;
            this.tooltip.style.display = "block";
            this.tooltip.style.left = `${event.clientX - rect.left + 12}px`;
            this.tooltip.style.top = `${event.clientY - rect.top - 8}px`;
            this.tooltip.textContent =
                `${mesh.userData.category} — ID: ${mesh.userData.elementId}`;
            this.renderer.domElement.style.cursor = "pointer";
        } else {
            this.tooltip.style.display = "none";
            this.renderer.domElement.style.cursor = "grab";
        }
    }

    // ═══════════════════════════════════════════
    //  Utilities
    // ═══════════════════════════════════════════

    private showNoData(show: boolean): void {
        this.noDataEl.style.display = show ? "block" : "none";
        this.renderer.domElement.style.display = show ? "none" : "block";
        this.hud.style.display = show ? "none" : "block";
    }

    private updateHUD(elements: ElementData[]): void {
        const categories = new Set(elements.map((e) => e.category));
        let totalVerts = 0;
        let totalTris = 0;

        for (const mesh of this.meshes.values()) {
            const geo = mesh.geometry;
            totalVerts += geo.attributes.position?.count || 0;
            totalTris += (geo.index?.count || 0) / 3;
        }

        while (this.hud.firstChild) this.hud.removeChild(this.hud.firstChild);
        const stats = [
            `🧱 ${this.meshes.size} elements`,
            `🏷️ ${categories.size} categories`,
            `🔺 ${this.formatNumber(totalTris)} triangles`,
        ];
        if (this.meshes.size > 8000) {
            stats.push(`⚠️ large model — filter via slicers for smooth orbit`);
        }
        for (const text of stats) {
            const span = document.createElement("span");
            span.className = "stat";
            span.textContent = text;
            this.hud.appendChild(span);
        }
    }

    private formatNumber(n: number): string {
        if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
        if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
        return String(Math.round(n));
    }

    private computeDataHash(elements: ElementData[]): string {
        // Hash on element IDs + total mesh payload size so the scene
        // rebuilds when additional data segments (fetchMoreData) arrive.
        let hash = elements.length.toString();
        if (elements.length > 0) {
            let meshBytes = 0;
            for (const e of elements) meshBytes += e.meshJson.length;
            hash += `-${elements[0].elementId}-${elements[elements.length - 1].elementId}-${meshBytes}`;
        }
        return hash;
    }

    public destroy(): void {
        this.disposed = true;
        if (this.animationId !== null) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
        const canvas = this.renderer?.domElement;
        if (canvas) {
            canvas.removeEventListener("mousedown", this.onMouseDown);
            canvas.removeEventListener("mousemove", this.onMouseMove);
            canvas.removeEventListener("mouseup", this.onMouseUp);
            canvas.removeEventListener("wheel", this.onWheel);
            canvas.removeEventListener("contextmenu", this.onContextMenu);
        }
        for (const mesh of this.meshes.values()) {
            mesh.geometry.dispose();
            (mesh.material as THREE.Material).dispose();
        }
        this.meshes.clear();
        this.renderer?.dispose();
        if (this.container.parentElement === this.target) {
            this.target.removeChild(this.container);
        }
    }
}
