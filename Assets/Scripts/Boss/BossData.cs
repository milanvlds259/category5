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

        [Header("editor")]
        public Color gizmoColor = Color.magenta;

        // evaluates the hp scaling curve to get the health value for a given round
        public int GetHealthForRound(int roundIndex, int totalRounds)
        {
            if (totalRounds <= 1)
                return baseHealth;

            float t = Mathf.Clamp01((float)roundIndex / (totalRounds - 1));
            float multiplier = hpScalingCurve.Evaluate(t);
            return Mathf.RoundToInt(baseHealth * multiplier);
        }
    }
}
