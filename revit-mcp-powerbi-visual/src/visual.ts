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
            this.animationId = requestAnimationFrame(animate);
            this.updateCameraFromSpherical();
            this.renderer.render(this.scene, this.camera);
        };
        animate();
    }

    // ═══════════════════════════════════════════
    //  Manual Orbit Controls
    // ═══════════════════════════════════════════

    private initOrbitControls(): void {
        const canvas = this.renderer.domElement;

        canvas.addEventListener("mousedown", (e: MouseEvent) => {
            this.isMouseDown = true;
            this.mouseButton = e.button;
            this.prevMouse = { x: e.clientX, y: e.clientY };
            e.preventDefault();
        });

        canvas.addEventListener("mousemove", (e: MouseEvent) => {
            if (!this.isMouseDown) {
                // Hover tooltip
                this.handleHover(e);
                return;
            }

            const dx = e.clientX - this.prevMouse.x;
            const dy = e.clientY - this.prevMouse.y;
            this.prevMouse = { x: e.clientX, y: e.clientY };

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
        });

        canvas.addEventListener("mouseup", (e: MouseEvent) => {
            if (this.isMouseDown && this.mouseButton === 0) {
                // Check if it was a click (not a drag)
                const dx = Math.abs(e.clientX - this.prevMouse.x);
                const dy = Math.abs(e.clientY - this.prevMouse.y);
                if (dx < 3 && dy < 3) {
                    this.handleClick(e);
                }
            }
            this.isMouseDown = false;
            this.mouseButton = -1;
        });

        canvas.addEventListener("wheel", (e: WheelEvent) => {
            e.preventDefault();
            const zoomFactor = e.deltaY > 0 ? 1.1 : 0.9;
            this.spherical.radius *= zoomFactor;
            this.spherical.radius = Math.max(0.5, Math.min(5000, this.spherical.radius));
        }, { passive: false });

        canvas.addEventListener("contextmenu", (e) => e.preventDefault());
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
        if (!dataView?.table?.rows?.length) {
            this.showNoData(true);
            return;
        }
        this.showNoData(false);

        // Large models arrive in segments — keep fetching until Power BI
        // has delivered every row (rows accumulate in the same dataView).
        if (dataView.metadata.segment) {
            this.host.fetchMoreData(true);
        }

        // Resize renderer to fit container
        const width = options.viewport.width;
        const height = options.viewport.height;
        this.renderer.setSize(width, height);
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();

        // Extract data from DataView
        const elements = this.extractData(dataView);

        // Check if data changed → rebuild scene
        const newHash = this.computeDataHash(elements);
        if (newHash !== this.currentDataHash) {
            this.currentDataHash = newHash;
            this.rebuildScene(elements);
        }

        // Apply highlights (cross-filter from other visuals)
        this.applyHighlights(dataView, elements);

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
     * Inbound cross-filtering: When another visual highlights data,
     * ghost (fade) non-highlighted elements.
     */
    private applyHighlights(dataView: DataView, elements: ElementData[]): void {
        const table = dataView.table!;
        const hasHighlights = table.columns?.some(
            (col) => (col as any).highlights != null
        ) ?? false;

        // If no highlights, restore all meshes
        if (!hasHighlights) {
            this.restoreAllMeshes();
            return;
        }

        // Build set of highlighted element IDs
        const highlightedIds = new Set<number>();
        const colIdx = this.findElementIdColumn(table);

        if (colIdx >= 0 && table.rows) {
            for (let r = 0; r < table.rows!.length; r++) {
                const elementId = Number(table.rows![r][colIdx]);
                if (elementId) highlightedIds.add(elementId);
            }
        }

        // If we couldn't determine highlights specifically, try basic approach
        if (highlightedIds.size === 0) {
            // Fallback: highlight based on selection manager
            this.restoreAllMeshes();
            return;
        }

        const ghostOpacity = this.settings?.rendering?.ghostOpacity ?? 0.08;

        for (const mesh of this.meshes.values()) {
            const mat = mesh.material as THREE.MeshPhongMaterial;
            const isHighlighted = highlightedIds.has(mesh.userData.elementId);

            if (isHighlighted) {
                mat.color.copy(mesh.userData.originalColor);
                mat.opacity = 1.0;
                mat.transparent = false;
                mat.depthWrite = true;
            } else {
                mat.opacity = ghostOpacity;
                mat.transparent = true;
                mat.depthWrite = false;
            }
        }
    }

    private restoreAllMeshes(): void {
        for (const mesh of this.meshes.values()) {
            const mat = mesh.material as THREE.MeshPhongMaterial;
            mat.color.copy(mesh.userData.originalColor);
            mat.opacity = 1.0;
            mat.transparent = false;
            mat.depthWrite = true;
        }
    }

    private findElementIdColumn(table: powerbi.DataViewTable): number {
        for (let i = 0; i < table.columns.length; i++) {
            if (table.columns[i].roles?.["elementId"]) return i;
        }
        return -1;
    }

    /**
     * Outbound cross-filtering: User clicks a 3D element →
     * tell Power BI to filter other visuals.
     */
    private handleClick(event: MouseEvent): void {
        const rect = this.renderer.domElement.getBoundingClientRect();
        this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        this.raycaster.setFromCamera(this.mouse, this.camera);
        const intersects = this.raycaster.intersectObjects(
            Array.from(this.meshes.values()),
            false
        );

        if (intersects.length > 0) {
            const mesh = intersects[0].object as RevitMesh;
            const selectionId = mesh.userData.selectionId;

            // Ctrl+click for multi-select
            this.selectionManager.select(selectionId, event.ctrlKey || event.metaKey);

            // Visual feedback — highlight selected
            this.highlightSelected(mesh);
        } else {
            // Click on empty space → clear selection
            this.selectionManager.clear();
            this.restoreAllMeshes();
        }
    }

    private highlightSelected(selectedMesh: RevitMesh): void {
        const ghostOpacity = this.settings?.rendering?.ghostOpacity ?? 0.08;

        for (const mesh of this.meshes.values()) {
            const mat = mesh.material as THREE.MeshPhongMaterial;
            if (mesh === selectedMesh) {
                mat.color.set(0x00aaff); // Highlight blue
                mat.opacity = 1.0;
                mat.transparent = false;
                mat.depthWrite = true;
            } else {
                mat.color.copy(mesh.userData.originalColor);
                mat.opacity = ghostOpacity;
                mat.transparent = true;
                mat.depthWrite = false;
            }
        }
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
            Array.from(this.meshes.values()),
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
        if (this.animationId !== null) {
            cancelAnimationFrame(this.animationId);
        }
        for (const mesh of this.meshes.values()) {
            mesh.geometry.dispose();
            (mesh.material as THREE.Material).dispose();
        }
        this.renderer.dispose();
    }
}
