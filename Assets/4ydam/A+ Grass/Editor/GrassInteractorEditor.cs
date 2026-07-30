using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

[CustomEditor(typeof(GrassInteractor))]
public class GrassInteractorEditor : Editor
{
    public VisualTreeAsset visualTreeAsset;
    private const string DocumentationUrl = "https://4ydam.com/assets/a+grass/getting-started/";

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        if (visualTreeAsset == null)
        {
            root.Add(new HelpBox("Assign a VisualTreeAsset to GrassInteractorEditor.", HelpBoxMessageType.Warning));
            return root;
        }

        root.Add(new IMGUIContainer(AGrassBannerDrawer.DrawBanner));

        visualTreeAsset.CloneTree(root);

        var documentationButton = root.Q<Button>("documentationButton");
        if (documentationButton != null)
        {
            documentationButton.clicked += () => Application.OpenURL(DocumentationUrl);
        }

        var interaction = root.Q<VisualElement>("Interaction_VisualElement");

        var pushRateResetButton = interaction?.Q<VisualElement>("PushRate_VisualElement")?.Q<Button>();
        if (pushRateResetButton != null)
        {
            pushRateResetButton.clicked += () =>
            {
                serializedObject.FindProperty("pushRate").floatValue = 6f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var maxPushStrengthResetButton = interaction?.Q<VisualElement>("MaxpPush_VisualElement")?.Q<Button>();
        if (maxPushStrengthResetButton != null)
        {
            maxPushStrengthResetButton.clicked += () =>
            {
                serializedObject.FindProperty("maxPushStrength").floatValue = 2f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var motion = root.Q<VisualElement>("Motion_VisualElement");

        var positionOffsetField = motion?.Q<Vector3Field>("positionOffset");
        if (positionOffsetField != null)
        {
            positionOffsetField.bindingPath = "positionOffset";
        }

        var movementThresholdResetButton = motion?.Q<VisualElement>("MovementThreshold_VisualElement")?.Q<Button>();
        if (movementThresholdResetButton != null)
        {
            movementThresholdResetButton.clicked += () =>
            {
                serializedObject.FindProperty("movementThreshold").floatValue = 0.5f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var speedSmoothingResetButton = motion?.Q<VisualElement>("SpeedSmoothing_VisualElement")?.Q<Button>();
        if (speedSmoothingResetButton != null)
        {
            speedSmoothingResetButton.clicked += () =>
            {
                serializedObject.FindProperty("speedSmoothing").floatValue = 0.3f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        var detectionRadiusResetButton = motion?.Q<VisualElement>("DetectionRadius_VisualElement")?.Q<Button>();
        if (detectionRadiusResetButton != null)
        {
            detectionRadiusResetButton.clicked += () =>
            {
                serializedObject.FindProperty("detectionRadius").floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
            };
        }

        return root;
    }
}
