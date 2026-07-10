using UnityEngine;
using Category5.Core;
using Category5.Player;
using Category5.SkillTree;

namespace Category5.Interactions
{
    /// <summary>
    /// Homebase interactable station for accessing the skill tree.
    /// Opens the SkillTreeUI for the player's currently selected class.
    /// </summary>
    public class SkillTreeStation : HubInteractable
    {
        [SerializeField] private string interactPrompt = "Skill Tree";

        public override string GetInteractPrompt() => $"[F] {interactPrompt}";

        public override void Interact(GameObject player)
        {
            // Get the player's selected class
            int classId = ClassSelectionManager.GetClassId();

            if (classId == PlayerClass.NoClassId)
            {
                Debug.LogWarning("SkillTreeStation: No class selected! Select a class first.");
                return;
            }

            if (SkillTreeUI.Instance != null)
            {
                SkillTreeUI.Instance.Open(classId);
            }
            else
            {
                Debug.LogError("SkillTreeStation: SkillTreeUI instance not found in scene!");
            }
        }
    }
}