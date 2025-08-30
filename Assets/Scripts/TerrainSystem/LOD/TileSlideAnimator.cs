using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HexGlobeProject.TerrainSystem.LOD
{
    public class TileSlideAnimator
    {
        private readonly Transform planetSphereTransform;
        public TileSlideAnimator(Transform planetSphereTransform)
        {
            this.planetSphereTransform = planetSphereTransform;
        }

        public IEnumerator SlideTilesVerticallyCoroutine(TileId parentId, List<GameObject> childGOs, Vector3 parentOrig, List<Vector3> childOrigs, float duration, bool isMerge = false, Dictionary<TileId, GameObject> TileObjects = null)
        {
            float elapsed = 0f;
            Vector3 planetCenter = planetSphereTransform.position;
            float oceanRadius = planetSphereTransform.localScale.x * 0.5f;
            float offset = oceanRadius * 0.025f; // 2.5% of ocean sphere radius (smoother)
            float overlap = 0.2f; // 20% overlap (less abrupt)
            float incomingDuration = duration;
            float outgoingStart = duration * (1f - overlap);
            while (elapsed < duration)
            {
                float tIn = Mathf.Clamp01(elapsed / incomingDuration);
                float tOut = Mathf.Clamp01((elapsed - outgoingStart) / (duration - outgoingStart));
                float easeIn = tIn * tIn * (3f - 2f * tIn);
                float easeOut = tOut * tOut * (3f - 2f * tOut);
                if (isMerge)
                {
                    // Merge: parent is incoming (rises up), children are outgoing (sink down)
                    if (TileObjects != null && TileObjects.TryGetValue(parentId, out var parentGO) && parentGO != null)
                    {
                        Vector3 parentRadial = parentOrig.normalized;
                        Vector3 startPos = parentOrig - parentRadial * offset;
                        Vector3 endPos = parentOrig;
                        parentGO.transform.position = Vector3.Lerp(startPos, endPos, easeIn);
                    }
                    if (elapsed >= outgoingStart)
                    {
                        for (int i = 0; i < childGOs.Count; i++)
                        {
                            Vector3 childRadial = childOrigs[i].normalized;
                            Vector3 startPos = childOrigs[i];
                            Vector3 endPos = childOrigs[i] - childRadial * offset;
                            childGOs[i].transform.position = Vector3.Lerp(startPos, endPos, easeOut);
                        }
                    }
                }
                else
                {
                    // Split: parent is outgoing (sinks), children are incoming (rise up)
                    for (int i = 0; i < childGOs.Count; i++)
                    {
                        Vector3 childRadial = childOrigs[i].normalized;
                        Vector3 startPos = childOrigs[i] - childRadial * offset;
                        Vector3 endPos = childOrigs[i];
                        childGOs[i].transform.position = Vector3.Lerp(startPos, endPos, easeIn);
                    }
                    if (elapsed >= outgoingStart && TileObjects != null && TileObjects.TryGetValue(parentId, out var parentGO) && parentGO != null)
                    {
                        Vector3 parentRadial = parentOrig.normalized;
                        Vector3 startPos = parentOrig;
                        Vector3 endPos = parentOrig - parentRadial * offset;
                        parentGO.transform.position = Vector3.Lerp(startPos, endPos, easeOut);
                    }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
