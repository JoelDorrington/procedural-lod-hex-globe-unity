using UnityEditor;
using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

/// <summary>
/// Editor diagnostics to print icosahedron face centroids and round-trip mapping.
/// Run via Tools/Icosphere/Print Face Mapping in the Editor to inspect current ordering.
/// </summary>
public static class IcosphereFaceDiagnostics
{
    [MenuItem("Tools/Icosphere/Print Face Mapping")]
    public static void PrintFaceMapping()
    {
        Debug.Log("=== Icosphere Face Mapping Diagnostic ===");
        int faceCount = 20; // expected
        for (int face = 0; face < faceCount; face++)
        {
            // Use the canonical center used by mapping utilities
            IcosphereMapping.GetTileBaryCenter(0, 0, 0, out float u, out float v);
            Vector3 centroidDir = IcosphereMapping.BaryToWorldDirection(face, u, v);

            // Also compute a geometric centroid as a cross-check
            // (reflects triangle vertex average direction)
            // We can't access IcosahedronVertices directly because it's internal, so rely on Bary mapping only.

            // Ask mapping to resolve this world direction back to a face index
            IcosphereMapping.WorldDirectionToTileFaceIndex(centroidDir, out int resolvedFace);

            Debug.LogFormat("Face {0}: centroidDir={1} resolvedFace={2}", face, centroidDir.ToString("F6"), resolvedFace);
        }
        Debug.Log("=== End Face Mapping Diagnostic ===");
    }
}
