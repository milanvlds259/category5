using UnityEngine;
using System;

namespace Category5.Audio
{
    // static event hub for player-related audio events
    // other scripts fire these events, AudioManager listens and plays sounds
    public static class PlayerEvents
    {
        // fired when player performs a dodge roll
        public static event Action<Vector3> OnPlayerDodge;
        
        // fired when player jumps
        public static event Action<Vector3> OnPlayerJump;
        
        // fired when player lands on the ground
        public static event Action<Vector3> OnPlayerLand;
        
        // fired when player dies
        public static event Action<Vector3> OnPlayerDeath;
        
        // fired when player heals (lifesteal, etc)
        public static event Action<Vector3, int> OnPlayerHeal;
        
        // fired when player swings their weapon (start of attack animation)
        public static event Action<Vector3> OnPlayerAttackSwing;
        
        // =====================================
        // invoke methods - call these from gameplay scripts
        // =====================================
        
        public static void InvokeDodge(Vector3 position)
        {
            OnPlayerDodge?.Invoke(position);
        }
        
        public static void InvokeJump(Vector3 position)
        {
            OnPlayerJump?.Invoke(position);
        }
        
        public static void InvokeLand(Vector3 position)
        {
            OnPlayerLand?.Invoke(position);
        }
        
        public static void InvokeDeath(Vector3 position)
        {
            OnPlayerDeath?.Invoke(position);
        }
        
        public static void InvokeHeal(Vector3 position, int amount)
        {
            OnPlayerHeal?.Invoke(position, amount);
        }
        
        public static void InvokeAttackSwing(Vector3 position)
        {
            OnPlayerAttackSwing?.Invoke(position);
        }
    }
}
