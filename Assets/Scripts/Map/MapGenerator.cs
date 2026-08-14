using System;
using System.Collections.Generic;
using UnityEngine;
using Category5.Core;
using Unity.Netcode;

namespace Category5.Map
{
    // generates the spatial layout for a storm and delegates room instantiation to RoomManager
    // the full ring layout is computed at game start, but only the current room is instantiated
    // rooms are despawned when cleared and new rooms spawn at the old room's position
    public class MapGenerator : NetworkBehaviour
    {
        [Header("storm configuration")]
        [Tooltip("the storm to generate — set by NetworkMenu before scene load, or assign in inspector for testing")]
        [SerializeField] private StormData defaultStorm;

        // public read-only access so UI can read the blueprint from the default storm
        public StormData DefaultStorm => defaultStorm;

        [Header("ring layout")]
        [Tooltip("radius of the outermost ring — must be large enough so rooms don't overlap")]
        [SerializeField] private float outerRingRadius = 200f;

        [Tooltip("radius decrease per inner ring — must be larger than room diameter")]
        [SerializeField] private float ringRadiusStep = 60f;

        [Tooltip("minimum ring radius (innermost ring won't be smaller than this)")]
        [SerializeField] private float minRingRadius = 80f;

        // seed sync
        public NetworkVariable<int> Seed = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // map root
        private GameObject _mapParent;

        // current storm data
        private StormData _currentStorm;

        // layout built during generation
        private MapLayout _layout;

        private bool IsServerAuthority => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // =====================================
        // lifecycle
        // =====================================

        public override void OnNetworkSpawn()
        {
            Seed.OnValueChanged += OnSeedChanged;

            // start generation if we have a storm set and are the server
            if (IsServerAuthority)
            {
                // fall back to defaultStorm if SetStorm wasn't called
                if (_currentStorm == null)
                {
                    _currentStorm = defaultStorm;
                }

                if (_currentStorm != null)
                {
                    StartGeneration();
                }
                else
                {
                    Debug.LogError("[MapGenerator] no storm data assigned — cannot generate map");
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            Seed.OnValueChanged -= OnSeedChanged;
        }

        private void OnSeedChanged(int previousValue, int newValue)
        {
            if (!IsServerAuthority && newValue != 0)
            {
                DeleteMap();
                GenerateStormMap(newValue);
            }
        }

        // =====================================
        // public API
        // =====================================

        /// <summary>
        /// sets the storm data and starts generation (called by NetworkMenu before scene load)
        /// </summary>
        public void SetStorm(StormData storm)
        {
            _currentStorm = storm;
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.SetStormData(storm);
            }
        }

        /// <summary>
        /// called by GameFlowManager after scene load to kick off generation
        /// </summary>
        public void StartGeneration()
        {
            if (!IsServerAuthority) return;
            if (_currentStorm == null)
            {
                // fall back to default storm for testing
                _currentStorm = defaultStorm;
                if (_currentStorm == null)
                {
                    Debug.LogError("[MapGenerator] no storm data assigned â€” cannot generate map");
                    return;
                }
            }

            // pick a random seed and sync it
            Seed.Value = UnityEngine.Random.Range(-99999, 99999);
            GenerateStormMap(Seed.Value);
        }

        /// <summary>
        /// legacy entry point â€” called by old code paths
        /// </summary>
        public void StartRound()
        {
            if (IsServerAuthority)
            {
                StartGeneration();
            }
        }

        // =====================================
        // map generation
        // =====================================

        private void GenerateStormMap(int seed)
        {
            UnityEngine.Random.InitState(seed);
            DeleteMap();

            _mapParent = new GameObject("StormMap");

            // delegate layout generation to MapLayoutGenerator
            // this only computes positions and connections — no rooms are instantiated
            MapLayoutGenerator layoutGen = new MapLayoutGenerator();
            layoutGen.SetEyewallCount(_currentStorm.eyewallCount);
            _layout = layoutGen.Generate(_currentStorm, seed);

            if (_layout == null)
            {
                Debug.LogError("[MapGenerator] layout generation failed");
                return;
            }

            // notify RoomManager — it will instantiate the starting room
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.SetStorm(_currentStorm);
                RoomManager.Instance.SetLayout(_layout);
                RoomManager.Instance.StartAtRoom(_layout.StartingRoomIndex);
            }

            // notify GameFlowManager — always re-send storm data in case
            // SetStorm was called before GameFlowManager existed
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.SetStormData(_currentStorm);
                GameFlowManager.Instance.SetLayout(_layout);
            }
        }

        // =====================================
        // eye room
        // =====================================

        // =====================================
        // cleanup
        // =====================================

        public void DeleteMap()
        {
            if (_mapParent != null)
            {
                DestroyImmediate(_mapParent);
            }

            _layout = null;
        }

        // =====================================
        // helpers
        // =====================================

        private void OnDrawGizmosSelected()
        {
            if (_currentStorm == null) return;

            // draw ring outlines (ring 0 = outermost = largest radius)
            for (int ring = 0; ring < _currentStorm.eyewallCount; ring++)
            {
                float radius = Mathf.Max(minRingRadius, outerRingRadius - (ring * ringRadiusStep));
                Gizmos.color = ring == 0 ? Color.yellow : Color.cyan;
                Gizmos.DrawWireCube(Vector3.up * 5f, new Vector3(radius * 2, 0.5f, radius * 2));
            }

            // draw eye
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Vector3.up * 5f, 5f);
        }
    }
}