using UnityEngine;

namespace HexGlobeProject.HexMap.Runtime
{
    public class UnitMover : MonoBehaviour
    {
        public int UnitId { get; private set; }
        public int CurrentNodeIndex { get; private set; }

        private Vector3 targetPos;
        private int targetNode;
        private float speed = 5f;
        private bool moving = false;

        public void Initialize(UnitManager mgr, int unitId, int startNode)
        {
            UnitId = unitId;
            CurrentNodeIndex = startNode;
        }

        public void MoveTo(Vector3 pos, int nodeIndex)
        {
            targetPos = pos;
            targetNode = nodeIndex;
            moving = true;
        }

        void Update()
        {
            if (!moving) return;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPos) < 0.01f)
            {
                CurrentNodeIndex = targetNode;
                moving = false;
            }
        }
    }
}
