using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using UnityEngine;

namespace Unity.VisualScripting.Community
{

    [NodeGenerator(typeof(BaseMethodCall))]
    public class BaseMethodCallGenerator : NodeGenerator<BaseMethodCall>
    {
        private Dictionary<ValueOutput, string> outputNames;
        public BaseMethodCallGenerator(Unit unit) : base(unit)
        {
        }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            var output = string.Empty;
            output += CodeBuilder.Indent(indent) + MakeClickableForThisUnit("base".ConstructHighlight() + "." + Unit.member.name + "(") + GenerateArguments(data) + MakeClickableForThisUnit(");") + (Unit.exit.hasValidConnection ? "\n" : string.Empty);
            output += GetNextUnit(Unit.exit, data, indent);
            return output;
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {

            if (Unit.enter != null && !Unit.enter.hasValidConnection && Unit.OutputParameters.Count > 0)
            {
                return MakeClickableForThisUnit($"/* Control Port Enter requires a connection */".WarningHighlight());
            }

            if (Unit.OutputParameters.ContainsValue(output))
            {
                var transformedKey = outputNames[output].Replace("&", "").Replace("%", "");

                return MakeClickableForThisUnit(transformedKey.VariableHighlight());
            }

            return MakeClickableForThisUnit("base".ConstructHighlight() + "." + Unit.member.name + "(") + GenerateArguments(data) + MakeClickableForThisUnit(")");
        }


        private string GenerateArguments(ControlGenerationData data)
        {
            if (data != null && Unit.member.isMethod)
            {
                var output = new List<string>();
                var parameters = Unit.member.methodInfo.GetParameters();
                int paramCount = parameters.Length;

                for (int i = 0; i < paramCount; i++)
                {
                    var parameter = parameters[i];
                    var input = Unit.InputParameters[i];

                    if (parameter.HasOutModifier())
                    {
                        var name = data.AddLocalNameInScope(parameter.Name, parameter.ParameterType).VariableHighlight();
                        output.Add(MakeClickableForThisUnit("out var ".ConstructHighlight() + name));

                        if (Unit.OutputParameters.Values.Any(o => o.key == "&" + parameter.Name && !outputNames.ContainsKey(Unit.OutputParameters[i])))
                            outputNames.Add(Unit.OutputParameters[i], "&" + name);

                        continue;
                    }

                    if (parameter.ParameterType.IsByRef)
                    {
                        if (!input.hasValidConnection || (input.hasValidConnection && !input.connection.source.unit.IsValidRefUnit()))
                        {
                            output.Add(MakeClickableForThisUnit($"/* {input.key.Replace("%", "")} needs to be connected to a variable unit or a get member unit */".WarningHighlight()));
                            continue;
                        }

                        output.Add(MakeClickableForThisUnit("ref ".ConstructHighlight()) + GenerateValue(input, data));
                        outputNames.Add(Unit.OutputParameters[i], "&" + parameter.Name.VariableHighlight());
                        continue;
                    }

                    if (parameter.IsDefined(typeof(ParamArrayAttribute), false) && !input.hasValidConnection)
                        continue;

                    if (parameter.IsOptional && !input.hasValidConnection && !input.hasDefaultValue)
                    {
                        bool hasLaterConnection = false;

                        for (int j = i + 1; j < paramCount; j++)
                        {
                            var laterParam = Unit.InputParameters[j];
                            if (laterParam != null && (laterParam.hasValidConnection || laterParam.hasDefaultValue))
                            {
                                hasLaterConnection = true;
                                break;
                            }
                        }

                        if (!hasLaterConnection)
                            continue;
                    }

                    output.Add(GenerateValue(input, data));
                }

                return string.Join(MakeClickableForThisUnit(", "), output);
            }

            return string.Join(MakeClickableForThisUnit(", "), Unit.valueInputs.Select(input => GenerateValue(input, data)));
        }
    }
}