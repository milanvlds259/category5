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
        public float baseDamage = 20f;
        public float castTime = 0f;
        public float manaCost = 0f; // stubbed for future mana system

        [Header("Visuals & Audio")]
        public GameObject vfxPrefab;
        public AudioClip sfxClip;
    }
}
