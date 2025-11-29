using System;

namespace Category5.Audio
{
    // static event hub for game flow audio events
    // other scripts fire these events, AudioManager listens and plays sounds
    public static class GameEvents
    {
        // fired when a new round starts
        public static event Action<int> OnRoundStart;
        
        // fired when power-up selection screen appears
        public static event Action OnPowerUpSelectionStart;
        
        // fired when a player selects a power-up
        public static event Action<string> OnPowerUpSelected;
        
        // fired when players win the game
        public static event Action OnVictory;
        
        // fired when all players die (game over)
        public static event Action OnGameOver;
        
        // fired when entering main menu
        public static event Action OnMenuEnter;
        
        // =====================================
        // invoke methods - call these from gameplay scripts
        // =====================================
        
        public static void InvokeRoundStart(int roundNumber)
        {
            OnRoundStart?.Invoke(roundNumber);
        }
        
        public static void InvokePowerUpSelectionStart()
        {
            OnPowerUpSelectionStart?.Invoke();
        }
        
        public static void InvokePowerUpSelected(string powerUpName)
        {
            OnPowerUpSelected?.Invoke(powerUpName);
        }
        
        public static void InvokeVictory()
        {
            OnVictory?.Invoke();
        }
        
        public static void InvokeGameOver()
        {
            OnGameOver?.Invoke();
        }
        
        public static void InvokeMenuEnter()
        {
            OnMenuEnter?.Invoke();
        }
    }
}
