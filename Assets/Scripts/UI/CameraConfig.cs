using System;
using UnityEngine;

namespace HexGlobeProject.UI
{
    [Serializable]
    public class CameraConfig
    {
        public Color backgroundColor = new Color(0.066037714f, 0.066037714f, 0.066037714f, 0f);
        public string clearFlags = "SolidColor";
        public float fieldOfView = 60f;
        public float nearClip = 0.3f;
        public float farClip = 10000f;
        public Vector3 position = Vector3.zero;
    }
}