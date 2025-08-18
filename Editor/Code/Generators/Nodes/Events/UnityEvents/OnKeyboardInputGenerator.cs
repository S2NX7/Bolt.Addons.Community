using System;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Unity.VisualScripting.Community.Utility;
using System.Collections;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(OnKeyboardInput))]
    public class OnKeyboardInputGenerator : MethodNodeGenerator
    {
        private OnKeyboardInput Unit => unit as OnKeyboardInput;
        public override ControlOutput OutputPort => Unit.trigger;

        public override List<ValueOutput> OutputValues => new();

        public override AccessModifier AccessModifier => AccessModifier.Private;

        public override MethodModifier MethodModifier => MethodModifier.None;

        public override string Name => "OnKeyboardInput" + count;

        public override Type ReturnType => Unit.coroutine ? typeof(IEnumerator) : typeof(void);

        public override List<TypeParam> Parameters => new();

        public override string MethodBody => GetNextUnit(OutputPort, Data, indent);

        public OnKeyboardInputGenerator(Unit unit) : base(unit) { }
        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            return base.GenerateValue(output, data);
        }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            string output = string.Empty;
            if (!typeof(MonoBehaviour).IsAssignableFrom(data.ScriptType))
            {
                return CodeBuilder.Indent(indent + 1) + MakeClickableForThisUnit(CodeUtility.ToolTip("OnKeyboardInput only works with ScriptGraphAssets, ScriptMachines or a ClassAsset that inherits MonoBehaviour", "Could not generate OnKeyboardInput", ""));
            }
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("if ".ControlHighlight() + "(") + CodeBuilder.CallCSharpUtilityMethod(Unit, MakeClickableForThisUnit("GetKeyAction"), GenerateValue(Unit.key, data), GenerateValue(Unit.action, data)) + MakeClickableForThisUnit(")") + "\n";
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("{") + "\n";
            output += CodeBuilder.Indent(indent + 1) + MakeClickableForThisUnit((Unit.coroutine ? $"StartCoroutine({Name}())" : Name + "()") + ";") + "\n";
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("}") + "\n";
            return output;
        }
    }
}