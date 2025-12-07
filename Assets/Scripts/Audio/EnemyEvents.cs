using UnityEngine;
using System;
using Category5.Core;

namespace Category5.Audio
{
    // static event hub for enemy-related audio events
    // other scripts fire these events, AudioManager listens and plays sounds
    public static class EnemyEvents
    {
        // fired when an enemy dies
        // parameters: position, element type
        public static event Action<Vector3, ElementType> OnEnemyDeath;
        
        // fired when an enemy spawns
        // parameters: position, element type
        public static event Action<Vector3, ElementType> OnEnemySpawn;
        
        // fired when an enemy takes damage
        // parameters: position, damage amount, element type
        public static event Action<Vector3, int, ElementType> OnEnemyHurt;
        
        // fired when an enemy attacks
        // parameters: position, element type
        public static event Action<Vector3, ElementType> OnEnemyAttack;
        
        // =====================================
        // invoke methods - call these from gameplay scripts
        // =====================================
        
        public static void InvokeDeath(Vector3 position, ElementType element = ElementType.None)
        {
            OnEnemyDeath?.Invoke(position, element);
        }
        
        public static void InvokeSpawn(Vector3 position, ElementType element = ElementType.None)
        {
            OnEnemySpawn?.Invoke(position, element);
        }
        
        public static void InvokeHurt(Vector3 position, int damage, ElementType element = ElementType.None)
        {
            OnEnemyHurt?.Invoke(position, damage, element);
        }
        
        public static void InvokeAttack(Vector3 position, ElementType element = ElementType.None)
        {
            OnEnemyAttack?.Invoke(position, element);
        }
    }
}
