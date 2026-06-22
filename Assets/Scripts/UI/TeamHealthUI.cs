using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;
using System.Collections.Generic;

namespace Category5.UI
{
    /// <summary>
    /// manages the team health bar ui display showing all connected players' health bars
    /// dynamically creates/removes health bar entries as players spawn/disconnect
    /// keeps entries always visible but layered behind power-up/game-over uis
    /// </summary>
    public class TeamHealthUI : MonoBehaviour
    {
        [SerializeField] private TeamHealthBarEntry teamHealthBarEntryPrefab;
        [SerializeField] private Transform entryContainer; // parent container with vertical layout group
        
        // track active entries by client id
        private Dictionary<ulong, TeamHealthBarEntry> _activeEntries = new Dictionary<ulong, TeamHealthBarEntry>();

        private void Start()
        {
            // register all existing players in the scene
            var players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                // skip the local player - their health is shown in the main hud
                if (player.IsOwner)
                    continue;
                
                RegisterPlayerEntry(player);
            }

            // subscribe to disconnect events to remove entries immediately
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.OnPlayerDisconnected += HandlePlayerDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.OnPlayerDisconnected -= HandlePlayerDisconnected;
            }
        }

        /// <summary>
        /// called when a player spawns in the game - creates a health bar entry for them
        /// this is called from PlayerController.OnNetworkSpawn()
        /// skips the local player since their health is shown in the main hud
        /// </summary>
        public void OnPlayerSpawned(PlayerController player)
        {
            // skip the local player - their health is shown in the main hud
            if (player.IsOwner)
                return;
            
            RegisterPlayerEntry(player);
        }

        private void RegisterPlayerEntry(PlayerController player)
        {
            if (player == null) return;

            // get the client id from the player's network object
            ulong clientId = player.OwnerClientId;

            // if we already have an entry for this player, skip
            if (_activeEntries.ContainsKey(clientId))
            {
                return;
            }

            // instantiate the entry prefab
            TeamHealthBarEntry entry = Instantiate(teamHealthBarEntryPrefab, entryContainer);
            entry.Initialize(player, clientId);

            // cache it
            _activeEntries[clientId] = entry;
        }

        private void HandlePlayerDisconnected(ulong clientId, string playerName)
        {
            // remove entry immediately on disconnect
            if (_activeEntries.TryGetValue(clientId, out var entry))
            {
                _activeEntries.Remove(clientId);
                Destroy(entry.gameObject);
            }
        }
    }
}
