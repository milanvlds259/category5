using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Category5.Player;
using Category5.Core;
using Category5.Items;
using Category5.Enemies;
using Category5.Boss;
using Unity.Netcode;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Category5.DebugTools
{
    public class DebugMenuUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Transform characterListContent;
        [SerializeField] private Transform itemListContent;
        [SerializeField] private GameObject buttonPrefab;

        [Header("Tab System")]
        [SerializeField] private Button charactersTabButton;
        [SerializeField] private Button itemsTabButton;
        [SerializeField] private Button spawnsTabButton;
        [SerializeField] private GameObject charactersTabPanel;
        [SerializeField] private GameObject itemsTabPanel;
        [SerializeField] private GameObject spawnsTabPanel;

        [Header("Spawns Tab")]
        [SerializeField] private Transform enemyListContent;
        [SerializeField] private Transform bossListContent;
        [SerializeField] private Transform debugSpawnPoint;

        public static bool IsMenuOpen { get; private set; } = false;
        public static bool IsGodModeActive { get; private set; } = false;

        private InputAction toggleAction;
        private readonly List<NetworkObject> _spawnedEntities = new List<NetworkObject>();

        private void Start()
        {
            // set up input
            toggleAction = InputSystem.actions.FindAction("Debug/ToggleMenu");

            if (menuPanel != null)
                menuPanel.SetActive(false);

            // wire up tab buttons
            if (charactersTabButton != null) charactersTabButton.onClick.AddListener(() => SwitchTab(0));
            if (itemsTabButton != null) itemsTabButton.onClick.AddListener(() => SwitchTab(1));
            if (spawnsTabButton != null) spawnsTabButton.onClick.AddListener(() => SwitchTab(2));

            PopulateCharacters();
            PopulateItems();
            PopulateEnemies();
            PopulateBosses();
            SetupActionButtons();
            SwitchTab(0);
        }

        private void SetupActionButtons()
        {
            if (menuPanel == null) return;

            var buttons = menuPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("HEAL")) btn.onClick.AddListener(HealPlayer);
                if (btn.name.Contains("MANA")) btn.onClick.AddListener(RestoreMana);
                if (btn.name.Contains("RESPAWN")) btn.onClick.AddListener(ResetPlayer);
                if (btn.name.Contains("CLEAR")) btn.onClick.AddListener(ClearInventory);
                if (btn.name.Contains("DESPAWN")) btn.onClick.AddListener(DespawnAll);
                if (btn.name.Contains("KILL")) btn.onClick.AddListener(KillAllEnemies);
                if (btn.name.Contains("GOD")) btn.onClick.AddListener(ToggleGodMode);
            }
        }

        private void Update()
        {
            if (toggleAction != null && toggleAction.WasPressedThisFrame())
            {
                ToggleMenu();
            }
        }

        public void ToggleMenu()
        {
            IsMenuOpen = !IsMenuOpen;
            if (menuPanel != null)
            {
                menuPanel.SetActive(IsMenuOpen);
                if (IsMenuOpen)
                {
                    PopulateCharacters();
                    PopulateItems();
                    PopulateEnemies();
                    PopulateBosses();
                }
            }

            // toggle cursor lock
            if (IsMenuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // ────────────────────────────────────────────────────────
        // Tab system
        // ────────────────────────────────────────────────────────

        private void SwitchTab(int index)
        {
            if (charactersTabPanel != null) charactersTabPanel.SetActive(index == 0);
            if (itemsTabPanel != null) itemsTabPanel.SetActive(index == 1);
            if (spawnsTabPanel != null) spawnsTabPanel.SetActive(index == 2);
        }

        // ────────────────────────────────────────────────────────
        // Characters tab
        // ────────────────────────────────────────────────────────

        private void PopulateCharacters()
        {
            if (characterListContent == null || ClassRegistry.Instance == null) return;

            foreach (Transform child in characterListContent) Destroy(child.gameObject);

            var classes = ClassRegistry.Instance.GetAllClasses();
            foreach (var playerClass in classes)
            {
                if (playerClass == null) continue;

                GameObject btnObj = Instantiate(buttonPrefab, characterListContent);
                var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = playerClass.className;

                var btn = btnObj.GetComponent<Button>();
                int classId = playerClass.classId;
                btn.onClick.AddListener(() => SwitchClass(classId));
            }
        }

        // ────────────────────────────────────────────────────────
        // Items tab
        // ────────────────────────────────────────────────────────

        private void PopulateItems()
        {
            if (itemListContent == null || ItemRegistry.Instance == null) return;

            foreach (Transform child in itemListContent) Destroy(child.gameObject);

            var items = ItemRegistry.Instance.AllItems;
            foreach (var item in items)
            {
                if (item == null) continue;

                GameObject btnObj = Instantiate(buttonPrefab, itemListContent);
                var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = item.ItemName;

                var btn = btnObj.GetComponent<Button>();
                string itemId = item.UniqueId;
                btn.onClick.AddListener(() => AddItem(itemId));
            }
        }

        // ────────────────────────────────────────────────────────
        // Spawns tab — enemies
        // ────────────────────────────────────────────────────────

        private void PopulateEnemies()
        {
            if (enemyListContent == null) return;

            foreach (Transform child in enemyListContent) Destroy(child.gameObject);

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:EnemyData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data == null || data.enemyPrefab == null) continue;

                GameObject btnObj = Instantiate(buttonPrefab, enemyListContent);
                var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = data.enemyName;

                var btn = btnObj.GetComponent<Button>();
                btn.onClick.AddListener(() => SpawnEnemy(data));
            }
#endif

            if (enemyListContent.childCount == 0)
            {
                GameObject msgObj = Instantiate(buttonPrefab, enemyListContent);
                var msg = msgObj.GetComponentInChildren<TextMeshProUGUI>();
                if (msg != null) msg.text = "(no EnemyData assets found)";
                var msgBtn = msgObj.GetComponent<Button>();
                if (msgBtn != null) msgBtn.interactable = false;
            }
        }

        // ────────────────────────────────────────────────────────
        // Spawns tab — bosses
        // ────────────────────────────────────────────────────────

        private void PopulateBosses()
        {
            if (bossListContent == null) return;

            foreach (Transform child in bossListContent) Destroy(child.gameObject);

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:BossData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BossData data = AssetDatabase.LoadAssetAtPath<BossData>(path);
                if (data == null || data.bossPrefab == null) continue;

                GameObject btnObj = Instantiate(buttonPrefab, bossListContent);
                var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = data.bossName;

                var btn = btnObj.GetComponent<Button>();
                btn.onClick.AddListener(() => SpawnBoss(data));
            }
#endif

            if (bossListContent.childCount == 0)
            {
                GameObject msgObj = Instantiate(buttonPrefab, bossListContent);
                var msg = msgObj.GetComponentInChildren<TextMeshProUGUI>();
                if (msg != null) msg.text = "(no BossData assets found)";
                var msgBtn = msgObj.GetComponent<Button>();
                if (msgBtn != null) msgBtn.interactable = false;
            }
        }

        // ────────────────────────────────────────────────────────
        // Spawn logic
        // ────────────────────────────────────────────────────────

        private Vector3 GetSpawnPosition()
        {
            if (debugSpawnPoint != null)
                return debugSpawnPoint.position;

            // fallback: spawn in front of local player
            var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayer != null)
                return localPlayer.transform.position + localPlayer.transform.forward * 5f;

            return Vector3.zero;
        }

        private void SpawnEnemy(EnemyData data)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[DebugMenu] Can only spawn from host/server.");
                return;
            }

            if (data.enemyPrefab == null)
            {
                Debug.LogWarning($"[DebugMenu] EnemyData '{data.enemyName}' has no prefab assigned.");
                return;
            }

            Vector3 pos = GetSpawnPosition();
            GameObject obj = Instantiate(data.enemyPrefab, pos, Quaternion.identity);
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                _spawnedEntities.Add(netObj);
                Debug.Log($"[DebugMenu] Spawned enemy: {data.enemyName}");
            }
            else
            {
                Debug.LogError($"[DebugMenu] Enemy prefab for '{data.enemyName}' is missing NetworkObject!");
                Destroy(obj);
            }
        }

        private void SpawnBoss(BossData data)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[DebugMenu] Can only spawn from host/server.");
                return;
            }

            if (data.bossPrefab == null)
            {
                Debug.LogWarning($"[DebugMenu] BossData '{data.bossName}' has no prefab assigned.");
                return;
            }

            Vector3 pos = GetSpawnPosition();
            GameObject obj = Instantiate(data.bossPrefab, pos, Quaternion.identity);
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                _spawnedEntities.Add(netObj);
                Debug.Log($"[DebugMenu] Spawned boss: {data.bossName}");
            }
            else
            {
                Debug.LogError($"[DebugMenu] Boss prefab for '{data.bossName}' is missing NetworkObject!");
                Destroy(obj);
            }
        }

        // ────────────────────────────────────────────────────────
        // Action buttons
        // ────────────────────────────────────────────────────────

        private void DespawnAll()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[DebugMenu] Can only despawn from host/server.");
                return;
            }

            int count = 0;
            for (int i = _spawnedEntities.Count - 1; i >= 0; i--)
            {
                if (_spawnedEntities[i] != null && _spawnedEntities[i].IsSpawned)
                {
                    _spawnedEntities[i].Despawn(true);
                    count++;
                }
            }
            _spawnedEntities.Clear();
            Debug.Log($"[DebugMenu] Despawned {count} debug entities.");
        }

        private void KillAllEnemies()
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[DebugMenu] Can only kill from host/server.");
                return;
            }

            int count = 0;
            // kill all tracked debug spawns
            for (int i = _spawnedEntities.Count - 1; i >= 0; i--)
            {
                if (_spawnedEntities[i] != null && _spawnedEntities[i].IsSpawned)
                {
                    var enemy = _spawnedEntities[i].GetComponent<EnemyBase>();
                    if (enemy != null && !enemy.IsDead)
                    {
                        enemy.TakeDamage(99999);
                        count++;
                    }
                    var boss = _spawnedEntities[i].GetComponent<BossBase>();
                    if (boss != null && !boss.IsDead)
                    {
                        boss.TakeDamage(99999);
                        count++;
                    }
                }
            }

            // also kill any enemies in the scene that weren't debug-spawned
            var allEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in allEnemies)
            {
                if (!enemy.IsDead)
                {
                    enemy.TakeDamage(99999);
                    count++;
                }
            }

            Debug.Log($"[DebugMenu] Killed {count} enemies/bosses.");
        }

        private void ToggleGodMode()
        {
            IsGodModeActive = !IsGodModeActive;
            Debug.Log($"[DebugMenu] God mode: {(IsGodModeActive ? "ON" : "OFF")}");
        }

        // ────────────────────────────────────────────────────────
        // Existing utility methods (unchanged)
        // ────────────────────────────────────────────────────────

        private void SwitchClass(int classId)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var classManager = localPlayer.GetComponent<PlayerClassManager>();
                if (classManager != null)
                {
                    classManager.RequestSetClassIdServerRpc(classId);
                    Debug.Log($"[DebugMenu] Requested class switch to ID: {classId}");
                }
            }
        }

        private void AddItem(string itemId)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var inventory = localPlayer.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    if (NetworkManager.Singleton.IsServer)
                    {
                        inventory.AddItem(itemId);
                        Debug.Log($"[DebugMenu] Added item: {itemId}");
                    }
                    else
                    {
                        Debug.LogWarning("[DebugMenu] Cannot add item from client. Must be Host/Server.");
                    }
                }
            }
        }

        public void HealPlayer()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var controller = localPlayer.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.Heal(9999);
                    Debug.Log("[DebugMenu] Healed player to full.");
                }
            }
        }

        public void RestoreMana()
        {
            if (!NetworkManager.Singleton.IsServer) return;
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var controller = localPlayer.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.RestoreMana(9999);
                    Debug.Log("[DebugMenu] Restored player mana to full.");
                }
            }
        }

        public void ResetPlayer()
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var controller = localPlayer.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.Respawn();
                }
            }
        }

        public void ClearInventory()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var inventory = localPlayer.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    inventory.ClearInventory();
                }
            }
        }
    }
}
