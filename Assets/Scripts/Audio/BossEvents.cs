using UnityEngine;
using System;

namespace Category5.Audio
{
    // static event hub for boss-related audio events
    // other scripts fire these events, AudioManager listens and plays sounds
    public static class BossEvents
    {
        // fired when boss dies
        public static event Action<Vector3> OnBossDeath;
        
        // fired when boss spawns/appears
        public static event Action<Vector3> OnBossSpawn;
        
        // fired when boss takes damage
        public static event Action<Vector3, int> OnBossHurt;
        
        // =====================================
        // invoke methods - call these from gameplay scripts
        // =====================================
        
        public static void InvokeDeath(Vector3 position)
        {
            OnBossDeath?.Invoke(position);
        }
        
        public static void InvokeSpawn(Vector3 position)
        {
            OnBossSpawn?.Invoke(position);
        }
        
        public static void InvokeHurt(Vector3 position, int damage)
        {
            OnBossHurt?.Invoke(position, damage);
        }
    }
}
