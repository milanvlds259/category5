using UnityEngine;
using Category5.Audio;

namespace Category5.Enemies
{
    // data asset defining a single attack an enemy can perform
    // enemies can have multiple attacks - one is selected by weight each time they attack
    [CreateAssetMenu(fileName = "New Enemy Attack", menuName = "Category5/Enemy Attack")]
    public class EnemyAttackData : ScriptableObject
    {
        [Header("identity")]
        [Tooltip("display name for debugging")]
        public string attackName = "New Attack";

        [Header("selection")]
        [Tooltip("relative probability weight - higher = more likely to be chosen")]
        [Range(1, 100)]
        public int selectionWeight = 1;

        [Header("timing")]
        [Tooltip("how long the attack state lasts in seconds")]
        [Range(0.1f, 5f)]
        public float attackDuration = 0.5f;

        [Tooltip("seconds after attack starts that damage is applied - sync this to the animation hit frame")]
        [Range(0f, 5f)]
        public float damageDelay = 0.25f;

        [Header("damage")]
        [Tooltip("damage multiplier applied on top of the enemy's base damage from EnemyData (1.0 = normal)")]
        [Range(0.1f, 10f)]
        public float damageMultiplier = 1f;

        [Tooltip("range override for this attack - set to -1 to use the enemy's default attackRange from EnemyData")]
        public float attackRangeOverride = -1f;

        [Header("vfx / sfx")]
        [Tooltip("vfx prefab spawned at impact position when this attack hits")]
        public GameObject attackVfxPrefab;

        [Tooltip("sound data played when this attack fires")]
        public SoundData attackSound;
    }
}
