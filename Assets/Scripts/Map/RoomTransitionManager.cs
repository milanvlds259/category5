using System;
using UnityEngine;
using Unity.Netcode;
using Category5.Core;
using Category5.Player;

namespace Category5.Map
{
    // handles the logistics of moving players between storm rooms
    // validates transitions, activates new rooms, enforces backtracking rules
    public class RoomTransitionManager : NetworkBehaviour
    {
        public static RoomTransitionManager Instance { get; private set; }

        // current room tracked server-side
        private StormRoom _currentRoom;

        // events for UI and other systems
        public static event Action<StormRoom> OnRoomEntered;
        public static event Action<StormRoom> OnRoomExited;

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

        // =====================================
        // room entry (called by StormRoom when players enter)
        // =====================================

                /// <summary>
        /// called when all players have arrived in a new room
        /// server only — activates the room and locks exits
        /// </summary>
        public void OnPlayersArrived(StormRoom newRoom)
        {
            if (!IsServerAuthority) return;
            if (newRoom == null) return;

            // deactivate old room if we had one
            if (_currentRoom != null && _currentRoom != newRoom)
            {
                _currentRoom.SetCleared();
                OnRoomExited?.Invoke(_currentRoom);
            }

            // set new room as active
            _currentRoom = newRoom;

            OnRoomEntered?.Invoke(newRoom);
        }

        // ====================================
        // transition validation
        // =====================================

        /// <summary>
        /// checks if a transition to the target room is allowed
        /// in the new single-room flow, transitions are managed by RoomManager
        /// this method is kept for backward compatibility
        /// </summary>
        public bool CanTransitionTo(StormRoom currentRoom, StormRoom targetRoom)
        {
            if (currentRoom == null || targetRoom == null) return false;

            // in the new flow, any room can be transitioned to via the van vote
            // this method always returns true for simplicity
            return true;
        }

        // =====================================
        // room revelation (no-op in new flow)
        // =====================================

        /// <summary>
        /// in the new single-room flow, room revelation is handled by RoomManager
        /// this method is kept as a no-op for backward compatibility
        /// </summary>
        private void RevealAdjacentRooms(StormRoom room)
        {
            // no-op — RoomManager handles room transitions via voting
        }

        /// <summary>
        /// finds a StormRoom by its room index in the scene
        /// </summary>
        private StormRoom GetRoomByIndex(int roomIndex)
        {
            var allRooms = FindObjectsByType<StormRoom>(FindObjectsSortMode.None);
            foreach (var room in allRooms)
            {
                if (room.RoomIndex == roomIndex)
                    return room;
            }
            return null;
        }

        // =====================================
        // queries
        // =====================================

        /// <summary>
        /// returns the current room the players are in
        /// </summary>
        public StormRoom GetCurrentRoom()
        {
            return _currentRoom;
        }
    }
}
