using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;

[NodeGenerator(typeof(SubgraphUnit))]
public class SubgraphGenerator : NodeGenerator<SubgraphUnit>
{
    private static Dictionary<string, Type> typeCache = new();
    private Dictionary<CustomEvent, int> customEventIds = new();

    public SubgraphGenerator(SubgraphUnit unit) : base(unit)
    {
        var graphUnits = Unit.nest.graph.units;
        Unit graphInput = null;
        Unit graphOutput = null;

        foreach (var u in graphUnits)
        {
            if (graphInput == null && u is GraphInput gi) graphInput = gi;
            else if (graphOutput == null && u is GraphOutput go) graphOutput = go;
        }

        if (graphOutput != null || graphInput != null)
        {
            var inputGen = GetSingleDecorator(graphInput, graphInput);
            if (inputGen != null)
            {
                inputGen.connectedValueInputs = Unit.valueInputs
                    .Where(i => i.hasDefaultValue || i.hasValidConnection)
                    .ToList();
            }

            var outputGen = GetSingleDecorator(graphOutput, graphOutput);
            if (outputGen != null)
            {
                outputGen.connectedGraphOutputs = Unit.controlOutputs
                    .Where(o => o.hasValidConnection)
                    .ToList();
            }
        }
    }

    public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
    {
        if (data.TryGetGraphPointer(out var graphPointer))
        {
            data.SetGraphPointer(graphPointer.AsReference().ChildReference(Unit, false));
        }

        var graphUnits = Unit.nest.graph.units;
        Unit graphInput = null;
        Unit graphOutput = null;
        List<CustomEvent> customEvents = new();

        foreach (var u in graphUnits)
        {
            if (graphInput == null && u is GraphInput gi) graphInput = gi;
            else if (graphOutput == null && u is GraphOutput go) graphOutput = go;
            else if (u is CustomEvent ce) customEvents.Add(ce);
        }

        var sb = new StringBuilder();
        var subgraphName = Unit.nest.graph.title ?? (Unit.nest.source == GraphSource.Macro ? Unit.nest.macro.name : "UnnamedSubgraph");

        if (CSharpPreviewSettings.ShouldShowSubgraphComment)
        {
            if (graphInput != null || graphOutput != null)
                sb.AppendLine(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit($"//Subgraph: \"{subgraphName}\" Port({input.key}) ".CommentHighlight()));
            else
                sb.AppendLine(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit($"/* Subgraph \"{subgraphName}\" is empty */ ".WarningHighlight()));
        }

        foreach (var variable in Unit.nest.graph.variables)
        {
            var type = GetCachedType(variable.typeHandle.Identification);
            var name = data.AddLocalNameInScope(variable.name, type);
            sb.AppendLine(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit($"{type.As().CSharpName(false, true)} {name.VariableHighlight()} = ") +
                          variable.value.As().Code(true, Unit, true, true, "", false, true) + MakeSelectableForThisUnit(";")
            );
        }

        int index = 0;
        foreach (var customEvent in customEvents)
        {
            index++;
            customEventIds[customEvent] = index;

            if (!typeof(MonoBehaviour).IsAssignableFrom(data.ScriptType))
            {
                sb.AppendLine(CodeUtility.ToolTip("/* Custom Event units only work on monobehaviours */", "Could not generate Custom Events", ""));
                break;
            }

            var generator = GetSingleDecorator(customEvent, customEvent);
            var action = customEvent.coroutine
                ? $"({"args".VariableHighlight()}) => StartCoroutine({GetMethodName(customEvent)}({"args".VariableHighlight()}))"
                : GetMethodName(customEvent);

            sb.AppendLine(CodeBuilder.Indent(indent) + CodeBuilder.CallCSharpUtilityMethod(customEvent, nameof(CSharpUtility.RegisterCustomEvent), generator.GenerateValue(customEvent.target, data), action) + ";");

            var returnType = customEvent.coroutine ? typeof(IEnumerator) : typeof(void);
            sb.AppendLine(CodeBuilder.Indent(indent) + CodeUtility.MakeSelectable(customEvent, $"{returnType.As().CSharpName(false, true)} {GetMethodName(customEvent)}({"CustomEventArgs".TypeHighlight()} {"args".VariableHighlight()})"));
            sb.AppendLine(CodeBuilder.Indent(indent) + CodeUtility.MakeSelectable(customEvent, "{"));
            sb.AppendLine(CodeBuilder.Indent(indent + 1) + CodeUtility.MakeSelectable(customEvent, $"{"if".ControlHighlight()} ({"args".VariableHighlight()}.{"name".VariableHighlight()} == ") + generator.GenerateValue(customEvent.name, data) + CodeUtility.MakeSelectable(customEvent, ")"));
            sb.AppendLine(CodeBuilder.Indent(indent + 1) + CodeUtility.MakeSelectable(customEvent, "{"));
            data.NewScope();
            sb.Append(GetNextUnit(customEvent.trigger, data, indent + 2));
            data.ExitScope();
            sb.AppendLine(CodeBuilder.Indent(indent + 1) + CodeUtility.MakeSelectable(customEvent, "}"));
            sb.AppendLine(CodeBuilder.Indent(indent) + CodeUtility.MakeSelectable(customEvent, "}"));
        }

        if (input.hasValidConnection && graphInput != null)
        {
            var output = graphInput.controlOutputs.FirstOrDefault(o => o.key.Equals(input.key, StringComparison.OrdinalIgnoreCase));
            sb.Append(GetNextUnit(output, data, indent));
        }

        if (data.TryGetGraphPointer(out var _graphPointer))
        {
            data.SetGraphPointer(graphPointer.AsReference().ParentReference(false));
        }
        return sb.ToString();
    }

    private static Type GetCachedType(string typeId)
    {
        if (!typeCache.TryGetValue(typeId, out var type))
        {
            type = Type.GetType(typeId) ?? typeof(object);
            typeCache[typeId] = type;
        }
        return type;
    }

    private string GetMethodName(CustomEvent customEvent)
    {
        return !customEvent.name.hasValidConnection
            ? (string)customEvent.defaultValues[customEvent.name.key]
            : "CustomEvent" + (customEventIds.TryGetValue(customEvent, out var id) ? id : 0);
    }

    public override string GenerateValue(ValueOutput output, ControlGenerationData data)
    {
        var graphOutput = Unit.nest.graph.units.OfType<GraphOutput>().FirstOrDefault();
        return graphOutput != null
            ? GetSingleDecorator(graphOutput, graphOutput).GenerateValue(output, data)
            : "/* Subgraph missing GraphOutput unit */".WarningHighlight();
    }
}