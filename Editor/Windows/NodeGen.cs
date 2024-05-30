using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;
using Unity.VisualScripting.Community.Libraries.CSharp;

public class CustomNodeGeneratorWindow : EditorWindow
{
    private List<InputData> controlInputs = new List<InputData>();
    private List<InputData> controlOutputs = new List<InputData>();
    private List<ValueInputData> valueInputs = new List<ValueInputData>();
    private List<ValueOutputData> valueOutputs = new List<ValueOutputData>();
    private string fileName = "MyCustomNode";
    private bool useUnit;
    private bool useMachineEventUnit;
    private bool useEventUnit;
    private bool openFileAfterGeneration = true;
    private Type[] alltypes;

    private Vector2 scrollPosition;
    private string filePath = "";

    private void OnEnable()
    {
        alltypes = Codebase.settingsAssemblies
        .SelectMany(a => a.GetTypes().Where(type => !type.IsGenericTypeDefinition))
        .ToArray();
    }

    [MenuItem("Window/Community Addons/Custom Node Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<CustomNodeGeneratorWindow>("Custom Node Generator");
        window.minSize = new Vector2(1000, 400);
    }

    private int selectedOption = 0;
    private string[] options = { "Unit", "MachineEventUnit", "EventUnit" };

    private void OnGUI()
    {
        GUILayout.Label("Custom Node Generator", EditorStyles.boldLabel);

        GUILayout.Space(10);

        fileName = EditorGUILayout.TextField("File Name", fileName);

        catagory = EditorGUILayout.TextField("Category", catagory);
        openFileAfterGeneration = EditorGUILayout.Toggle("Open Generated File", openFileAfterGeneration);

        GUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUI.BeginChangeCheck();

        selectedOption = EditorGUILayout.Popup("Select Option", selectedOption, options);

        if (EditorGUI.EndChangeCheck())
        {
            useUnit = selectedOption == 0;
            useMachineEventUnit = selectedOption == 1;
            useEventUnit = selectedOption == 2;
        }

        GUILayout.Space(10);

        DrawInputList(controlInputs, "Control Inputs", false);
        DrawInputList(controlOutputs, "Control Outputs", false);
        DrawValueInputList(valueInputs, "Value Inputs");
        DrawValueOutputList(valueOutputs, "Value Outputs");

        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        GUIStyle fontSize = new GUIStyle(GUI.skin.button);
        fontSize.fontSize = 16;

        if (GUILayout.Button("Generate Custom Node", fontSize, GUILayout.Height(30)))
        {
            ShowSaveFileDialog();
        }
    }

    private void ShowSaveFileDialog()
    {
        string defaultFileName = $"{fileName.Replace(" ", "")}.cs";
        string defaultDirectory = "Assets";
        filePath = EditorUtility.SaveFilePanel("Save Generated Code", defaultDirectory, defaultFileName, "cs");

        if (!string.IsNullOrEmpty(filePath))
        {
            GenerateCustomNodeCode();
        }
    }

    private void GenerateCustomNodeCode()
    {
        string code = GenerateCustomNodeTemplate(fileName, GetSelectedUnitType());
        SaveGeneratedCode(code);
    }

    private void SaveGeneratedCode(string code)
    {
        System.IO.File.WriteAllText(filePath, code);
        AssetDatabase.Refresh();
        Debug.Log($"Generated custom node code saved at {filePath}");

        if (openFileAfterGeneration)
        {
            EditorUtility.OpenWithDefaultApp(filePath);
        }
    }

    private void DrawInputList(List<InputData> inputs, string title, bool showTypeOptions = true)
    {
        GUILayout.Label(title, EditorStyles.boldLabel);

        List<int> indicesToRemove = new List<int>();

        foreach (InputData input in inputs)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label($"Input {inputs.IndexOf(input) + 1}", EditorStyles.boldLabel);

            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                indicesToRemove.Add(inputs.IndexOf(input));
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            EditorGUI.indentLevel++;

            input.name = EditorGUILayout.TextField("Name", input.name);
            input.methodName = EditorGUILayout.TextField("Method Name", input.methodName);
            input.HideLabel = EditorGUILayout.Toggle("Hide Label", input.HideLabel);

            if (showTypeOptions)
            {
                DrawTypeDropdown(input, input.type);
            }

            EditorGUI.indentLevel--;

            GUILayout.Space(10);
        }

        foreach (int index in indicesToRemove)
        {
            if (index >= 0 && index < inputs.Count)
            {
                inputs.RemoveAt(index);
            }
        }

        if (GUILayout.Button("Add " + title.Substring(0, title.Length - 1)))
        {
            inputs.Add(new InputData());
        }

        GUILayout.Space(10);
    }

    private void DrawValueInputList(List<ValueInputData> valueInputs, string title)
    {
        GUILayout.Label(title, EditorStyles.boldLabel);

        List<int> indicesToRemove = new List<int>();

        foreach (ValueInputData valueInput in valueInputs)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label($"Value Input {valueInputs.IndexOf(valueInput) + 1}", EditorStyles.boldLabel);

            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                indicesToRemove.Add(valueInputs.IndexOf(valueInput));
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            EditorGUI.indentLevel++;

            valueInput.name = EditorGUILayout.TextField("Name", valueInput.name);
            DrawTypeDropdown(valueInput, valueInput.type);
            valueInput.HideLabel = EditorGUILayout.Toggle("Hide Label", valueInput.HideLabel);

            if (valueInput.type == typeof(GameObject) || typeof(Component).IsAssignableFrom(valueInput.type))
            {
                valueInput.nullMeansSelf = EditorGUILayout.Toggle("Null Means Self", valueInput.nullMeansSelf);
            }

            EditorGUI.indentLevel--;

            GUILayout.Space(10);
        }

        foreach (int index in indicesToRemove)
        {
            if (index >= 0 && index < valueInputs.Count)
            {
                valueInputs.RemoveAt(index);
            }
        }

        if (GUILayout.Button("Add " + title.Substring(0, title.Length - 1)))
        {
            valueInputs.Add(new ValueInputData());
        }

        GUILayout.Space(10);
    }

    private void DrawValueOutputList(List<ValueOutputData> valueOutputs, string title)
    {
        GUILayout.Label(title, EditorStyles.boldLabel);

        List<int> indicesToRemove = new List<int>();

        foreach (ValueOutputData valueOutput in valueOutputs)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label($"Value Output {valueOutputs.IndexOf(valueOutput) + 1}", EditorStyles.boldLabel);

            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                indicesToRemove.Add(valueOutputs.IndexOf(valueOutput));
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            EditorGUI.indentLevel++;

            valueOutput.name = EditorGUILayout.TextField("Name", valueOutput.name);
            DrawTypeDropdown(valueOutput, valueOutput.type);
            valueOutput.HideLabel = EditorGUILayout.Toggle("Hide Label", valueOutput.HideLabel);
            valueOutput.triggersMethod = EditorGUILayout.Toggle("Triggers Method", valueOutput.triggersMethod);

            if (valueOutput.triggersMethod) 
            {
                valueOutput.methodName = EditorGUILayout.TextField("Method Name", valueOutput.methodName);
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(10);
        }

        foreach (int index in indicesToRemove)
        {
            if (index >= 0 && index < valueOutputs.Count)
            {
                valueOutputs.RemoveAt(index);
            }
        }

        if (GUILayout.Button("Add " + title.Substring(0, title.Length - 1)))
        {
            valueOutputs.Add(new ValueOutputData());
        }

        GUILayout.Space(10);
    }


    private void DrawTypeDropdown(object Input, Type selectedType)
    {
        Type[] types = alltypes;

        ValueOutputData outputData = null;
        ValueInputData inputData = null;
        InputData inputDataInput = null;
        GUIContent buttonContent = new GUIContent();

        var lastRect = GUILayoutUtility.GetLastRect();

        if (Input is ValueOutputData)
        {
            outputData = (ValueOutputData)Input;
        }
        else if (Input is ValueInputData)
        {
            inputData = (ValueInputData)Input;
        }
        else if (Input is InputData)
        {
            inputDataInput = (InputData)Input;
        }

        int selectedIndex = Array.IndexOf(types, selectedType);
        if (selectedIndex < 0)
            selectedIndex = 0;

        if (Input is ValueInputData)
        {
            if (inputData.type != null)
            {
                buttonContent = new GUIContent(inputData.type.CSharpName(false).RemoveHighlights().RemoveMarkdown(), inputData.type.Icon()?[IconSize.Small]);
            }
            else
            {
                buttonContent = new GUIContent("Choose Type");
            }
        }
        else if (Input is ValueOutputData)
        {
            if (outputData.type != null)
            {
                buttonContent = new GUIContent(outputData.type.CSharpName(false).RemoveHighlights().RemoveMarkdown(), outputData.type.Icon()?[IconSize.Small]);
            }
            else
            {
                buttonContent = new GUIContent("Choose Type");
            }
        }

        if (GUILayout.Button(buttonContent, GUILayout.MaxHeight(19f)))
        {
            LudiqGUI.FuzzyDropdown(lastRect, new TypeOptionTree(types), selectedIndex, (index) =>
            {
                if (inputData != null)
                {
                    inputData.type = (Type)index;
                }

                if (outputData != null)
                {
                    outputData.type = (Type)index;
                }

                if (inputDataInput != null)
                {
                    inputDataInput.type = (Type)index;
                }
            });
        }
    }

    private string[] GetReadableTypeNames(Type[] types)
    {
        return types.Select(type => GetShortTypeName(type)).ToArray();
    }

    private string GetShortTypeName(Type type)
    {
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(float)) return "float";
        if (type == typeof(string)) return "string";
        return type.Name;
    }

    private string GetSelectedUnitType()
    {
        if (useMachineEventUnit)
            return "MachineEventUnit<EmptyEventArgs>";
        else if (useEventUnit)
            return "EventUnit<EmptyEventArgs>";
        else
            return "Unit";
    }

    private string catagory = "Community/YourCatagory";

    private string GenerateCustomNodeTemplate(string fileName, string unitType)
    {
        string template = $@"using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

";
        if (!string.IsNullOrEmpty(catagory))
        {
            template += $@"[UnitCategory(""{catagory}"")]";
        }

        template += $@"
[UnitTitle(""{fileName}"")]//Unit title
[TypeIcon(typeof(object))]//Unit icon
public class {fileName.Replace(" ", "")} : {unitType}
{{
    ";

        if (useEventUnit)
        {
            template += @"
    protected override bool register => throw new System.NotImplementedException();
";
        }

        // Setup variables for ControlInputs
        for (int i = 0; i < controlInputs.Count; i++)
        {
            string inputName = string.IsNullOrEmpty(controlInputs[i].name) ? $"ControlInput{i}" : controlInputs[i].name;
            template += $@"
    [DoNotSerialize]";
            if (controlInputs[i].HideLabel)
            {
                template += $@"
    [PortLabelHidden]";
            }
            template += $@"
    public ControlInput {inputName.Replace(" ", "")};";
        }

        // Setup variables for ControlOutputs
        for (int i = 0; i < controlOutputs.Count; i++)
        {
            string outputName = string.IsNullOrEmpty(controlOutputs[i].name) ? $"ControlOutput{i}" : controlOutputs[i].name;
            template += $@"
    [DoNotSerialize]";
            if (controlOutputs[i].HideLabel)
            {
                template += $@"
    [PortLabelHidden]";
            }
            template += $@"
    public ControlOutput {outputName.Replace(" ", "")};";
        }

        // Setup variables for ValueInputs
        for (int i = 0; i < valueInputs.Count; i++)
        {
            if (valueInputs[i].nullMeansSelf)
            {
                string inputName = string.IsNullOrEmpty(valueInputs[i].name) ? $"ValueInput{i}" : valueInputs[i].name;
                template += $@"
    [NullMeansSelf]";
                if (valueInputs[i].HideLabel)
                {
                    template += $@"
    [PortLabelHidden]";
                }
                template += $@"
    [DoNotSerialize]
    public ValueInput {inputName.Replace(" ", "")};";
            }
            else
            {
                string inputName = string.IsNullOrEmpty(valueInputs[i].name) ? $"ValueInput{i}" : valueInputs[i].name;
                template += $@"
    [DoNotSerialize]";
                if (valueInputs[i].HideLabel)
                {
                    template += $@"
    [PortLabelHidden]";
                }
                template += $@"
    public ValueInput {inputName.Replace(" ", "")};";
            }
        }

        // Setup variables for ValueOutputs
        for (int i = 0; i < valueOutputs.Count; i++)
        {
            string outputName = string.IsNullOrEmpty(valueOutputs[i].name) ? $"ValueOutput{i}" : valueOutputs[i].name;
            template += $@"
    [DoNotSerialize]";
            if (valueOutputs[i].HideLabel)
            {
                template += $@"
    [PortLabelHidden]";
            }
            template += $@"
    public ValueOutput {outputName.Replace(" ", "")};";
        }

        // Definition method
        template += @"
    protected override void Definition()
    {";
        if (useEventUnit || useMachineEventUnit)
        {
            template += @"
        base.Definition();";
        }

        // ControlInputs
        foreach (InputData input in controlInputs)
        {
            string inputName = string.IsNullOrEmpty(input.name) ? $"ControlInput{controlInputs.IndexOf(input)}" : input.name;
            template += $@"
        {inputName.Replace(" ", "")} = ControlInput(nameof({inputName.Replace(" ", "")}), {input.methodName});";
        }

        // ControlOutputs
        foreach (InputData output in controlOutputs)
        {
            string outputName = string.IsNullOrEmpty(output.name) ? $"ControlOutput{controlOutputs.IndexOf(output)}" : output.name;
            template += $@"
        {outputName.Replace(" ", "")} = ControlOutput(nameof({outputName.Replace(" ", "")}));";
        }

        // ValueInputs
        foreach (ValueInputData valueInput in valueInputs)
        {
            string valueInputName = string.IsNullOrEmpty(valueInput.name) ? $"ValueInput{valueInputs.IndexOf(valueInput)}" : valueInput.name;

            if (valueInput.nullMeansSelf)
            {
                template += $@"
        {valueInputName.Replace(" ", "")} = ValueInput<{valueInput.type.CSharpFullName().Replace(" ", "")}>(nameof({valueInputName.Replace(" ", "")}), default).NullMeansSelf();";
            }
            else
            {
                template += $@"
        {valueInputName.Replace(" ", "")} = ValueInput<{valueInput.type.CSharpFullName().Replace(" ", "")}>(nameof({valueInputName.Replace(" ", "")}), default);";
            }
        }

        // ValueOutputs
        foreach (ValueOutputData valueOutput in valueOutputs)
        {
            string valueOutputName = string.IsNullOrEmpty(valueOutput.name) ? $"ValueOutput{valueOutputs.IndexOf(valueOutput)}" : valueOutput.name;
            template += $@"
        {valueOutputName.Replace(" ", "")} = ValueOutput<{valueOutput.type.CSharpFullName().Replace(" ", "")}>(nameof({valueOutputName.Replace(" ", "")}){(valueOutput.triggersMethod ? $", {valueOutput.methodName}" : string.Empty)});";
        }

        foreach (InputData input in controlInputs)
        {
            foreach (InputData outputData in controlOutputs)
            {
                string outputName = string.IsNullOrEmpty(outputData.name) ? $"ControlOutput{controlOutputs.IndexOf(outputData)}" : outputData.name;
                template += $@"
        Succession({input.name.Replace(" ", "")}, {outputName.Replace(" ", "")});";
            }
        }

        template += @"
    }";

        foreach (InputData input in controlInputs) {
            template += $@"
    public ControlOutput {input.methodName}(Flow flow) 
    {{
        // Enter your logic here. You can add more methods if needed.

        // Put the name of the output you want to trigger when the node is entered.
        return null;
    }}";
        }
        foreach (ValueOutputData valueOutput in valueOutputs)
        {
            if (valueOutput.triggersMethod)
            {
                template += @$"
    public {valueOutput.type.DisplayName()} {valueOutput.methodName}(Flow flow)
    {{



        return null;
    }}
";
            }
        }
        template += @"
}";
        return template;
}

[Serializable]
public class InputData : ISerializationCallbackReceiver
{
    public string name;
    public bool HideLabel;
    public string methodName;
    public Type type;

    private string previousType;

    public void OnAfterDeserialize()
    {
        type = Type.GetType(previousType);
    }

    public void OnBeforeSerialize()
    {
        previousType = type.AssemblyQualifiedName;
    }
}

[Serializable]
public class ValueInputData : ISerializationCallbackReceiver
{
    public string name;
    public Type type;
    public bool HideLabel;
    public bool nullMeansSelf;

    private string previousType;

    public void OnAfterDeserialize()
    {
        type = Type.GetType(previousType);
    }

    public void OnBeforeSerialize()
    {
        previousType = type.AssemblyQualifiedName;
    }
}

    [Serializable]
    public class ValueOutputData : ISerializationCallbackReceiver
    {
        public string name;
        public bool HideLabel;
        public bool triggersMethod;
        public string methodName;
        public Type type;

        private string previousType;

        public void OnAfterDeserialize()
        {
            type = Type.GetType(previousType);
        }

        public void OnBeforeSerialize()
        {
            previousType = type.AssemblyQualifiedName;
        }
    }
}