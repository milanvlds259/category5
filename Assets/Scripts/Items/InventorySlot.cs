using Unity.Collections;
using Unity.Netcode;
using System;

namespace Category5.Items
{
    // represents a single inventory slot
    // uses NetworkVariable-compatible types for syncing
    public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
    {
        // fixed string for item id (uses asset name)
        public FixedString64Bytes itemId;
        
        // slot index (0-4 for 5-slot inventory)
        public int slotIndex;
        
        // is this slot empty?
        public bool IsEmpty => itemId.Length == 0;

        // constructor
        public InventorySlot(string id, int index)
        {
            itemId = new FixedString64Bytes(id);
            slotIndex = index;
        }

        // create an empty slot
        public static InventorySlot Empty(int index)
        {
            return new InventorySlot("", index);
        }

        // network serialization
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref slotIndex);
        }

        // iequatable implementation (required for networklist)
        public bool Equals(InventorySlot other)
        {
            return itemId.Equals(other.itemId) && slotIndex == other.slotIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is InventorySlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(itemId, slotIndex);
        }
    }
}
