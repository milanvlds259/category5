using UnityEngine;
using Category5.Core;

namespace Category5.Boss
{
    // defines all the universal attributes for a boss type — stats, attacks, visuals, hp scaling
    // mirrors the EnemyData pattern so each boss is fully self-contained
    [CreateAssetMenu(fileName = "NewBossData", menuName = "Category5/Boss Data")]
    public class BossData : ScriptableObject
    {
        [Header("identity")]
        public string bossName = "Boss";
        public ElementType elementType = ElementType.None;
        [TextArea(2, 4)]
        public string description;

        [Header("stats")]
        [Tooltip("base health at round 1 — scaled by hpScalingCurve for later rounds")]
        public int baseHealth = 500;
        public float moveSpeed = 3f;
        public float rotationSpeed = 5f;
        public float preferredDistance = 5f;
        public float chaseDistance = 15f;

        [Header("state timings")]
        public float idleDuration = 2f;
        public float cooldownDuration = 1f;

        [Header("movement behavior")]
        public BossMovementStyle movementStyle = BossMovementStyle.Direct;
        public bool rotatesDuringIdle = true;
        public bool rotatesDuringTelegraph = true;
        public bool rotatesDuringAttack = false;
        public bool movesDuringIdle = true;
        public bool movesDuringTelegraph = false;

        [Header("hp scaling")]
        [Tooltip("x = normalized round progress (0 = round 1, 1 = final round), y = hp multiplier")]
        public AnimationCurve hpScalingCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 4f)
        );

        [Header("player count scaling")]
        [Tooltip("hp multiplier per player count — index 0 = 1 player, index 1 = 2 players, etc. set all to 1 to disable")]
        public float[] playerCountMultipliers = { 1f, 1.5f, 2f, 2.5f };

        [Header("attacks")]
        [Tooltip("all attacks available to this boss — moved from the concrete boss script")]
        public BossAttackData[] availableAttacks;

        [Header("prefab")]
        [Tooltip("the networked boss prefab to spawn for this boss type")]
        public GameObject bossPrefab;

        [Header("visuals")]
        public Color bossColor = Color.white;
        [Tooltip("uniform scale multiplier applied on spawn")]
        public float scaleMultiplier = 1f;

        [Header("vfx")]
        public GameObject deathVfxPrefab;
        public GameObject spawnVfxPrefab;

        [Header("audio")]
        public AudioClip spawnSound;
        public AudioClip deathSound;
        public AudioClip hurtSound;

        [Header("intro")]
        [Tooltip("short punchy descriptor shown below the name e.g. HARBINGER OF STORMS")]
        public string introSubtitle = "";
        [Tooltip("optional portrait sprite shown as a semi-transparent background during the intro — leave null to hide")]
        public Sprite introPortrait;
        [Tooltip("total intro duration in seconds — also determines how long the boss stays dormant before attacking")]
        public float introDuration = 4f;

        [Header("editor")]
        public Color gizmoColor = Color.magenta;

        // evaluates the hp scaling curve to get the health value for a given round
        // optionally scales further by player count using playerCountMultipliers
        public int GetHealthForRound(int roundIndex, int totalRounds, int playerCount = 1)
        {
            if (totalRounds <= 1)
                return Mathf.RoundToInt(baseHealth * GetPlayerCountMultiplier(playerCount));

            float t = Mathf.Clamp01((float)roundIndex / (totalRounds - 1));
            float multiplier = hpScalingCurve.Evaluate(t);
            return Mathf.RoundToInt(baseHealth * multiplier * GetPlayerCountMultiplier(playerCount));
        }

        // returns the hp multiplier for the given player count
        private float GetPlayerCountMultiplier(int playerCount)
        {
            if (playerCountMultipliers == null || playerCountMultipliers.Length == 0)
            {
                Debug.LogError($"BossData '{bossName}': playerCountMultipliers array is empty — defaulting to 1x hp");
                return 1f;
            }

            int idx = Mathf.Clamp(playerCount - 1, 0, playerCountMultipliers.Length - 1);
            return playerCountMultipliers[idx];
        }
    }
}
