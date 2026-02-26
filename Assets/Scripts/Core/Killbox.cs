using UnityEngine;
using Unity.Netcode;
using Category5.Player;
using Category5.Enemies;
using Category5.Boss;

namespace Category5.Core
{
    // trigger volume that kills players/enemies and teleports bosses when they fall off the map
    // place below the arena with a large box collider set to "is trigger"
    [RequireComponent(typeof(Collider))]
    public class Killbox : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            // check for player - take massive damage to trigger normal death flow
            // dead players already have charactercontroller disabled so they wont retrigger
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && !player.IsDead.Value)
            {
                Debug.Log($"killbox: player {player.OwnerClientId} fell off the map");
                player.TakeDamage(99999);
                return;
            }

            // check for enemy - kill via damage to trigger normal death + spawner notification
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy != null)
            {
                Debug.Log($"killbox: enemy fell off the map");
                enemy.TakeDamage(99999);
                return;
            }

            // check for boss - teleport back to spawn instead of killing (because thats no fun)
            var boss = other.GetComponentInParent<BossBase>();
            if (boss != null)
            {
                Debug.Log($"killbox: boss fell off the map");
                boss.TeleportToSpawn();
                return;
            }
        }
    }
}
