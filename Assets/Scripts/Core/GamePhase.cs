namespace Category5.Core
{
    // game phase enum for tracking overall game state
    public enum GamePhase
    {
        Fighting,           // players are fighting boss or enemies
        PowerUpSelection,   // players are selecting items/power-ups (reusing name for compatibility)
        Victory,            // game won (all rounds complete)
        GameOver            // game lost (all players dead)
    }
}
