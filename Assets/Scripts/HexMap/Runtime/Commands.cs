using System;

namespace HexGlobeProject.HexMap.Runtime
{
    // Simple authoritative command to move a unit
    [Serializable]
    public struct MoveUnitCommand
    {
        public int unitId;
        public int targetNode;
    }

    // Network-agnostic command sender interface. Implement networked sender to transmit commands to server.
    public interface INetworkCommandSender
    {
        void SendMoveCommand(MoveUnitCommand cmd);
    }

    // Local (host) command sender that applies commands immediately to the local UnitManager
    public class LocalCommandSender : INetworkCommandSender
    {
        private UnitManager manager;
        public LocalCommandSender(UnitManager mgr) { manager = mgr; }
        public void SendMoveCommand(MoveUnitCommand cmd) => manager.ApplyMoveCommand(cmd);
    }
}
