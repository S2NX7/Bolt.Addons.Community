using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Utility;
using UnityEngine;
using System.Text;
using UnityEngine.InputSystem;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEditor;

#if PACKAGE_INPUT_SYSTEM_EXISTS
using Unity.VisualScripting.InputSystem;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(OnInputSystemEventButton))]
    public sealed class OnInputSystemEventButtonGenerator : MethodNodeGenerator
    {
        public OnInputSystemEventButtonGenerator(Unit unit) : base(unit)
        {
        }
        private OnInputSystemEventButton Unit => unit as OnInputSystemEventButton;
        public override ControlOutput OutputPort => Unit.trigger;

        public override List<ValueOutput> OutputValues => new List<ValueOutput>();

        public override AccessModifier AccessModifier => AccessModifier.None;

        public override MethodModifier MethodModifier => MethodModifier.None;

        public override string Name => "OnInputSystemEventButton" + count;

        public override Type ReturnType => typeof(void);

        public override List<TypeParam> Parameters => new List<TypeParam>();

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            if (!typeof(MonoBehaviour).IsAssignableFrom(data.ScriptType))
            {
                return CodeBuilder.Indent(indent + 1) + MakeSelectableForThisUnit(CodeUtility.ToolTip("OnInputSystemEvents only work with ScriptGraphAssets, ScriptMachines or a ClassAsset that inherits MonoBehaviour", "Could not generate OnInputSystemEvent", ""));
            }
            var output = new StringBuilder();
            var inputVariable = data.AddLocalNameInScope("playerInput", typeof(PlayerInput));
            var actionVariable = data.AddLocalNameInScope("action", typeof(InputAction));
            output.Append(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("var ".ConstructHighlight() + inputVariable.VariableHighlight() + " = ") + GenerateValue(Unit.Target, data) + MakeSelectableForThisUnit(";") + "\n");
            output.Append(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("var ".ConstructHighlight() + actionVariable.VariableHighlight() + " = " + inputVariable.VariableHighlight() + "." + "actions".VariableHighlight() + $".FindAction(") + GenerateValue(Unit.InputAction, data) + MakeSelectableForThisUnit(");") + "\n");
            output.Append(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("if".ControlHighlight() + " (" + GetState(actionVariable.VariableHighlight()) + ")"));
            output.AppendLine();
            output.AppendLine(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("{"));
            output.Append(GetNextUnit(Unit.trigger, data, indent + 1));
            output.AppendLine(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("}"));
#if !PACKAGE_INPUT_SYSTEM_1_2_0_OR_NEWER_EXISTS
            output.AppendLine(CodeBuilder.Indent(indent) + MakeSelectableForThisUnit($"{$"button{count}_wasRunning".VariableHighlight()} = {actionVariable.VariableHighlight()}.{"phase".VariableHighlight()} == {InputActionPhase.Started.As().Code(false)};"));
#endif
            return output.ToString();
        }

        private string GetState(string actionVariable)
        {
#if PACKAGE_INPUT_SYSTEM_1_2_0_OR_NEWER_EXISTS
            switch (Unit.InputActionChangeType)
            {
                case InputActionChangeOption.OnPressed:
                    return actionVariable + ".WasPressedThisFrame()";
                case InputActionChangeOption.OnHold:
                    return actionVariable + ".IsPressed()";
                case InputActionChangeOption.OnReleased:
                    return actionVariable + ".WasReleasedThisFrame()";
                default:
                    throw new ArgumentOutOfRangeException();
            }
#else
            // I really don't like this it feels very hacky and requires implementations for each CodeGenerator
            switch (Unit.InputActionChangeType)
            {
                case InputActionChangeOption.OnPressed:
                    return actionVariable + $".{triggered.VariableHighlight()}";
                case InputActionChangeOption.OnHold:
                    return actionVariable + $".{phase.VariableHighlight()} == {InputActionPhase.Started.As().Code(false)}";
                case InputActionChangeOption.OnReleased:
                    return $"{$"button{count}_wasRunning".VariableHighlight()}" + " && " + actionVariable + $".{phase.VariableHighlight()} != {InputActionPhase.Started.As().Code(false)}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
#endif
        }

        public override string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input == Unit.Target)
            {
                if (Unit.Target.hasValidConnection)
                {
                    data.SetExpectedType(input.type);
                    var code = GetNextValueUnit(input, data);
                    data.RemoveExpectedType();
                    return code;
                }
                else
                {
                    var value = input.unit.defaultValues[input.key];
                    if (value == null)
                    {
                        return MakeSelectableForThisUnit("gameObject".VariableHighlight() + "." + $"GetComponent<{typeof(PlayerInput).As().CSharpName(false, true, true)}>()");
                    }
                    else
                    {
                        return base.GenerateValue(input, data);
                    }
                }
            }
            else if (input == Unit.InputAction)
            {
                if (Unit.InputAction.hasValidConnection)
                {
                    data.SetExpectedType(input.type);
                    var code = GetNextValueUnit(input, data);
                    data.RemoveExpectedType();
                    return code;
                }
                else
                {
                    if (input.unit.defaultValues[input.key] is not InputAction value)
                    {
                        return MakeSelectableForThisUnit(CodeUtility.ToolTip("The problem could be that the player input component could not be found.", "Could not generate Input Action", "null".ConstructHighlight()));
                    }
                    else
                    {
                        return MakeSelectableForThisUnit(value.name.As().Code(false));
                    }
                }
            }
            return base.GenerateValue(input, data);
        }
    }
}
#endif