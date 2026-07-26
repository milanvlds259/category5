using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;

namespace Category5.WeakPoints
{
    // all the info break effects need to know about who broke what
    public struct WeakPointBreakContext
    {
        public MonoBehaviour Host;          // the enemy or boss that owned this weak point
        public ulong AttackerClientId;      // the player who broke it
        public PlayerController AttackerPlayer;
        public PlayerStats AttackerStats;
        public WeakPoint WeakPoint;
        public Vector3 BreakPosition;

        // creates a context from the weak point and attacker id
        // resolves player references from the network manager
        public static WeakPointBreakContext Create(
            MonoBehaviour host,
            ulong attackerClientId,
            WeakPoint weakPoint,
            Vector3 breakPosition)
        {
            var ctx = new WeakPointBreakContext
            {
                Host = host,
                AttackerClientId = attackerClientId,
                WeakPoint = weakPoint,
                BreakPosition = breakPosition
            };

            // try to find the attacker's player controller and stats
            if (NetworkManager.Singleton != null
                && NetworkManager.Singleton.ConnectedClients.TryGetValue(attackerClientId, out var client))
            {
                ctx.AttackerPlayer = client.PlayerObject?.GetComponent<PlayerController>();
                ctx.AttackerStats = client.PlayerObject?.GetComponent<PlayerStats>();
            }

            return ctx;
        }
    }
}
