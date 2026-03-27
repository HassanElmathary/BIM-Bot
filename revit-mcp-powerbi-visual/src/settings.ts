/**
 * Visual settings (formatting pane options).
 */

import { dataViewObjectsParser } from "powerbi-visuals-utils-dataviewutils";
import DataViewObjectsParser = dataViewObjectsParser.DataViewObjectsParser;

export class VisualSettings extends DataViewObjectsParser {
    public rendering: RenderingSettings = new RenderingSettings();
    public interaction: InteractionSettings = new InteractionSettings();
}

export class RenderingSettings {
    /** Background color of the 3D viewport */
    public backgroundColor: string = "#1a1a2e";

    /** Opacity of non-highlighted (ghosted) elements (0.0 - 1.0) */
    public ghostOpacity: number = 0.08;

    /** Show wireframe overlay */
    public showWireframe: boolean = false;

    /** Enable ambient occlusion effect */
    public enableAO: boolean = true;
}

export class InteractionSettings {
    /** Enable click-to-filter on 3D elements */
    public enableSelection: boolean = true;

    /** Enable orbit controls (rotate/zoom/pan) */
    public enableOrbit: boolean = true;

    /** Auto-rotate the model when idle */
    public autoRotate: boolean = false;

    /** Auto-rotate speed (degrees per frame) */
    public autoRotateSpeed: number = 0.5;
}
