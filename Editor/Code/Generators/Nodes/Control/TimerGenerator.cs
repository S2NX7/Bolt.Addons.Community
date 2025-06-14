using System;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;

namespace Unity.VisualScripting.Community 
{
    [NodeGenerator(typeof(Timer))]
    public class TimerGenerator : VariableNodeGenerator
    {
        public TimerGenerator(Timer unit) : base(unit)
        {
            NameSpaces = "Unity.VisualScripting.Community";
        }
        private Timer Unit => unit as Timer;
        public override AccessModifier AccessModifier => AccessModifier.Private;
    
        public override FieldModifier FieldModifier => FieldModifier.None;
    
        public override string Name => "timer" + count;
    
        public override Type Type => typeof(TimerLogic);
    
        public override object DefaultValue => new TimerLogic();
    
        public override bool HasDefaultValue => true;
    
        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            variableName = Name;
            if(!typeof(MonoBehaviour).IsAssignableFrom(data.ScriptType))
            {
                return CodeBuilder.Indent(indent + 1) + MakeClickableForThisUnit(CodeUtility.ToolTip("Timers only works with ScriptGraphAssets, ScriptMachines or a ClassAsset that inherits MonoBehaviour",  "Could not generate Timer", ""));
            }
    
            var output = string.Empty;
    
            if (input == Unit.start)
            {
                var action = GetAction(Unit.started, indent, data);
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + ".StartTimer(") + GenerateValue(Unit.duration, data) + MakeClickableForThisUnit(", ") + GenerateValue(Unit.unscaledTime, data) + (!string.IsNullOrEmpty(action) ? MakeClickableForThisUnit(", ") + action : "") + MakeClickableForThisUnit(");") + "\n";
            }
            else if (input == Unit.pause)
            {
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + ".PauseTimer();") + "\n";
            }
            else if (input == Unit.resume)
            {
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + ".ResumeTimer();") + "\n";
            }
            else if (input == Unit.toggle)
            {
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + ".ToggleTimer();") + "\n";
            }
    
            if (Unit.tick.hasValidConnection && !data.generatorData.TryGetValue(Unit.tick, out var tickGenerated))
            {
                data.generatorData.Add(Unit.tick, true);
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "OnTick".VariableHighlight() + " += ") + GetAction(Unit.tick, indent, data) + MakeClickableForThisUnit(";") + "\n";
            }
    
            if (Unit.completed.hasValidConnection && !data.generatorData.TryGetValue(Unit.completed, out var completedGenerated))
            {
                data.generatorData.Add(Unit.completed, true);
                output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "OnCompleted".VariableHighlight() + " += ") + GetAction(Unit.completed, indent, data) + MakeClickableForThisUnit(";") + "\n";
            }
    
            return output;
        }
    
        private string GetAction(ControlOutput controlOutput, int indent, ControlGenerationData data)
        {
            if (!controlOutput.hasValidConnection)
                return "";
            var output = "";
            data.NewScope();
            data.SetReturns(typeof(void));
            output += MakeClickableForThisUnit("() =>") + "\n";
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("{") + "\n";
            output += GetNextUnit(controlOutput, data, indent + 1);
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("}");
            data.ExitScope();
            return output;
        }
    
        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            if (output == Unit.elapsedSeconds)
            {
                return MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "Elapsed".VariableHighlight());
            }
            else if (output == Unit.elapsedRatio)
            {
                return MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "ElapsedPercentage".VariableHighlight());
            }
            else if (output == Unit.remainingSeconds)
            {
                return MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "Remaining".VariableHighlight());
            }
            else if (output == Unit.remainingRatio)
            {
                return MakeClickableForThisUnit(variableName.VariableHighlight() + "." + "RemainingPercentage".VariableHighlight());
            }
            return base.GenerateValue(output, data);
        }
    } 
}