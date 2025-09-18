using UnityEngine;
using HexGlobeProject.TerrainSystem.LOD;

namespace HexGlobeProject.Tests.PlayMode
{
    /// <summary>
    /// Editor-assembly copy of PlaymodeTestSceneBuilder so Editor tests can build a minimal scene.
    /// Mirrors the PlayMode helper but lives in the Editor test assembly.
    /// </summary>
    public class PlaymodeTestSceneBuilder
    {
        public GameObject PlanetRoot { get; private set; }
        public GameObject CameraGO { get; private set; }
        public CameraController CameraController { get; private set; }
        public PlanetTileVisibilityManager Manager { get; private set; }

        public void Build()
        {
            PlanetRoot = new GameObject("PM_Planet");

            CameraGO = new GameObject("PM_Camera");
            var cam = CameraGO.AddComponent<Camera>();
            CameraController = CameraGO.AddComponent<CameraController>();
            CameraController.target = PlanetRoot.transform;
            CameraController.minDistance = 1f;
            CameraController.maxDistance = 100f;
            CameraController.distance = CameraController.maxDistance;

            var mgrGO = new GameObject("PM_Manager");
            Manager = mgrGO.AddComponent<PlanetTileVisibilityManager>();
            Manager.GetType().GetField("planetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(Manager, PlanetRoot.transform);
            Manager.GameCamera = CameraController;

            var cfg = ScriptableObject.CreateInstance<TerrainConfig>();
            cfg.baseRadius = 1f;
            cfg.baseResolution = 8;
            cfg.heightScale = 0f;
            Manager.config = cfg;
        }

        public void Teardown()
        {
            if (Manager != null)
            {
                Object.DestroyImmediate(Manager.gameObject);
                Manager = null;
            }
            if (CameraGO != null) { Object.DestroyImmediate(CameraGO); CameraGO = null; }
            if (PlanetRoot != null) { Object.DestroyImmediate(PlanetRoot); PlanetRoot = null; }
        }
    }
}
