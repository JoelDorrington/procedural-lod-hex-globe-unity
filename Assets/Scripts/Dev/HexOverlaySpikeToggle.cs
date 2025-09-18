using UnityEngine;

// Small runtime toggler spike for the HexOverlay shader
// Usage: Attach to a GameObject and assign the target Material (the material using HexOverlaySpike.shader)
public class HexOverlaySpikeToggle : MonoBehaviour
{
    [Tooltip("Material to toggle. Use a shared material if you want a global toggle.")]
    public Material targetMaterial;

    [Tooltip("Property name used by the shader to enable/disable overlay (float: 0 or 1)")]
    public string overlayProperty = "_OverlayEnabled";

    public bool enabledOnStart = true;

    void Start()
    {
        if (targetMaterial != null)
        {
            targetMaterial.SetFloat(overlayProperty, enabledOnStart ? 1f : 0f);
        }
    }

    // Toggle via inspector button or keyboard 'H' for quick testing
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (targetMaterial != null)
        {
            float cur = targetMaterial.GetFloat(overlayProperty);
            float next = (cur < 0.5f) ? 1f : 0f;
            targetMaterial.SetFloat(overlayProperty, next);
            Debug.Log("HexOverlaySpikeToggle: overlay set to " + next);
            return;
        }

        // No target material assigned: toggle on all Planet instances' MeshRenderers
        var planets = FindObjectsByType<HexGlobeProject.HexMap.Planet>(FindObjectsSortMode.None);
        if (planets == null || planets.Length == 0)
        {
            Debug.LogWarning("HexOverlaySpikeToggle: No Planet instances found and no target material assigned.");
            return;
        }

        // Determine current state by inspecting first planet's first material that has the property
        bool anyEnabled = false;
        foreach (var p in planets)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            foreach (var mat in mr.sharedMaterials)
            {
                if (mat != null && mat.HasProperty(overlayProperty))
                {
                    anyEnabled = mat.GetFloat(overlayProperty) > 0.5f;
                    break;
                }
            }
            if (anyEnabled) break;
        }

        float newVal = anyEnabled ? 0f : 1f;
        foreach (var p in planets)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty(overlayProperty)) m.SetFloat(overlayProperty, newVal);
            }
        }

        Debug.Log("HexOverlaySpikeToggle: set overlay to " + newVal + " on " + planets.Length + " planets.");
    }
}