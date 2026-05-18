using System.Collections.Generic;

namespace Category5.Items
{
    /// <summary>
    /// Tracks which clients have collected an item from a specific spawner.
    /// Plain C# class with no NetworkBehaviour dependency for full unit-testability.
    /// </summary>
    public class SpawnerCollectionTracker
    {
        private readonly HashSet<ulong> _collectedClientIds = new HashSet<ulong>();

        /// <summary>
        /// Returns true if the given client has already collected from this spawner.
        /// </summary>
        public bool HasPlayerCollected(ulong clientId)
        {
            return _collectedClientIds.Contains(clientId);
        }

        /// <summary>
        /// Attempts to mark the client as collected.
        /// Returns true if the client was newly added, false if already present.
        /// This provides an atomic check-and-set, eliminating TOCTOU races.
        /// </summary>
        public bool TryMarkCollected(ulong clientId)
        {
            return _collectedClientIds.Add(clientId);
        }

        /// <summary>
        /// Clears all collection state (e.g., on round reset).
        /// </summary>
        public void Clear()
        {
            _collectedClientIds.Clear();
        }

        /// <summary>
        /// Number of unique clients that have collected.
        /// </summary>
        public int Count => _collectedClientIds.Count;
    }
}
