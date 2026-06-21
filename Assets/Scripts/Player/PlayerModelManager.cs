using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Category5.Core;
using Category5.UI;

namespace Category5.Player
{
    // manages the player's visual character model
    // handles model swapping per class, animator setup, attachment points,
    // and character controller resizing
    // runs on ALL clients so every player sees every other player's model
    public class PlayerModelManager : NetworkBehaviour
    {
        [Header("Animation")]
        [Tooltip("shared animator controller used by all classes (classes can override via ModelData)")]
        [SerializeField] private RuntimeAnimatorController sharedAnimatorController;
        
        // current model instance
        private GameObject _currentModel;
        private ModelData _currentModelData;
        private int _currentLoadedClass = PlayerClass.NoClassId;
        
        // cached references
        private PlayerClassManager _classManager;
        private PlayerController _playerController;
        private PlayerCombat _playerCombat;
        private Animator _animator;
        private OwnerPlayerNetworkAnimator _ownerNetworkAnimator;
        private NetworkAnimator _networkAnimator;
        private CharacterController _characterController;
        private Transform _modelRoot;
        
        // attachment points from current model
        public Transform WeaponMountR { get; private set; }
        public Transform WeaponMountL { get; private set; }
        public Transform ProjectileSpawnPoint { get; private set; } // place in avatar where projectiles spawn (like bow tip or hand)
        
        // animator reference for external systems (playercontroller, hitfeedbackmanager, etc)
        public Animator ModelAnimator => _animator;
        
        // event fired when a model finishes loading on any player
        public static event Action<PlayerController, Animator> OnModelLoaded;
        
        private void Awake()
        {
            _classManager = GetComponent<PlayerClassManager>();
            _playerController = GetComponent<PlayerController>();
            _playerCombat = GetComponent<PlayerCombat>();
            _characterController = GetComponent<CharacterController>();
            _ownerNetworkAnimator = GetComponent<OwnerPlayerNetworkAnimator>();
            _networkAnimator = _ownerNetworkAnimator != null ? _ownerNetworkAnimator : GetComponent<NetworkAnimator>();
            _modelRoot = transform.Find("ModelRoot");

            if (_networkAnimator == null)
            {
                Debug.LogError("PlayerModelManager: Missing NetworkAnimator on Player prefab. remote animation sync will not work.");
            }
            else if (_ownerNetworkAnimator == null)
            {
                Debug.LogError("PlayerModelManager: Player prefab must use OwnerPlayerNetworkAnimator instead of the base NetworkAnimator. remote animation sync will not work until this is fixed.");
            }

            if (_modelRoot == null)
            {
                Debug.LogWarning("PlayerModelManager: ModelRoot child not found");
                _modelRoot = transform;
            }
        }
        
        private void Start()
        {
            // PlayerClassManager now handles all lobby synchronization and networked class changes.
            // It calls LoadModel() on this component whenever the class needs to update.
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            // synchronization handled by PlayerClassManager
        }

        public override void OnNetworkDespawn()
        {
        }
        
        // loads the model prefab for the given class id
// runs on all clients independently
        public void LoadModel(int classId)
        {
            // skip if already loaded this class
            if (classId == _currentLoadedClass) return;
            _currentLoadedClass = classId;
            
            // no class selected - skip model load
            if (classId == PlayerClass.NoClassId) return;
            
            // get class data from registry
            PlayerClass classData = GetClassData(classId);
            if (classData == null)
            {
                Debug.LogWarning($"PlayerModelManager: No class data found for classId {classId}");
                return;
            }
            
            if (classData.modelPrefab == null)
            {
                Debug.LogWarning($"PlayerModelManager: No model prefab assigned for class {classData.className}");
                return;
            }

            if (_modelRoot == null)
            {
                _modelRoot = transform.Find("ModelRoot");
                if (_modelRoot == null)
                {
                    _modelRoot = transform;
                }
            }
            
            // destroy current model if one exists
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
                _currentModelData = null;
            }

            // clear any leftover children under model root (aka editor preview or previous model)
            ClearModelRootChildren();
            
            // instantiate new model as child of model root
            _currentModel = Instantiate(classData.modelPrefab, _modelRoot);
            _currentModel.name = "CharacterModel";
            _currentModel.transform.localPosition = Vector3.zero;
            _currentModel.transform.localRotation = Quaternion.identity;
            
            // match player's layer recursively
            SetLayerRecursively(_currentModel, gameObject.layer);
            
            _currentModelData = _currentModel.GetComponent<ModelData>();
            
            SetupAnimator();
            
            // cache attachment points from model
            CacheAttachmentPoints();
            
            AdjustCharacterController();
            AdjustNameTagHeight();
            
            RefreshPlayerRenderers();
            
            // fire event so other systems can react (PlayerCombat, vfx, etc)
            OnModelLoaded?.Invoke(_playerController, _animator);
            
            // Debug.Log($"PlayerModelManager: Loaded model for class {classType} ({classData.className})");
        }

        // removes all children from model root so only current class model remains
        private void ClearModelRootChildren()
        {
            if (_modelRoot == null) return;

            for (int i = _modelRoot.childCount - 1; i >= 0; i--)
            {
                var child = _modelRoot.GetChild(i);
                if (child == null) continue;

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
        
        // configures the active model animator and binds network animator to it
        private void SetupAnimator()
        {
            // find animator on spawned model hierarchy
            var childAnimator = _currentModel.GetComponentInChildren<Animator>(true);
            if (childAnimator == null)
            {
                _animator = null;
                Debug.LogError("PlayerModelManager: No Animator found on spawned model. add an Animator component to each class model prefab root.");
                return;
            }

            _animator = childAnimator;
            _animator.enabled = true;
            
            // apply avatar from ModelData if provided
            if (_currentModelData != null && _currentModelData.avatar != null)
            {
                _animator.avatar = _currentModelData.avatar;
            }
            
            // apply controller: per-class override > shared > whatever existed
            if (_currentModelData != null && _currentModelData.overrideController != null)
            {
                _animator.runtimeAnimatorController = _currentModelData.overrideController;
            }
            else if (sharedAnimatorController != null)
            {
                _animator.runtimeAnimatorController = sharedAnimatorController;
            }

            // ensure animation events on the model animator can reach PlayerCombat
            var relay = _animator.GetComponent<PlayerAnimationEventRelay>();
            if (relay == null)
            {
                relay = _animator.gameObject.AddComponent<PlayerAnimationEventRelay>();
            }
            relay.Configure(_playerCombat);

            if (_animator.avatar == null)
            {
                Debug.LogWarning("PlayerModelManager: Animator avatar is null after setup. this will cause t-pose. assign ModelData.avatar on the class model prefab.");
            }

            if (_animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("PlayerModelManager: Animator controller is null after setup. assign sharedAnimatorController or ModelData.overrideController.");
            }
            
            // rebind to discover new bone hierarchy from the model child
            _animator.Rebind();
            _animator.Update(0f);

            // bind network animator after the animator is fully configured (if networked)
            if (_networkAnimator != null)
            {
                if (_ownerNetworkAnimator != null)
                {
                    _ownerNetworkAnimator.BindRuntimeAnimator(_animator);
                }
                else
                {
                    _networkAnimator.Animator = _animator;
                }
            }
        }

        // caches attachment point transforms from the current model's ModelData
        private void CacheAttachmentPoints()
        {
            WeaponMountR = null;
            WeaponMountL = null;
            ProjectileSpawnPoint = null;
            
            if (_currentModelData != null)
            {
                WeaponMountR = _currentModelData.weaponMountR;
                WeaponMountL = _currentModelData.weaponMountL;
                ProjectileSpawnPoint = _currentModelData.projectileSpawnPoint;
            }
        }
        
        // resizes the character controller to match the model's dimensions
        private void AdjustCharacterController()
        {
            if (_characterController == null || _currentModelData == null) return;
            
            _characterController.height = _currentModelData.characterHeight;
            _characterController.radius = _currentModelData.characterRadius;
            _characterController.center = _currentModelData.characterCenter;
        }
        
        // moves the name tag to the correct height for this model (in case we have models of different sizes)
        private void AdjustNameTagHeight()
        {
            if (_currentModelData == null) return;
            
            var nameTag = GetComponentInChildren<PlayerNameTag>(true);
            if (nameTag != null)
            {
                var rt = nameTag.GetComponent<RectTransform>();
                if (rt != null)
                {
                    var pos = rt.anchoredPosition;
                    pos.y = _currentModelData.nameTagHeight;
                    rt.anchoredPosition = pos;
                }
            }
        }
        
        // tells PlayerController to re-cache its renderer array after model swap
        private void RefreshPlayerRenderers()
        {
            if (_playerController != null)
            {
                _playerController.RefreshRenderers();
            }
        }
        
        // recursively sets layer on a gameobject and all its children
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        
        // gets class data from the class registry
        private PlayerClass GetClassData(int classId)
        {
            if (ClassRegistry.Instance == null)
            {
                Debug.LogError("PlayerModelManager: ClassRegistry not found!");
                return null;
            }
            
            return ClassRegistry.Instance.GetClass(classId);
        }
    }
}
