using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Category5.WeakPoints
{
    // generic named stack system on the player
    // classes, items, and weak point break effects can all grant/consume stacks
    // stacks are identified by a string id so different systems don't interfere
    public class PlayerStackManager : NetworkBehaviour
    {
        // per-stack-type tracking
        private readonly Dictionary<string, StackData> _stacks = new Dictionary<string, StackData>();

        // events for ui and class-specific logic
        public static event Action<ulong, string, int> OnStacksChanged;

        private struct StackData
        {
            public NetworkVariable<int> Count;
            public float DecayTime;     // 0 = no decay
            public float DecayTimer;
            public int MaxStacks;

            public StackData(int maxStacks, float decayTime)
            {
                Count = new NetworkVariable<int>(0);
                MaxStacks = maxStacks;
                DecayTime = decayTime;
                DecayTimer = 0f;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        private void Update()
        {
            if (!IsServer) return;

            // tick decay timers
            var keys = new List<string>(_stacks.Keys);
            foreach (var key in keys)
            {
                var data = _stacks[key];
                if (data.DecayTime <= 0f) continue;
                if (data.Count.Value <= 0) continue;

                data.DecayTimer += Time.deltaTime;
                if (data.DecayTimer >= data.DecayTime)
                {
                    data.Count.Value = 0;
                    data.DecayTimer = 0f;
                    _stacks[key] = data; // write back since struct is value type
                    NotifyStacksChanged(key, 0);
                }
                else
                {
                    _stacks[key] = data; // write back
                }
            }
        }

        // add stacks to a named type (server-only)
        public void AddStack(string id, int amount, int maxStacks, float decayTime)
        {
            if (!IsServer) return;
            if (string.IsNullOrEmpty(id)) return;

            if (!_stacks.TryGetValue(id, out var data))
            {
                data = new StackData(maxStacks, decayTime);
            }

            int newCount = data.Count.Value + amount;
            if (data.MaxStacks > 0)
            {
                newCount = Mathf.Min(newCount, data.MaxStacks);
            }

            data.Count.Value = newCount;
            data.DecayTimer = 0f; // reset decay on gain
            _stacks[id] = data;

            NotifyStacksChanged(id, newCount);
        }

        // consume all stacks of a type, returns how many were consumed (server-only)
        public int ConsumeStacks(string id)
        {
            if (!IsServer) return 0;
            if (!_stacks.TryGetValue(id, out var data)) return 0;

            int consumed = data.Count.Value;
            data.Count.Value = 0;
            data.DecayTimer = 0f;
            _stacks[id] = data;

            if (consumed > 0)
            {
                NotifyStacksChanged(id, 0);
            }

            return consumed;
        }

        // read current stack count (works on all clients via NetworkVariable)
        public int GetStacks(string id)
        {
            if (_stacks.TryGetValue(id, out var data))
            {
                return data.Count.Value;
            }
            return 0;
        }

        // check if the player has at least N stacks
        public bool HasStacks(string id, int required)
        {
            return GetStacks(id) >= required;
        }

        // clear all stacks (e.g. on death)
        public void ClearAllStacks()
        {
            if (!IsServer) return;

            var keys = new List<string>(_stacks.Keys);
            foreach (var key in keys)
            {
                var data = _stacks[key];
                if (data.Count.Value > 0)
                {
                    data.Count.Value = 0;
                    data.DecayTimer = 0f;
                    _stacks[key] = data;
                    NotifyStacksChanged(key, 0);
                }
            }
        }

        private void NotifyStacksChanged(string id, int newCount)
        {
            OnStacksChanged?.Invoke(OwnerClientId, id, newCount);
        }
    }
}
