using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using UnityEditor;
using UnityEngine;
[Inspector(typeof(IAction))]
public class IActionInspector : Inspector
{
    public IActionInspector(Metadata metadata) : base(metadata)
    {
    }

    protected override float GetHeight(float width, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    protected override void OnGUI(Rect position, GUIContent label)
    {
        if (metadata["Unit"].value == null)
        {
            EditorGUI.HelpBox(position, "No Action Node for this action.", MessageType.Warning);
        }
        else if (GUI.Button(position, "Preview", LudiqStyles.paddedButton))
        {
            var unit = metadata["Unit"].value as DelegateNode;
            CSharpPreviewWindow.Open();
            // CSharpPreviewWindow.instance.preview.output = DelegateNodeGenerator.GetSingleDecorator(unit, unit).GenerateControl(null, new ControlGenerationData(), 0).RemoveMarkdown();
        }
    }
}