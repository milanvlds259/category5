namespace Category5.Core
{
    // game phase enum for tracking overall game state
    public enum GamePhase
    {
        Fighting,           // players are fighting enemies in a room or fighting the boss
        RoomTransition,     // players are moving between rooms via wind tunnels
        PowerUpSelection,   // players are selecting items/power-ups after boss death
        Victory,            // game won (boss in the eye defeated)
        GameOver            // game lost (all players dead)
    }
}
