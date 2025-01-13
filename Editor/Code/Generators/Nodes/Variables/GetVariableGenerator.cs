using System;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(GetVariable))]
    public class GetVariableGenerator : LocalVariableGenerator
    {
        private GetVariable Unit => unit as GetVariable;
        public GetVariableGenerator(Unit unit) : base(unit)
        {
            SetNamespaceBasedOnVariableKind();
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            SetNamespaceBasedOnVariableKind();

            if (Unit.name.hasValidConnection || Unit.kind == VariableKind.Object)
            {
                if (Unit.kind == VariableKind.Object && !Unit.name.hasValidConnection)
                {
                    return GenerateDisconnectedVariableCode(data);
                }
                return GenerateConnectedVariableCode(data);
            }
            else
            {
                return GenerateDisconnectedVariableCode(data);
            }
        }

        private void SetNamespaceBasedOnVariableKind()
        {
            NameSpaces = Unit.kind == VariableKind.Scene ? "UnityEngine.SceneManagement" : string.Empty;
        }

        private string GenerateConnectedVariableCode(ControlGenerationData data)
        {
            variableType = GetVariableType("", data, false);
            if (data.GetExpectedType() == variableType)
                data.SetCurrentExpectedTypeMet(true, variableType);
            else
                data.SetCurrentExpectedTypeMet(false, variableType);
            var typeString = variableType != null ? $"<{variableType.As().CSharpName(false, true)}>" : string.Empty;
            var kind = GetKind(data, typeString);

            data.CreateSymbol(Unit, variableType ?? typeof(object), $"{kind}.Get{typeString}({GenerateValue(Unit.name, data)})");
            return kind + $"{GenerateValue(Unit.name, data)}{MakeSelectableForThisUnit(")")}";
        }

        private string GetKind(ControlGenerationData data, string typeString)
        {
            var variables = typeof(VisualScripting.Variables).As().CSharpName(true, true);
            return Unit.kind switch
            {
                VariableKind.Object => MakeSelectableForThisUnit(variables + $".Object(") + $"{GenerateValue(Unit.@object, data)}{MakeSelectableForThisUnit($").Get{typeString}(")}",
                VariableKind.Scene => MakeSelectableForThisUnit(variables + $"." + "ActiveScene".VariableHighlight() + $".Get{typeString}("),
                VariableKind.Application => MakeSelectableForThisUnit(variables + "." + "Application".VariableHighlight() + $".Get{typeString}("),
                VariableKind.Saved => MakeSelectableForThisUnit(variables + "." + "Saved".VariableHighlight()) + $".Get{typeString}(",
                _ => string.Empty,
            };
        }

        private string GenerateDisconnectedVariableCode(ControlGenerationData data)
        {
            var name = Unit.defaultValues[Unit.name.key] as string;
            variableType = GetVariableType(name, data, true);
            if (Unit.kind == VariableKind.Object || Unit.kind == VariableKind.Scene || Unit.kind == VariableKind.Application || Unit.kind == VariableKind.Saved)
            {
                var typeString = variableType != null ? $"<{variableType.As().CSharpName(false, true)}>" : string.Empty;
                var isExpectedType = data.GetExpectedType() == null || (variableType != null && data.GetExpectedType().IsAssignableFrom(variableType)) || (IsVariableDefined(name) && !string.IsNullOrEmpty(GetVariableDeclaration(name).typeHandle.Identification) && Type.GetType(GetVariableDeclaration(name).typeHandle.Identification) == data.GetExpectedType()) || (data.TryGetVariableType(data.GetVariableName(name), out Type targetType) && targetType == data.GetExpectedType());
                data.SetCurrentExpectedTypeMet(isExpectedType, variableType);
                var code = GetKind(data, typeString) + $"{GenerateValue(Unit.name, data)}{MakeSelectableForThisUnit(")")}";
                return new ValueCode(code, variableType, !isExpectedType);
            }
            else
            {
                data.CreateSymbol(Unit, variableType, name);
                return MakeSelectableForThisUnit(data.GetVariableName(name).VariableHighlight());
            }
        }

        private Type GetVariableType(string name, ControlGenerationData data, bool checkDecleration)
        {
            if (checkDecleration)
            {
                var isDefined = IsVariableDefined(name);
                if (isDefined)
                {
                    var declaration = GetVariableDeclaration(name);
                    return declaration.typeHandle.Identification != null ? Type.GetType(declaration.typeHandle.Identification) : null;
                }
            }
            return data.TryGetVariableType(data.GetVariableName(name), out Type targetType) ? targetType : data.GetExpectedType() ?? typeof(object);
        }

        private bool IsVariableDefined(string name)
        {
            var objectDefined = Unit.kind == VariableKind.Object && !Unit.@object.hasValidConnection && Selection.activeGameObject != null && VisualScripting.Variables.Object(Selection.activeGameObject).IsDefined(name);
            return Unit.kind switch
            {
                VariableKind.Object => objectDefined,
                VariableKind.Scene => VisualScripting.Variables.ActiveScene.IsDefined(name),
                VariableKind.Application => VisualScripting.Variables.Application.IsDefined(name),
                VariableKind.Saved => VisualScripting.Variables.Saved.IsDefined(name),
                _ => false,
            };
        }

        private VariableDeclaration GetVariableDeclaration(string name)
        {
            var objectDeclaration = Unit.kind == VariableKind.Object && !Unit.@object.hasValidConnection && Selection.activeGameObject != null ? VisualScripting.Variables.Object(Selection.activeGameObject).GetDeclaration(name) : null;
            return Unit.kind switch
            {
                VariableKind.Object => objectDeclaration,
                VariableKind.Scene => VisualScripting.Variables.ActiveScene.GetDeclaration(name),
                VariableKind.Application => VisualScripting.Variables.Application.GetDeclaration(name),
                VariableKind.Saved => VisualScripting.Variables.Saved.GetDeclaration(name),
                _ => null,
            };
        }

        public override string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input == Unit.@object && !input.hasValidConnection)
            {
                return "gameObject".VariableHighlight();
            }

            if (input == Unit.@object)
            {
                data.SetExpectedType(typeof(GameObject));
                var code = base.GenerateValue(input, data);
                data.RemoveExpectedType();
                return code;
            }
            return base.GenerateValue(input, data);
        }
    }
}