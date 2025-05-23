
using System;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Text;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(BranchParams))]
    public class BranchParamsGenerator : NodeGenerator<BranchParams>
    {
        public BranchParamsGenerator(Unit unit) : base(unit) { }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            return base.GenerateValue(output, data);
        }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            var output = new StringBuilder();
            string cachedIndent = CodeBuilder.Indent(indent);

            var trueData = new ControlGenerationData(data);
            var falseData = new ControlGenerationData(data);
            string trueCode;

            if (input == Unit.enter)
            {
                // Construct "if" statement
                output.Append(cachedIndent)
                      .Append(MakeSelectableForThisUnit("if".ConstructHighlight() + " ("))
                      .Append(GenerateArguments(data))
                      .Append(MakeSelectableForThisUnit(")"))
                      .AppendLine()
                      .Append(cachedIndent + MakeSelectableForThisUnit("{"))
                      .AppendLine();

                trueData.NewScope();
                trueCode = GetNextUnit(Unit.exitTrue, data, indent + 1);
                trueData.ExitScope();

                output.Append(trueCode).AppendLine();

                output.Append(cachedIndent + MakeSelectableForThisUnit("}"));

                // Handle the "else" branch if present
                if (Unit.exitFalse.hasAnyConnection)
                {
                    output.AppendLine().Append(cachedIndent).Append(MakeSelectableForThisUnit("else".ConstructHighlight()));

                    if (!Unit.exitTrue.hasValidConnection || string.IsNullOrEmpty(trueCode))
                    {
                        output.Append(CodeBuilder.MakeRecommendation(
                            "You should use the negate node and connect the true input instead"));
                    }

                    output.AppendLine()
                          .Append(cachedIndent + MakeSelectableForThisUnit("{"))
                          .AppendLine();

                    falseData.NewScope();
                    output.Append(GetNextUnit(Unit.exitFalse, data, indent + 1)).AppendLine();
                    falseData.ExitScope();

                    output.Append(cachedIndent + MakeSelectableForThisUnit("}"));
                }

                // Handle the "finished" branch if present
                if (Unit.exitNext != null && Unit.exitNext.hasAnyConnection)
                {
                    output.AppendLine().Append(GetNextUnit(Unit.exitNext, data, indent));
                }
            }

            // Update break status in data
            data.hasBroke = trueData.hasBroke && falseData.hasBroke;

            return output.ToString();
        }

        private string GenerateArguments(ControlGenerationData data)
        {
            var op = " && ";
            List<string> values = new List<string>();
            switch (Unit.BranchingType)
            {
                case LogicParamNode.BranchType.And:
                    op = " && ";
                    data.SetExpectedType(typeof(bool));
                    foreach (var arg in Unit.arguments)
                    {
                        values.Add(GenerateValue(arg, data));
                    }
                    data.RemoveExpectedType();
                    break;
                case LogicParamNode.BranchType.Or:
                    op = " || ";
                    data.SetExpectedType(typeof(bool));
                    foreach (var arg in Unit.arguments)
                    {
                        values.Add(GenerateValue(arg, data));
                    }
                    data.RemoveExpectedType();
                    break;
                case LogicParamNode.BranchType.GreaterThan:
                    op = Unit.AllowEquals ? " >= " : " > ";
                    data.SetExpectedType(typeof(float));
                    foreach (var arg in Unit.arguments)
                    {
                        values.Add(GenerateValue(arg, data));
                    }
                    data.RemoveExpectedType();
                    break;
                case LogicParamNode.BranchType.LessThan:
                    op = Unit.AllowEquals ? " <= " : " > ";
                    data.SetExpectedType(typeof(float));
                    foreach (var arg in Unit.arguments)
                    {
                        values.Add(GenerateValue(arg, data));
                    }
                    data.RemoveExpectedType();
                    break;
                case LogicParamNode.BranchType.Equal:
                    op = " == ";
                    if (Unit.Numeric)
                        data.SetExpectedType(typeof(float));
                    foreach (var arg in Unit.arguments)
                    {
                        values.Add(GenerateValue(arg, data));
                    }
                    if (Unit.Numeric)
                        data.RemoveExpectedType();
                    break;
            }
            return string.Join(MakeSelectableForThisUnit(op), values);
        }
    }
}
