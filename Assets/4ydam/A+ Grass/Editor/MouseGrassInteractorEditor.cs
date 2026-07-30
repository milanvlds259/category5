using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(MouseGrassInteractionHandler))]
public class GrassMouseInteractionHandlerEditor : Editor
{
    public VisualTreeAsset visualTreeAsset;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        if (visualTreeAsset == null)
        {
            root.Add(new HelpBox("Assign a VisualTreeAsset to GrassMouseInteractionHandlerEditor.", HelpBoxMessageType.Warning));
            return root;
        }

        root.Add(new IMGUIContainer(AGrassBannerDrawer.DrawBanner));

        visualTreeAsset.CloneTree(root);
        
        return root;
    }
}
