using System;
using UnityEngine;

namespace HexGlobeProject.UI
{
    [Serializable]
    public class DirectionalLightConfig
    {
        public string name = "Directional Light";
        public Color color = Color.white;
        public float intensity = 1f;
        public bool sunFlareEnabled = true;
        public string flareName = "sunburst";
        public float flareBrightness = 1f;
        public Vector3 rotation = new Vector3(51.459f, -30f, 0f);
        public Vector3 position = new Vector3(200f, 100f, 0f);
    }
}