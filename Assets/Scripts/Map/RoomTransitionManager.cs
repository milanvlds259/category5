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

            if (newRoom.CurrentState == StormRoomState.Visible)
            {
                newRoom.SetActive();
            }

            // reveal adjacent rooms
            RevealAdjacentRooms(newRoom);

            OnRoomEntered?.Invoke(newRoom);
        }

        // ====================================
        // transition validation
        // =====================================

        /// <summary>
        /// checks if a transition to the target room is allowed
        /// rules:
        ///   - same ring: allowed if target is Visible or Cleared
        ///   - inward: allowed if target is Visible (one-way, can't go back out)
        /// </summary>
        public bool CanTransitionTo(StormRoom currentRoom, StormRoom targetRoom)
        {
            if (currentRoom == null || targetRoom == null) return false;

            // same ring — always allowed if target is accessible
            if (currentRoom.EyewallIndex == targetRoom.EyewallIndex)
            {
                return targetRoom.CurrentState == StormRoomState.Visible ||
                       targetRoom.CurrentState == StormRoomState.Cleared;
            }

            // inward transition — only if target is Visible
            if (targetRoom.EyewallIndex < currentRoom.EyewallIndex)
            {
                return targetRoom.CurrentState == StormRoomState.Visible;
            }

            // outward transition — blocked (ring-only backtracking
            return false;
        }

        // =====================================
        // room revelation
        // =====================================

        /// <summary>
        /// reveals adjacent rooms (left, right) and inward path if this room has one
        /// called when a room is cleared or activated
        /// </summary>
        private void RevealAdjacentRooms(StormRoom room)
        {
            if (!IsServerAuthority) return;

            var layout = GameFlowManager.Instance?.CurrentLayout;
            if (layout == null) return;

            var roomData = layout.GetRoom(room.RoomIndex);

            // reveal left neighbor
            if (roomData.leftRoomIndex >= 0)
            {
                var leftRoom = GetRoomByIndex(roomData.leftRoomIndex);
                if (leftRoom != null) leftRoom.SetVisible();
            }

            // reveal right neighbor
            if (roomData.rightRoomIndex >= 0)
            {
                var rightRoom = GetRoomByIndex(roomData.rightRoomIndex);
                if (rightRoom != null) rightRoom.SetVisible();
            }

            // reveal inward path if this room has one
            if (roomData.inwardRoomIndex >= 0)
            {
                var inwardRoom = GetRoomByIndex(roomData.inwardRoomIndex);
                if (inwardRoom != null) inwardRoom.SetVisible();
            }
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
