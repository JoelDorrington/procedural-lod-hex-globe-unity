using UnityEngine;
using HexGlobeProject.HexMap.Model;

namespace HexGlobeProject.HexMap.Runtime
{
    // Handle selection and right-click movement during playtest.
    public class UnitInputController : MonoBehaviour
    {
        public UnitManager unitManager;
        public DirectionalCellLookup directionalLookup;
        public Transform planetTransform;
        public float planetRadius = 10f;
        public PlayerController playerController; // optional
        private int selectedUnitId = -1;

        void Update()
        {
            if (IsPointerOverUI()) return;

            if (Input.GetMouseButtonDown(0))
            {
                // select unit under cursor
                if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f))
                {
                    var mover = hit.collider.gameObject.GetComponent<UnitMover>();
                    if (mover != null)
                    {
                        selectedUnitId = mover.UnitId;
                    }
                }
            }

            if (Input.GetMouseButtonDown(1) && selectedUnitId != -1)
            {
                // issue move: map hit point direction to node
                if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 1000f))
                {
                    Vector3 dir = (hit.point - (planetTransform!=null?planetTransform.position:Vector3.zero)).normalized;
                    int node = directionalLookup.Lookup(dir);
                    if (node >= 0)
                    {
                        if (playerController != null && playerController.commandSender != null)
                        {
                            var cmd = new MoveUnitCommand{ unitId = selectedUnitId, targetNode = node };
                            playerController.commandSender.SendMoveCommand(cmd);
                        }
                        else
                        {
                            unitManager.MoveUnitToNode(selectedUnitId, node);
                        }
                    }
                }
            }
        }

        // Reflection-based check to avoid compile-time dependency on UnityEngine.EventSystems
        private bool IsPointerOverUI()
        {
            // Try to get EventSystem type from common assemblies
            var eventSystemType = System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI")
                ?? System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.CoreModule")
                ?? System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine") ;
            if (eventSystemType == null) return false;
            var currentProp = eventSystemType.GetProperty("current");
            if (currentProp == null) return false;
            var current = currentProp.GetValue(null);
            if (current == null) return false;
            // Prefer IsPointerOverGameObject(int) if available
            var method = eventSystemType.GetMethod("IsPointerOverGameObject", new System.Type[]{ typeof(int) });
            try
            {
                if (method != null)
                {
                    var res = method.Invoke(current, new object[]{ 0 });
                    if (res is bool b) return b;
                }
                else
                {
                    var methodNoArgs = eventSystemType.GetMethod("IsPointerOverGameObject", new System.Type[0]);
                    if (methodNoArgs != null)
                    {
                        var res = methodNoArgs.Invoke(current, null);
                        if (res is bool b2) return b2;
                    }
                }
            }
            catch
            {
                // If reflection invocation fails, don't block input
                return false;
            }
            return false;
        }
    }
}
