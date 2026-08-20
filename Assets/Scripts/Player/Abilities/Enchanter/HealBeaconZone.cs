using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Category5.Player;

namespace Category5
{
    [RequireComponent(typeof(NetworkObject))]
    public class HealBeaconZone : NetworkBehaviour
    {
        [Header("Heal Settings")]
        [SerializeField] private LayerMask playerLayers;

        [Header("vfx")]
        [Tooltip("spawned once when the beacon appears")]
        [SerializeField] private GameObject spawnVfxPrefab;
        [Tooltip("looping aura effect parented to the beacon")]
        [SerializeField] private GameObject activeVfxPrefab;
        [Tooltip("spawned once when the beacon expires")]
        [SerializeField] private GameObject expireVfxPrefab;
        [Tooltip("spawned at the heal target's position every tick")]
        [SerializeField] private GameObject healTickVfxPrefab;

        [Header("Debug")]
        [SerializeField] private bool showDebugRadius = true;
        [SerializeField] private Color debugColor = new Color(1f, 1f, 0f, 0.2f);

        private ulong _ownerClientId;
        private float _healPerTick;
        private float _tickInterval;
        private float _duration;
        private float _radius;
        private float _elapsed;
        private float _tickTimer;

        private GameObject _debugSphere;

        public static event Action<Vector3, int> OnHealTick;
        public static event Action<Vector3, float> OnBeaconSpawned;
        public static event Action<Vector3> OnBeaconExpired;

        public void Initialize(ulong ownerClientId, float healPerTick, float tickInterval, float duration, float radius)
        {
            _ownerClientId = ownerClientId;
            _healPerTick = healPerTick;
            _tickInterval = tickInterval;
            _duration = duration;
            _radius = radius;
        }

        public void NotifySpawned()
        {
            if (!IsServer) return;
            NotifyBeaconSpawnedClientRpc(transform.position, _radius);
        }

        private void Update()
        {
            if (!IsServer) return;

            _elapsed += Time.deltaTime;
            _tickTimer += Time.deltaTime;

            if (_tickTimer >= _tickInterval)
            {
                _tickTimer = 0f;
                HealAllies();
            }

            if (_elapsed >= _duration)
            {
                NotifyBeaconExpiredClientRpc(transform.position);
                NetworkObject.Despawn(true);
            }
        }

        private void OnDestroy()
        {
            if (_debugSphere != null)
            {
                Destroy(_debugSphere);
            }
        }

        private void HealAllies()
        {
            Collider[] hits = playerLayers.value != 0
                ? Physics.OverlapSphere(transform.position, _radius, playerLayers)
                : Physics.OverlapSphere(transform.position, _radius);

            var healedTargets = new HashSet<int>();
            int healAmount = Mathf.RoundToInt(_healPerTick);
            int healedCount = 0;

            foreach (Collider collider in hits)
            {
                PlayerController player = collider.GetComponentInParent<PlayerController>();
                if (player == null) continue;
                if (player.IsDead.Value) continue;

                int id = player.GetInstanceID();
                if (!healedTargets.Add(id)) continue;

                player.Heal(healAmount);
                healedCount++;
            }

            if (healedCount > 0)
            {
                NotifyHealTickClientRpc(transform.position, healAmount);
            }
        }

        [ClientRpc]
        private void NotifyHealTickClientRpc(Vector3 position, int healAmount)
        {
            OnHealTick?.Invoke(position, healAmount);

            if (healTickVfxPrefab != null)
                Instantiate(healTickVfxPrefab, position, Quaternion.identity);
        }

        [ClientRpc]
        private void NotifyBeaconSpawnedClientRpc(Vector3 position, float radius)
        {
            OnBeaconSpawned?.Invoke(position, radius);
            CreateDebugSphere(radius);

            if (spawnVfxPrefab != null)
                Instantiate(spawnVfxPrefab, position, Quaternion.identity);

            if (activeVfxPrefab != null)
            {
                GameObject activeVfx = Instantiate(activeVfxPrefab, transform);
                activeVfx.transform.localPosition = Vector3.zero;
            }
        }

        [ClientRpc]
        private void NotifyBeaconExpiredClientRpc(Vector3 position)
        {
            OnBeaconExpired?.Invoke(position);
            if (_debugSphere != null)
            {
                Destroy(_debugSphere);
            }

            if (expireVfxPrefab != null)
                Instantiate(expireVfxPrefab, position, Quaternion.identity);
        }


        // THIS IS JUST A DEBUG VISUALIZATION HELPER I WILL DELETE IT WHEN WE HAVE ACTUAL ANIMATION STUFFS
        private void CreateDebugSphere(float radius)
        {
            if (!showDebugRadius) return;
            if (_debugSphere != null) return;

            _debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _debugSphere.name = "HealBeaconDebugSphere";
            _debugSphere.transform.SetParent(transform, false);
            _debugSphere.transform.localPosition = Vector3.zero;
            _debugSphere.transform.localScale = Vector3.one * radius * 2f;

            Collider col = _debugSphere.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            Renderer renderer = _debugSphere.GetComponent<Renderer>();
            if (renderer == null) return;

            Material mat = CreateDebugMaterial();
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", debugColor);
                }
                if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", debugColor);
                }

                renderer.material = mat;
            }
        }

        // creates a transparent material for debug visuals
        private Material CreateDebugMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[HealBeaconZone] no suitable unlit shader found for debug sphere");
                return null;
            }

            Material mat = new Material(shader);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = 3000;
            }
            else if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }
    }
}
