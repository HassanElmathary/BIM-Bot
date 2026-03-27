/**
 * MeshParser — Decodes compact MeshJSON from the SQLite Geometry table
 * into Three.js BufferGeometry objects.
 *
 * MeshJSON format: {"v":[x1,y1,z1,...], "f":[i1,i2,i3,...]}
 */

import * as THREE from "three";

export interface MeshJSON {
    v: number[];   // flat vertex array [x,y,z, x,y,z, ...]
    f: number[];   // flat face index array [i,i,i, i,i,i, ...]
}

/**
 * Parse a MeshJSON string into a Three.js BufferGeometry.
 * Returns null if the mesh data is invalid or empty.
 */
export function parseMeshJSON(jsonStr: string): THREE.BufferGeometry | null {
    try {
        const data: MeshJSON = JSON.parse(jsonStr);

        if (!data.v || !data.f || data.v.length < 9 || data.f.length < 3) {
            return null; // Need at least 1 triangle (3 vertices × 3 components)
        }

        const geometry = new THREE.BufferGeometry();

        // Set vertex positions
        const positions = new Float32Array(data.v);
        geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));

        // Set face indices
        const indices = new Uint32Array(data.f);
        geometry.setIndex(new THREE.BufferAttribute(indices, 1));

        // Compute normals for proper lighting
        geometry.computeVertexNormals();
        geometry.computeBoundingBox();

        return geometry;
    } catch {
        return null;
    }
}

/**
 * Compute the bounding box center and size of a set of geometries.
 * Used to center the camera on the model.
 */
export function computeSceneBounds(meshes: THREE.Mesh[]): {
    center: THREE.Vector3;
    size: THREE.Vector3;
    radius: number;
} {
    const box = new THREE.Box3();

    for (const mesh of meshes) {
        mesh.geometry.computeBoundingBox();
        const meshBox = mesh.geometry.boundingBox!.clone();
        meshBox.applyMatrix4(mesh.matrixWorld);
        box.union(meshBox);
    }

    const center = new THREE.Vector3();
    const size = new THREE.Vector3();
    box.getCenter(center);
    box.getSize(size);

    const radius = size.length() / 2;

    return { center, size, radius };
}
