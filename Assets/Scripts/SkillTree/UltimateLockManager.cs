using UnityEngine;
using System;
using Category5.Core;
using Category5.Player;

namespace Category5.SkillTree
{
    /// <summary>
    /// Component on the player that tracks whether the current class's ultimate (R ability) is unlocked.
    /// PlayerAbilityManager checks this before allowing R ability use.
    /// AbilityCooldownUI subscribes to show/hide the lock indicator.
    /// </summary>
    public class UltimateLockManager : MonoBehaviour
    {
        /// <summary>Fired when the ultimate lock state changes. true = unlocked, false = locked.</summary>
        public event Action<bool> OnUltimateLockStateChanged;

        /// <summary>True if the ultimate is currently unlocked for this player's class.</summary>
        public bool IsUnlocked { get; private set; } = false;

        private PlayerClassManager _classManager;
        private int _currentClassId = PlayerClass.NoClassId;

        private void Awake()
        {
            _classManager = GetComponent<PlayerClassManager>();
        }

        private void OnEnable()
        {
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnNodeUnlocked += HandleNodeUnlocked;
                SkillTreeManager.Instance.OnTreeReset += HandleTreeReset;
            }
        }

        private void OnDisable()
        {
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnNodeUnlocked -= HandleNodeUnlocked;
                SkillTreeManager.Instance.OnTreeReset -= HandleTreeReset;
            }
        }

        private void Start()
        {
            // Subscribe to class changes if we have a class manager
            if (_classManager != null)
            {
                _classManager.SelectedClassId.OnValueChanged += OnClassChanged;
                // Check initial state
                RefreshLockState(_classManager.SelectedClassId.Value);
            }
        }

        private void OnDestroy()
        {
            if (_classManager != null)
            {
                _classManager.SelectedClassId.OnValueChanged -= OnClassChanged;
            }
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnNodeUnlocked -= HandleNodeUnlocked;
                SkillTreeManager.Instance.OnTreeReset -= HandleTreeReset;
            }
        }

        private void OnClassChanged(int oldClass, int newClass)
        {
            RefreshLockState(newClass);
        }

        /// <summary>Updates the lock state for the current class and fires event if changed.</summary>
        public void RefreshLockState(int classId)
        {
            _currentClassId = classId;

            if (classId == PlayerClass.NoClassId)
            {
                IsUnlocked = false;
                OnUltimateLockStateChanged?.Invoke(false);
                return;
            }

            if (SkillTreeManager.Instance == null)
            {
                // No skill tree manager = no lock system, allow ultimate
                IsUnlocked = true;
                OnUltimateLockStateChanged?.Invoke(true);
                return;
            }

            bool wasUnlocked = IsUnlocked;
            IsUnlocked = SkillTreeManager.Instance.IsUltimateUnlocked(classId);

            if (wasUnlocked != IsUnlocked)
            {
                OnUltimateLockStateChanged?.Invoke(IsUnlocked);
            }
        }

        /// <summary>Called when any node is unlocked - checks if it affects our current class.</summary>
        private void HandleNodeUnlocked(int classId, int nodeId)
        {
            if (classId == _currentClassId)
            {
                RefreshLockState(_currentClassId);
            }
        }

        /// <summary>Called when any tree is reset - checks if it affects our current class.</summary>
        private void HandleTreeReset(int classId)
        {
            if (classId == _currentClassId)
            {
                RefreshLockState(_currentClassId);
            }
        }
    }
}