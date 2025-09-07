using System;

namespace HexGlobeProject.Util
{
    /// <summary>
    /// Simple runtime toggles to control verbose diagnostic logging across the project.
    /// Defaults are conservative to keep CI/tests quiet. Flip to true when debugging locally.
    /// </summary>
    public static class Diagnostics
    {
        // Enable detailed builder logs (mesh sampling, verts, bounds)
        public static bool EnableBuilderDebug = false;

        // Enable per-tile diagnostics (collider vertex dumps, init summaries)
        public static bool EnableTileDiagnostics = false;
    }
}
