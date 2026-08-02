using System.Collections.Generic;
using UnityEngine;

namespace Category5.Map
{
    // tracks votes for the next room selection
    // each connected player casts one vote for one of the available rooms
    // disconnected players' votes are excluded from the tally
    public class RoomVoteData
    {
        private List<int> _roomOptions;
        private Dictionary<ulong, int> _playerVotes; // clientId -> roomIndex

        public RoomVoteData(List<int> roomOptions)
        {
            _roomOptions = new List<int>(roomOptions);
            _playerVotes = new Dictionary<ulong, int>();
        }

        // records a player's vote
        public void CastVote(ulong clientId, int roomIndex)
        {
            if (!_roomOptions.Contains(roomIndex))
            {
                Debug.LogWarning($"[RoomVoteData] room {roomIndex} is not a valid vote option");
                return;
            }

            _playerVotes[clientId] = roomIndex;
        }

        // returns true if all currently connected players have voted
        public bool AllConnectedPlayersVoted(List<ulong> connectedClientIds)
        {
            foreach (ulong id in connectedClientIds)
            {
                if (!_playerVotes.ContainsKey(id))
                {
                    return false;
                }
            }
            return true;
        }

        // returns the room with the most votes
        // ties are broken randomly
        public int GetWinningRoom()
        {
            if (_roomOptions.Count == 0) return -1;
            if (_roomOptions.Count == 1) return _roomOptions[0];

            // count votes per room (only counting connected players)
            Dictionary<int, int> voteCounts = new Dictionary<int, int>();
            foreach (int room in _roomOptions)
            {
                voteCounts[room] = 0;
            }

            foreach (var vote in _playerVotes)
            {
                if (voteCounts.ContainsKey(vote.Value))
                {
                    voteCounts[vote.Value]++;
                }
            }

            // find the room(s) with the most votes
            int maxVotes = 0;
            List<int> winners = new List<int>();
            foreach (var kvp in voteCounts)
            {
                if (kvp.Value > maxVotes)
                {
                    maxVotes = kvp.Value;
                    winners.Clear();
                    winners.Add(kvp.Key);
                }
                else if (kvp.Value == maxVotes)
                {
                    winners.Add(kvp.Key);
                }
            }

            // if there's a tie, pick randomly
            if (winners.Count > 1)
            {
                return winners[Random.Range(0, winners.Count)];
            }

            return winners[0];
        }

        // returns the current vote count for a given room
        public int GetVoteCount(int roomIndex)
        {
            int count = 0;
            foreach (var vote in _playerVotes)
            {
                if (vote.Value == roomIndex) count++;
            }
            return count;
        }

        // returns the list of rooms being voted on
        public List<int> RoomOptions => _roomOptions;
    }
}
