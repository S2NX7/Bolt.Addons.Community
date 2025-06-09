
using System;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(ChanceFlow))]
    public class ChanceFlowGenerator : NodeGenerator<ChanceFlow>
    {
        public ChanceFlowGenerator(Unit unit) : base(unit) { }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            var output = "";
            if (Unit.trueOutput.hasValidConnection)
            {
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("if".ControlHighlight() + " (" + "CSharpUtility".TypeHighlight() + $".Chance(") + GenerateValue(Unit.value, data) + MakeSelectableForThisUnit("))");
                output += "\n" + CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("{") + "\n";
                data.NewScope();
                output += GetNextUnit(Unit.trueOutput, data, indent + 1);
                data.ExitScope();
                output += "\n" + CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("}") + "\n";
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("else".ControlHighlight());
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("{") + "\n";
                data.NewScope();
                output += GetNextUnit(Unit.falseOutput, data, indent + 1);
                data.ExitScope();
                output += "\n" + CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("}") + "\n";
            }
            else if (!Unit.trueOutput.hasValidConnection && Unit.falseOutput.hasValidConnection)
            {
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("if".ControlHighlight() + " (!" + "CSharpUtility".TypeHighlight() + $".Chance(") + GenerateValue(Unit.value, data) + MakeSelectableForThisUnit("))");
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("{");
                data.NewScope();
                output += GetNextUnit(Unit.falseOutput, data, indent + 1);
                data.ExitScope();
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("}");
            }

            return output;
        }
    }
}
