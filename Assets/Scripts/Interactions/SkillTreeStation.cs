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
        public override string GetInteractPrompt() => "[F] Skill Tree";

        public override void Interact(GameObject player)
        {
            Debug.Log("SkillTreeStation: Interact called");

            int classId = ClassSelectionManager.GetClassId();
            Debug.Log($"SkillTreeStation: classId = {classId} (NoClassId = {PlayerClass.NoClassId})");

            if (classId == PlayerClass.NoClassId)
            {
                Debug.LogWarning("SkillTreeStation: No class selected! Select a class first.");
                return;
            }

            Debug.Log($"SkillTreeStation: SkillTreeUI.Instance = {(SkillTreeUI.Instance != null ? "found" : "NULL")}");
            Debug.Log($"SkillTreeStation: SkillTreeManager.Instance = {(SkillTreeManager.Instance != null ? "found" : "NULL")}");
            Debug.Log($"SkillTreeStation: SkillPointManager.Instance = {(SkillPointManager.Instance != null ? "found" : "NULL")}");

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