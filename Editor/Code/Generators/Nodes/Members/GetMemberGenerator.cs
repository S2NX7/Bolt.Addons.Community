using Unity;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using System.Linq;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Unity.VisualScripting.GetMember))]
    public sealed class GetMemberGenerator : NodeGenerator<Unity.VisualScripting.GetMember>
    {
        public GetMemberGenerator(Unity.VisualScripting.GetMember unit) : base(unit)
        {
            NameSpaces = Unit.member.declaringType.Namespace;
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            if (Unit.target != null)
            {
                if (Unit.target.hasValidConnection)
                {
                    string name;

                    if (Unit.member.isField)
                    {
                        name = Unit.member.fieldInfo.Name;
                    }
                    else if (Unit.member.isProperty)
                    {
                        name = Unit.member.name;
                    }
                    else
                    {
                        name = Unit.member.ToPseudoDeclarer().ToString(); // I don't think this should be possible.
                    }

                    var outputCode = Unit.CreateClickableString();

                    if (typeof(Component).IsAssignableFrom(Unit.member.pseudoDeclaringType))
                    {
                        outputCode.Ignore(GenerateValue(Unit.target, data)).Clickable(GetComponent(Unit.target, data)).Dot().Clickable(name.VariableHighlight());
                    }
                    else
                    {
                        outputCode.Ignore(GenerateValue(Unit.target, data)).Dot().Clickable(name.VariableHighlight());
                    }

                    return outputCode;
                }
                else
                {
                    return $"{GenerateValue(Unit.target, data)}{MakeClickableForThisUnit($".{Unit.member.name.VariableHighlight()}")}";
                }
            }
            else
            {
                return MakeClickableForThisUnit($"{Unit.member.targetType.As().CSharpName(false, true)}.{Unit.member.name.VariableHighlight()}");
            }
        }


        public override string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (Unit.target != null)
            {
                if (input == Unit.target)
                {
                    if (Unit.target.hasValidConnection)
                    {
                        data.SetExpectedType(Unit.member.pseudoDeclaringType);
                        var connectedCode = GetNextValueUnit(input, data);
                        data.RemoveExpectedType();
                        if (Unit.member.pseudoDeclaringType.IsSubclassOf(typeof(Component)))
                        {
                            return Unit.CreateIgnoreString(connectedCode).EndIgnoreContext().Cast(typeof(GameObject), ShouldCast(input, data));
                        }
                        return Unit.CreateIgnoreString(connectedCode).EndIgnoreContext().Cast(input.type, ShouldCast(input, data));
                    }
                    else if (Unit.target.hasDefaultValue)
                    {
                        if (input.type == typeof(GameObject) || input.type.IsSubclassOf(typeof(Component)) || input.type == typeof(Component))
                        {
                            return MakeClickableForThisUnit("gameObject".VariableHighlight() + GetComponent(Unit.target, data));
                        }
                        return Unit.defaultValues[input.key].As().Code(true, Unit, true, true, "", false, true);
                    }
                    else
                    {
                        return base.GenerateValue(input, data);
                    }
                }
            }

            return base.GenerateValue(input, data);
        }

        string GetComponent(ValueInput valueInput, ControlGenerationData data)
        {
            if (valueInput.hasValidConnection)
            {
                if (valueInput.type == valueInput.connection.source.type && valueInput.connection.source.unit is MemberUnit or InheritedMemberUnit or AssetFieldUnit or AssetMethodCallUnit)
                {
                    return "";
                }
                else
                {
                    return ((valueInput.connection.source.unit is MemberUnit memberUnit && memberUnit.member.name != "GetComponent") || GetSourceType(valueInput, data) == typeof(GameObject)) && Unit.member.pseudoDeclaringType != typeof(GameObject) ? $".GetComponent<{Unit.member.pseudoDeclaringType.As().CSharpName(false, true)}>()" : string.Empty;
                }
            }
            else
            {
                if (Unit.member.pseudoDeclaringType != typeof(GameObject))
                    return $".GetComponent<{Unit.member.pseudoDeclaringType.As().CSharpName(false, true)}>()";
                else
                    return "";
            }
        }

    }
}