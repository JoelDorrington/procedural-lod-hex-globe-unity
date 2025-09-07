using UnityEngine;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public static class Halton
    {
        public static float Sample(int index, int b)
        {
            if (index <= 0) return 0f;
            float result = 0f;
            float f = 1f;
            int i = index;
            while (i > 0)
            {
                f /= b;
                result += (i % b) * f;
                i /= b;
            }
            return result;
        }

        public static Vector2 Sample2D(int index, int baseX = 2, int baseY = 3)
        {
            return new Vector2(Sample(index, baseX), Sample(index, baseY));
        }
    }
}
