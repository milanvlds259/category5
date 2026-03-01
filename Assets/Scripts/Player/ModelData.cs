using UnityEngine;

namespace Category5.Player
{
    // data component attached to character model prefabs
    // provides avatar, attachment points, and sizing info to PlayerModelManager
    // when swapping custom models later, add this component and configure in inspector
    public class ModelData : MonoBehaviour
    {
        [Header("Avatar")]
        [Tooltip("humanoid avatar for this model's rig - required for animation retargeting")]
        public Avatar avatar;
        
        [Header("Animator Override")]
        [Tooltip("optional per-class animator controller override (leave null to use the shared controller)")]
        public RuntimeAnimatorController overrideController;
        
        [Header("Attachment Points")]
        [Tooltip("right hand bone for weapon attachment")]
        public Transform weaponMountR;
        
        [Tooltip("left hand bone for weapon/shield attachment")]
        public Transform weaponMountL;
        
        [Tooltip("transform where ranged projectiles spawn from (e.g. bow tip or hand)")]
        public Transform projectileSpawnPoint;
        
        [Header("Character Controller Sizing")]
        [Tooltip("character controller height for this model")]
        public float characterHeight = 2f;
        
        [Tooltip("character controller radius for this model")]
        public float characterRadius = 0.5f;
        
        [Tooltip("character controller center offset for this model")]
        public Vector3 characterCenter = Vector3.zero;
        
        [Header("Name Tag")]
        [Tooltip("height above the model root where the name tag should float")]
        public float nameTagHeight = 2.5f;
        
        [Header("Ground Check")]
        [Tooltip("ground check offset for this model's proportions")]
        public Vector3 groundCheckOffset = new Vector3(0, -0.95f, 0);
    }
}
