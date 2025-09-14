using System.Collections.Generic;
using UnityEngine;
using HexGlobeProject.HexMap.Model;

namespace HexGlobeProject.HexMap.Runtime
{
    // Responsible for spawning unit prefabs and tracking unit GameObjects by unitId.
    public class UnitManager : MonoBehaviour
    {
        public GameObject unitPrefab;
        public Transform planetTransform;
        public float planetRadius = 10f;

        // authoritative model; prefer native for runtime but allow managed as well
        public GameModelNative modelNative;
        public GameModel modelManaged;

        // topology reference (must match model)
        public TopologyResult topology;

        private Dictionary<int, GameObject> units = new Dictionary<int, GameObject>();

        public GameObject SpawnUnitAtNode(int nodeIndex, int unitId)
        {
            if (unitPrefab == null) return null;
            if (topology == null) return null;
            var center = topology.centers[nodeIndex].normalized;
            Vector3 pos = planetTransform != null ? planetTransform.position + center * planetRadius : center * planetRadius;
            var go = Instantiate(unitPrefab, pos, Quaternion.identity);
            var mover = go.GetComponent<UnitMover>();
            if (mover == null) mover = go.AddComponent<UnitMover>();
            mover.Initialize(this, unitId, nodeIndex);

            units[unitId] = go;

            // update model
            if (modelNative != null) modelNative.TryPlaceUnit(nodeIndex, unitId);
            if (modelManaged != null) modelManaged.TryPlaceUnit(nodeIndex, unitId);

            return go;
        }

        public bool MoveUnitToNode(int unitId, int targetNode)
        {
            if (!units.TryGetValue(unitId, out var go)) return false;
            var mover = go.GetComponent<UnitMover>();
            if (mover == null) return false;
            int from = mover.CurrentNodeIndex;
            // update model first (authoritative)
            bool ok = false;
            if (modelNative != null) ok = modelNative.TryMoveUnit(from, targetNode, unitId);
            if (!ok && modelManaged != null) ok = modelManaged.TryMoveUnit(from, targetNode, unitId);
            if (!ok) return false;
            var center = topology.centers[targetNode].normalized;
            Vector3 pos = planetTransform != null ? planetTransform.position + center * planetRadius : center * planetRadius;
            mover.MoveTo(pos, targetNode);
            return true;
        }

        // Apply an authoritative move command (from server or local host) - keeps semantic separation
        public bool ApplyMoveCommand(MoveUnitCommand cmd)
        {
            return MoveUnitToNode(cmd.unitId, cmd.targetNode);
        }

        public bool TryGetUnitObject(int unitId, out GameObject go) => units.TryGetValue(unitId, out go);
    }
}
