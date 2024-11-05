using Unity;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using System.Linq;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;
[NodeGenerator(typeof(Unity.VisualScripting.For))]
public sealed class ForGenerator : LocalVariableGenerator<Unity.VisualScripting.For>
{
    public ForGenerator(Unity.VisualScripting.For unit) : base(unit)
    {
    }

    public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
    {
        var output = string.Empty;

        if (input == Unit.enter)
        {
            var initialization = GenerateValue(Unit.firstIndex, data);
            var condition = GenerateValue(Unit.lastIndex, data);
            var iterator = GenerateValue(Unit.step, data);

            variableName = data.AddLocalNameInScope("i", typeof(int));
            variableType = typeof(int);

            string varName = MakeSelectableForThisUnit(variableName.VariableHighlight());

            output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit($"for".ControlHighlight() + "(int ".ConstructHighlight()) + $"{varName}".VariableHighlight() + MakeSelectableForThisUnit(" = ") + initialization + MakeSelectableForThisUnit("; ") + varName.VariableHighlight() + $"{MakeSelectableForThisUnit(" < ")}{condition}{MakeSelectableForThisUnit("; ")}" + varName.VariableHighlight() + $"{MakeSelectableForThisUnit(" += ")}{iterator}{MakeSelectableForThisUnit(")")}";
            output += "\n";
            output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("{");
            output += "\n";

            if (Unit.body.hasAnyConnection)
            {
                data.NewScope();
                output += GetNextUnit(Unit.body, data, indent + 1);
                data.ExitScope();
            }

            output += "\n";
            output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("}");
            output += "\n";
        }

        if (Unit.exit.hasAnyConnection)
        {
            output += GetNextUnit(Unit.exit, data, indent);
            output += "\n";
        }


        return output;
    }

    public override string GenerateValue(ValueOutput output, ControlGenerationData data)
    {
        return MakeSelectableForThisUnit(variableName.VariableHighlight());
    }

    public override string GenerateValue(ValueInput input, ControlGenerationData data)
    {
        if (input.hasValidConnection)
        {
            data.SetExpectedType(input.type);
            var connectedCode = GetNextValueUnit(input, data);
            data.RemoveExpectedType();
            return new ValueCode(connectedCode, input.type, ShouldCast(input, data, false));
        }
        else
        {
            return Unit.defaultValues[input.key].As().Code(false, unit);
        }
    }
}