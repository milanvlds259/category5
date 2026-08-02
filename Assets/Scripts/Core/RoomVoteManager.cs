using System;
using Unity.Netcode;
using UnityEngine;
using Category5.Map;

namespace Category5.Core
{
    // handles vote networking for next room selection
    // clients call CastVoteServerRpc to vote for a room
    // server tracks votes and resolves when all connected players have voted
    // disconnected players' votes are excluded from the tally
    public class RoomVoteManager : NetworkBehaviour
    {
        public static RoomVoteManager Instance { get; private set; }

        // events
        public static event Action<int> OnVoteReceived; // (roomIndex)
        public static event Action OnAllVotesIn;

        private bool IsServerAuthority => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // called by clients to cast a vote
        [Rpc(SendTo.Server)]
        public void CastVoteServerRpc(int roomIndex, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            // forward to RoomManager
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.CastVote(senderId, roomIndex);
            }

            OnVoteReceived?.Invoke(roomIndex);
        }

        // called by RoomManager when all votes are in
        public void NotifyAllVotesIn()
        {
            OnAllVotesIn?.Invoke();
        }
    }
}
