namespace Category5.Core
{
    // tracks state for each room in the storm
    // server-authoritative via NetworkVariable on StormRoom
    // simplified to two states — only the current room is instantiated
    public enum StormRoomState
    {
        Active,      // players are currently inside — spawner running
        Cleared      // task completed — next room will be selected by host
    }
}
