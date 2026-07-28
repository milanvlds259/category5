using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Category5.Core;
using Category5.Map;
using Category5.Enemies;
using Category5.Boss;

namespace Category5.EditorTools
{
    // editor menu tool that auto-generates test assets for the storm dungeon crawl system
    // creates a StormData, RoomPrefabPool, and StormCategoryData with sensible defaults
    // useful for quick prototyping without hand-authoring every asset
    public class StormTestAssetCreator
    {
        private const string AssetFolder = "Assets/Data/Storms";

        [MenuItem("Category5/Create Storm Test Assets")]
        public static void CreateStormTestAssets()
        {
            // ensure target folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Storms");
            }

            // create room prefab pools
            RoomPrefabPool outerPool = CreateRoomPrefabPool("OuterRingRooms", 4);
            RoomPrefabPool innerPool = CreateRoomPrefabPool("InnerRingRooms", 3);
            RoomPrefabPool eyePool = CreateRoomPrefabPool("EyeRoom", 1);

            // create a basic boss data (uses existing TestBoss if available)
            BossData bossData = FindOrCreateBossData();

            // create storm data
            StormData storm = ScriptableObject.CreateInstance<StormData>();
            storm.stormName = "Test Storm";
            storm.eyewallCount = 3;
            storm.roomsPerEyewall = new int[] { 8, 5, 3 };
            storm.inwardPathsPerRing = new int[] { 2, 2, 1 };
            storm.enemyCountMultiplier = 1.2f;
            storm.enemyHealthMultiplier = 1.15f;
            storm.innerRingDifficultyRamp = 0.1f;
            storm.bossForEye = bossData;
            storm.outerRoomPool = outerPool;
            storm.innerRoomPools = new RoomPrefabPool[] { innerPool, innerPool };
            storm.eyeRoomPool = eyePool;

            AssetDatabase.CreateAsset(storm, $"{AssetFolder}/TestStorm.asset");

            // create storm category
            StormCategoryData category = ScriptableObject.CreateInstance<StormCategoryData>();
            category.categoryName = "Category 1";
            category.categoryNumber = 1;
            category.requiredResearchLevel = 0;
            category.availableStorms = new StormData[] { storm };

            AssetDatabase.CreateAsset(category, $"{AssetFolder}/Category1.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StormTestAssetCreator] created test assets in {AssetFolder}");
            EditorUtility.DisplayDialog(
                "Storm Test Assets Created",
                $"Created:\n" +
                $"- 3 RoomPrefabPool assets\n" +
                $"- 1 StormData asset (TestStorm)\n" +
                $"- 1 StormCategoryData asset (Category1)\n\n" +
                $"Assign TestStorm to MapGenerator.defaultStorm in the scene.\n" +
                $"Note: room prefabs are empty placeholders — replace with hand-crafted prefabs for actual gameplay.",
                "OK"
            );
        }

        // =====================================
        // room prefab pool creation
        // =====================================

        private static RoomPrefabPool CreateRoomPrefabPool(string poolName, int placeholderCount)
        {
            RoomPrefabPool pool = ScriptableObject.CreateInstance<RoomPrefabPool>();
            pool.poolName = poolName;
            pool.roomPrefabs = new GameObject[placeholderCount];

            // create placeholder prefabs (empty GameObjects with required components)
            for (int i = 0; i < placeholderCount; i++)
            {
                GameObject prefab = CreatePlaceholderRoomPrefab($"{poolName}_Room{i}");
                string prefabPath = $"{AssetFolder}/{poolName}_Room{i}.prefab";
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                pool.roomPrefabs[i] = savedPrefab;
                Object.DestroyImmediate(prefab);
            }

            AssetDatabase.CreateAsset(pool, $"{AssetFolder}/{poolName}.asset");
            return pool;
        }

        private static GameObject CreatePlaceholderRoomPrefab(string prefabName)
        {
            // create a basic room prefab with all required components
            GameObject room = GameObject.CreatePrimitive(PrimitiveType.Cube);
            room.name = prefabName;
            room.transform.localScale = new Vector3(30f, 2f, 30f);

            // add a ground plane for player movement
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(room.transform);
            ground.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            // add exit point transforms
            GameObject leftExit = new GameObject("LeftExit");
            leftExit.transform.SetParent(room.transform);
            leftExit.transform.localPosition = new Vector3(-15f, 1f, 0f);

            GameObject rightExit = new GameObject("RightExit");
            rightExit.transform.SetParent(room.transform);
            rightExit.transform.localPosition = new Vector3(15f, 1f, 0f);

            GameObject inwardExit = new GameObject("InwardExit");
            inwardExit.transform.SetParent(room.transform);
            inwardExit.transform.localPosition = new Vector3(0f, 1f, 15f);

            // add spawn points
            GameObject spawnPoints = new GameObject("SpawnPoints");
            spawnPoints.transform.SetParent(room.transform);
            for (int i = 0; i < 4; i++)
            {
                GameObject spawn = new GameObject($"Spawn{i}");
                spawn.transform.SetParent(spawnPoints.transform);
                float angle = i * 90f * Mathf.Deg2Rad;
                spawn.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 5f,
                    1f,
                    Mathf.Sin(angle) * 5f
                );
            }

            // add trigger volume for entry detection
            GameObject triggerObj = new GameObject("EntryTrigger");
            triggerObj.transform.SetParent(room.transform);
            triggerObj.transform.localPosition = Vector3.zero;
            SphereCollider triggerCollider = triggerObj.AddComponent<SphereCollider>();
            triggerCollider.radius = 15f;
            triggerCollider.isTrigger = true;
            TriggerVolume triggerVolume = triggerObj.AddComponent<TriggerVolume>();
            triggerVolume.targetLayers = LayerMask.GetMask("Player");
            triggerVolume.targetTag = "Player";

            // add enemy spawner
            GameObject spawnerObj = new GameObject("EnemySpawner");
            spawnerObj.transform.SetParent(room.transform);
            spawnerObj.transform.localPosition = new Vector3(0f, 1f, 0f);
            EnemySpawner spawner = spawnerObj.AddComponent<EnemySpawner>();
            spawner.autoStartOnSpawn = false;
            spawner.startOnTrigger = true;
            spawner.triggerVolume = triggerVolume;
            spawner.spawnBounds = new Vector3(25f, 0f, 25f);

            // set private serialized fields via SerializedObject
            SerializedObject spawnerSO = new SerializedObject(spawner);
            spawnerSO.FindProperty("enemiesPerWave").intValue = 3;
            spawnerSO.FindProperty("totalWaves").intValue = 2;
            spawnerSO.FindProperty("spawnInterval").floatValue = 1f;
            spawnerSO.FindProperty("waveCooldown").floatValue = 3f;
            spawnerSO.ApplyModifiedPropertiesWithoutUndo();

            // add network object (required for networked spawner)
            spawnerObj.AddComponent<NetworkObject>();

            // add storm room component
            StormRoom stormRoom = room.AddComponent<StormRoom>();
            // wire up references via SerializedObject so private fields get set
            SerializedObject so = new SerializedObject(stormRoom);
            so.FindProperty("leftExitPoint").objectReferenceValue = leftExit.transform;
            so.FindProperty("rightExitPoint").objectReferenceValue = rightExit.transform;
            so.FindProperty("inwardExitPoint").objectReferenceValue = inwardExit.transform;
            so.FindProperty("entryTrigger").objectReferenceValue = triggerVolume;
            so.FindProperty("roomSpawner").objectReferenceValue = spawner;
            so.ApplyModifiedPropertiesWithoutUndo();

            return room;
        }

        // =====================================
        // boss data
        // =====================================

        private static BossData FindOrCreateBossData()
        {
            // try to find an existing boss data asset
            string[] guids = AssetDatabase.FindAssets("t:BossData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<BossData>(path);
            }

            // create a placeholder boss data
            BossData boss = ScriptableObject.CreateInstance<BossData>();
            boss.bossName = "Test Boss";
            AssetDatabase.CreateAsset(boss, $"{AssetFolder}/TestBoss.asset");
            return boss;
        }
    }
}
