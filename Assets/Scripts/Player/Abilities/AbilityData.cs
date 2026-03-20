using UnityEngine;

namespace Category5
{
    [CreateAssetMenu(fileName = "New Ability", menuName = "Category5/Ability Data")]
    public class AbilityData : ScriptableObject
    {
        [Header("Identity")]
        public string abilityName;
        [TextArea(3, 6)]
        public string description;
        public Sprite abilityIcon;

        [Header("Gameplay")]
        public float cooldownDuration = 10f;
        
        [Tooltip("damage as a fraction of class attack damage (e.g. 2.5 = 250% of attack damage)")]
        public float damageCoefficient = 1f;
        
        public float castTime = 0f;
        public int manaCost = 0;

        [Header("Visuals & Audio")]
        public GameObject vfxPrefab;
        public AudioClip sfxClip;
    }
}
