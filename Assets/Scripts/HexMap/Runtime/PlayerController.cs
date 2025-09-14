using UnityEngine;
using HexGlobeProject.HexMap.Model;

namespace HexGlobeProject.HexMap.Runtime
{
    // PlayerController is network-neutral: it emits commands through INetworkCommandSender.
    public class PlayerController : MonoBehaviour
    {
        public INetworkCommandSender commandSender;
        public UnitManager unitManager;
        public DirectionalCellLookup lookup;
        public Transform planetTransform;
        public float planetRadius = 10f;

        private int selectedUnitId = -1;

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f))
                {
                    var mover = hit.collider.gameObject.GetComponent<UnitMover>();
                    if (mover != null) selectedUnitId = mover.UnitId;
                }
            }

            if (Input.GetMouseButtonDown(1) && selectedUnitId != -1)
            {
                if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f))
                {
                    Vector3 dir = (hit.point - (planetTransform!=null?planetTransform.position:Vector3.zero)).normalized;
                    int node = lookup.Lookup(dir);
                    if (node >= 0)
                    {
                        var cmd = new MoveUnitCommand{ unitId = selectedUnitId, targetNode = node };
                        commandSender?.SendMoveCommand(cmd);
                    }
                }
            }
        }
    }
}
