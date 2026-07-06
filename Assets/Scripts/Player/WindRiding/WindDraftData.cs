using UnityEngine;

namespace Category5.Player.WindRiding
{
    // data-driven tuning for a wind draft zone
    // create via Create -> Category5 -> Wind Draft Data
    // place instances in Assets/Data/WindDrafts/
    [CreateAssetMenu(fileName = "New Wind Draft", menuName = "Category5/Wind Draft Data")]
    public class WindDraftData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("display name shown in the editor dashboard")]
        public string draftName = "Wind Draft";

        [TextArea(2, 4)]
        public string description;

        [Header("Force")]
        [Tooltip("acceleration applied along the draft's forward axis while gliding (m/s^2)")]
        public float pushAcceleration = 12f;

        [Tooltip("hard cap on the glider's horizontal speed while inside this draft (m/s)")]
        public float maxDraftSpeed = 45f;

        [Tooltip("upward acceleration applied to a grounded/near-ground player to launch them (m/s^2)")]
        public float groundLaunchUpForce = 18f;

        [Tooltip("height above ground at which the forced launch ends and normal gliding takes over (m)")]
        public float launchClearThreshold = 1.5f;

        [Tooltip("fraction of the cylinder length (each end) over which strength eases 0 -> 1 -> 0 (0 = no falloff, 0.5 max)")]
        [Range(0f, 0.5f)]
        public float endFalloffRatio = 0.2f;

        [Tooltip("if true, falloff is inverted: full strength at the entry end, tapering at the exit end. useful for vertical updrafts where players enter from below")]
        public bool invertFalloff;

        [Header("Volume")]
        [Tooltip("radius of the cylindrical draft volume (m)")]
        public float cylinderRadius = 2.5f;

        [Tooltip("length of the cylindrical draft volume along the zone's forward axis (m)")]
        public float cylinderLength = 8f;

        [Header("Visuals & Audio")]
        [Tooltip("particle prefab spawned as a child of the zone; oriented along forward axis")]
        public GameObject vfxPrefab;

        [Tooltip("scales particle startSpeed and emission rate with pushAcceleration (1 = 1:1)")]
        public float vfxSpeedMultiplier = 1f;

        [Tooltip("ambient wind loop played while a player is inside the draft (optional)")]
        public AudioClip sfxClip;
    }
}
