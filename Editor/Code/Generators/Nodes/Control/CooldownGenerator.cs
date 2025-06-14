using System;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Cooldown))]
    public class CooldownGenerator : VariableNodeGenerator
    {
        public CooldownGenerator(Cooldown unit) : base(unit)
        {
            NameSpaces = "Unity.VisualScripting.Community";
        }

        private Cooldown Unit => unit as Cooldown;

        public override AccessModifier AccessModifier => AccessModifier.Private;

        public override FieldModifier FieldModifier => FieldModifier.None;

        public override string Name => "cooldown" + count;

        public override Type Type => typeof(CooldownLogic);

        public override object DefaultValue => new CooldownLogic();

        public override bool HasDefaultValue => true;

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            variableName = Name;
            if (!typeof(MonoBehaviour).IsAssignableFrom(data.ScriptType))
            {
                return CodeBuilder.Indent(indent + 1) + MakeClickableForThisUnit(CodeUtility.ToolTip("Cooldown only works with ScriptGraphAssets, ScriptMachines or a ClassAsset that inherits MonoBehaviour", "Could not generate Cooldown", ""));
            }

            var output = string.Empty;

            if (input == Unit.enter)
            {
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + ".StartCooldown(") + GenerateValue(Unit.duration, data) + MakeClickableForThisUnit(", ") + GenerateValue(Unit.unscaledTime, data) + MakeClickableForThisUnit(");") + "\n";
            }
            else if (input == Unit.reset)
            {
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + ".ResetCooldown();") + "\n";
            }

            if (Unit.exitReady.hasValidConnection && !data.generatorData.TryGetValue(Unit.exitReady, out var readyGenerated))
            {
                data.generatorData.Add(Unit.exitReady, true);
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "OnReady".VariableHighlight() + " += ") + GetAction(Unit.exitReady, indent, data) + MakeClickableForThisUnit(";") + "\n";
            }

            if (Unit.exitNotReady.hasValidConnection && !data.generatorData.TryGetValue(Unit.exitNotReady, out var notReadyGenerated))
            {
                data.generatorData.Add(Unit.exitNotReady, true);
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "NotReady".VariableHighlight() + " += ") + GetAction(Unit.exitNotReady, indent, data) + MakeClickableForThisUnit(";") + "\n";
            }

            if (Unit.tick.hasValidConnection && !data.generatorData.TryGetValue(Unit.tick, out var tickGenerated))
            {
                data.generatorData.Add(Unit.tick, true);
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "OnTick".VariableHighlight() + " += ") + GetAction(Unit.tick, indent, data) + MakeClickableForThisUnit(";") + "\n";
            }

            if (Unit.becameReady.hasValidConnection && !data.generatorData.TryGetValue(Unit.becameReady, out var becameReadyGenerated))
            {
                data.generatorData.Add(Unit.becameReady, true);
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "OnCompleteAction".VariableHighlight() + " += ") + GetAction(Unit.becameReady, indent, data) + MakeClickableForThisUnit(";") + "\n";
            }

            return output;
        }

        public string GetAction(ControlOutput controlOutput, int indent, ControlGenerationData data)
        {
            if (!controlOutput.hasValidConnection)
                return "";

            var output = "";
            output += MakeClickableForThisUnit("() =>") + "\n";
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("{") + "\n";
            data.NewScope();
            data.SetReturns(typeof(void));
            output += GetNextUnit(controlOutput, data, indent + 1);
            data.ExitScope();
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("}");
            return output;
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            if (output == Unit.remainingSeconds)
            {
                return MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "RemainingTime".VariableHighlight());
            }
            else if (output == Unit.remainingRatio)
            {
                return MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "RemainingPercentage".VariableHighlight());
            }
            return base.GenerateValue(output, data);
        }
    }
}