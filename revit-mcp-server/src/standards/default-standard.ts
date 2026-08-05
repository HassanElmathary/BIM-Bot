/**
 * BIM-Bot default standard set for audit_model_standards.
 * Offices override this by passing `standards` inline or `standardsPath`
 * pointing at a JSON file with the same shape.
 */

export interface AuditStandards {
    name: string;
    rules: {
        naming?: Array<{
            target: "views" | "sheets" | "sheetnumbers" | "levels" | "families" | "materials" | "worksets";
            pattern?: string;
            description?: string;
            severity?: "error" | "warning";
        }>;
        requiredParameters?: Array<{
            category: string;
            parameters: string[];
            severity?: "error" | "warning";
            defaultValue?: string;
        }>;
        lineWeights?: Array<{
            category: string;
            projectionWeight?: number;
            cutWeight?: number;
            severity?: "error" | "warning";
        }>;
        views?: {
            requireViewTemplate?: boolean;
            exemptPrefixes?: string[];
            defaultTemplate?: string;
            severity?: "error" | "warning";
        };
        health?: {
            maxWarnings?: number;
            allowCadImports?: boolean;
            allowInPlaceFamilies?: boolean;
        };
        worksetAssignments?: Array<{
            category: string;
            worksetName: string;
            severity?: "error" | "warning";
            bepReference?: string;
        }>;
        phaseCompliance?: {
            requireViewPhaseFilter?: string;
            defaultElementPhase?: string;
            severity?: "error" | "warning";
            bepReference?: string;
        };
        sharedParameters?: Array<{
            paramName: string;
            guid?: string;
            boundCategories: string[];
            severity?: "error" | "warning";
            bepReference?: string;
        }>;
    };
    scoringWeights?: {
        naming?: number;
        requiredParameters?: number;
        lineWeights?: number;
        views?: number;
        health?: number;
        worksetAssignments?: number;
        phaseCompliance?: number;
        sharedParameters?: number;
    };
}

export const DEFAULT_STANDARD: AuditStandards = {
    name: "BIM-Bot Default Standard",
    rules: {
        naming: [
            // No pattern → flags leftover copies and Revit auto-names
            // ("Copy of …", "Section 1", "Drafting 3", …)
            { target: "views", severity: "warning" },
            { target: "families", severity: "warning" },
        ],
        views: {
            requireViewTemplate: true,
            exemptPrefixes: ["WIP", "EXPORT", "TEMP", "3D-NAV"],
            severity: "warning",
        },
        health: {
            maxWarnings: 100,
            allowCadImports: false,
            allowInPlaceFamilies: false,
        },
    },
};
