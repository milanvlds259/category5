using UnityEngine;
using UnityEditor;
using Category5.Player;
using Category5;

public class AbilityEditor : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
}

[CustomEditor(typeof(PlayerClass))]
public class AbilityEditorWindow : Editor
{
    public override void OnInspectorGUI()
    {
        
        DrawDefaultInspector();

        PlayerClass playerClass = (PlayerClass)target;


        AbilityBase ability1Base = playerClass.ability1Prefab != null ? playerClass.ability1Prefab.GetComponent<AbilityBase>() : null;
        AbilityBase ability2Base = playerClass.ability2Prefab != null ? playerClass.ability2Prefab.GetComponent<AbilityBase>() : null;
        AbilityBase ability3Base = playerClass.ability3Prefab != null ? playerClass.ability3Prefab.GetComponent<AbilityBase>() : null;

        AbilityData ability1Data = ability1Base.Data;
        AbilityData ability2Data = ability2Base.Data;
        AbilityData ability3Data = ability3Base.Data;

        if (ability1Data != null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.TextArea(ability1Data.abilityName, EditorStyles.boldLabel);
            EditorGUILayout.TextArea(ability1Data.description, EditorStyles.wordWrappedLabel);
            ability1Data.damageCoefficient = EditorGUILayout.FloatField("Ability 1 Damage Coefficient", ability1Data != null ? ability1Data.damageCoefficient : 0f);
            ability1Data.cooldownDuration = EditorGUILayout.FloatField("Ability 1 Cooldown Duration", ability1Data != null ? ability1Data.cooldownDuration : 0f);
            ability1Data.manaCost = EditorGUILayout.IntField("Ability 1 Mana Cost", ability1Data != null ? ability1Data.manaCost : 0);
        }

        if (ability2Data != null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.TextArea(ability2Data.abilityName, EditorStyles.boldLabel);
            EditorGUILayout.TextArea(ability2Data.description, EditorStyles.wordWrappedLabel);
            ability2Data.damageCoefficient = EditorGUILayout.FloatField("Ability 1 Damage Coefficient", ability2Data != null ? ability2Data.damageCoefficient : 0f);
            ability2Data.cooldownDuration = EditorGUILayout.FloatField("Ability 1 Cooldown Duration", ability2Data != null ? ability2Data.cooldownDuration : 0f);
            ability2Data.manaCost = EditorGUILayout.IntField("Ability 1 Mana Cost", ability2Data != null ? ability2Data.manaCost : 0);
        }

        if (ability3Data != null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.TextArea(ability3Data.abilityName, EditorStyles.boldLabel);
            EditorGUILayout.TextArea(ability3Data.description, EditorStyles.wordWrappedLabel);
            ability3Data.damageCoefficient = EditorGUILayout.FloatField("Ability 1 Damage Coefficient", ability3Data != null ? ability3Data.damageCoefficient : 0f);
            ability3Data.cooldownDuration = EditorGUILayout.FloatField("Ability 1 Cooldown Duration", ability3Data != null ? ability3Data.cooldownDuration : 0f);
            ability3Data.manaCost = EditorGUILayout.IntField("Ability 1 Mana Cost", ability3Data != null ? ability3Data.manaCost : 0);
        }


    }
}

