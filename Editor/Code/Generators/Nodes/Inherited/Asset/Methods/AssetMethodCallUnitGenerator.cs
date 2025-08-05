using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using UnityEngine;

namespace Unity.VisualScripting.Community
{

    [NodeGenerator(typeof(AssetMethodCallUnit))]
    public class AssetMethodCallUnitGenerator : NodeGenerator<AssetMethodCallUnit>
    {
        private ControlGenerationData controlGenerationData;

        private Dictionary<ValueOutput, string> outputNames;
        public AssetMethodCallUnitGenerator(Unit unit) : base(unit)
        {
        }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            var output = string.Empty;
            controlGenerationData = data;
            output += MakeClickableForThisUnit(Unit.method.methodName + "(") + GenerateArguments(Unit.InputParameters.Values.ToList(), data) + MakeClickableForThisUnit(");");
            output += "\n" + GetNextUnit(Unit.exit, data, indent);
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

            return MakeClickableForThisUnit(Unit.method.methodName + "(") + GenerateArguments(Unit.InputParameters.Values.ToList(), data) + MakeClickableForThisUnit(")");
        }

        private string GenerateArguments(List<ValueInput> arguments, ControlGenerationData data)
        {
            if (data != null)
            {
                var output = new List<string>();
                var parameters = Unit.method.parameters;
                int paramCount = parameters.Count;

                for (int i = 0; i < paramCount; i++)
                {
                    var parameter = parameters[i];
                    var name = data.AddLocalNameInScope(parameter.name, parameter.type).VariableHighlight();

                    if (parameter.modifier == ParameterModifier.Out)
                    {
                        output.Add(MakeClickableForThisUnit("out var ".ConstructHighlight() + name));

                        if (Unit.OutputParameters.Values.Any(o => o.key == "&" + parameter.name && !outputNames.ContainsKey(Unit.OutputParameters[i])))
                            outputNames.Add(Unit.OutputParameters[i], "&" + name);

                        continue;
                    }

                    if (parameter.modifier == ParameterModifier.Ref)
                    {
                        var input = Unit.InputParameters.Values.FirstOrDefault(v => v.key == "%" + parameter.name);

                        if (input == null || !input.hasValidConnection || (input.hasValidConnection && !input.connection.source.unit.IsValidRefUnit()))
                        {
                            output.Add(MakeClickableForThisUnit($"/* {parameter.name} needs to be connected to a variable unit or a get member unit */".WarningHighlight()));
                            continue;
                        }

                        output.Add(MakeClickableForThisUnit("ref ".ConstructHighlight()) + GenerateValue(input, data));

                        if (Unit.OutputParameters.Values.Any(o => o.key == "&" + parameter.name && !outputNames.ContainsKey(Unit.OutputParameters[i])))
                            outputNames.Add(Unit.OutputParameters[i], "&" + name);

                        continue;
                    }

                    var inputAtIndex = (i < Unit.InputParameters.Count) ? Unit.InputParameters[i] : null;
                    if (parameter.hasDefault && (inputAtIndex == null || (!inputAtIndex.hasValidConnection && !inputAtIndex.hasDefaultValue)))
                    {
                        bool hasLaterConnection = false;
                        for (int j = i + 1; j < paramCount; j++)
                        {
                            var laterInput = (j < Unit.InputParameters.Count) ? Unit.InputParameters[j] : null;
                            if (laterInput != null && (laterInput.hasValidConnection || laterInput.hasDefaultValue))
                            {
                                hasLaterConnection = true;
                                break;
                            }
                        }

                        if (!hasLaterConnection)
                            continue;
                    }

                    if (inputAtIndex != null)
                        output.Add(GenerateValue(inputAtIndex, data));
                    else if (i < arguments.Count)
                        output.Add(GenerateValue(arguments[i], data));
                }

                return string.Join(MakeClickableForThisUnit(", "), output);
            }
            else
            {
                var output = arguments.Select(arg => GenerateValue(arg, data)).ToList();
                return string.Join(MakeClickableForThisUnit(", "), output);
            }
        }
    }
}