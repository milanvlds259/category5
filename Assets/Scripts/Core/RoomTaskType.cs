namespace Category5.Core
{
    // defines what kind of task a storm room requires
    // each room prefab declares its task type
    public enum RoomTaskType
    {
        EnemyWave,           // primary: clear all enemies to complete
        EliteEncounter,      // stronger single enemy (future)
        DefendPoint,         // future
        CollectItems,        // future
        EventRoom            // special/narrative room (future)
    }
}
