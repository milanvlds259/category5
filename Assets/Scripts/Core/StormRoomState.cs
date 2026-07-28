namespace Category5.Core
{
    // tracks discovery and completion state for each room in the storm
    // server-authoritative via NetworkVariable on StormRoom
    public enum StormRoomState
    {
        Hidden,      // not yet discovered — invisible on map, can't enter
        Visible,     // discovered — shown on map, wind tunnel access unlocked
        Active,      // players are currently inside — spawner running
        Cleared      // task completed — can backtrack to, item drop spawned
    }
}
