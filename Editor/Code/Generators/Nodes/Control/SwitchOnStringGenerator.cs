﻿using Unity.VisualScripting;
using System.Linq;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Libraries.Humility;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(SwitchOnString))]
    public sealed class SwitchOnStringGenerator : NodeGenerator<SwitchOnString>
    {
        public SwitchOnStringGenerator(SwitchOnString unit) : base(unit)
        {
        }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            var output = string.Empty;

            if (input == Unit.enter)
            {
                var values = Unit.branches;
                var outputs = Unit.outputs.ToArray();
                var isLiteral = Unit.selector.hasValidConnection && Unit.selector.connection.source.unit is Literal;
                var localName = string.Empty;
                if (isLiteral) localName = data.AddLocalNameInScope("str", typeof(string));
                var newLiteral = isLiteral ? CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("var ".ConstructHighlight() + $"{localName} = ") + ((Unit)Unit.selector.connection.source.unit).GenerateValue(Unit.selector.connection.source, data) + MakeSelectableForThisUnit(";") : string.Empty;
                var @enum = Unit.selector.hasValidConnection ? isLiteral ? MakeSelectableForThisUnit(localName) : ((Unit)Unit.selector.connection.source.unit).GenerateValue(Unit.selector.connection.source, data) : base.GenerateControl(input, data, indent);

                if (isLiteral) output += MakeSelectableForThisUnit(newLiteral) + "\n";
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("switch".ConstructHighlight() + $" ({@enum})");
                output += "\n";
                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("{");
                output += "\n";

                for (int i = 0; i < values.Count; i++)
                {
                    var _connection = ((ControlOutput)outputs[i])?.connection;

                    output += CodeBuilder.Indent(indent + 1) + MakeSelectableForThisUnit("case ".ConstructHighlight() + $@" ""{values[i].Key}""".StringHighlight() + ":");
                    output += "\n";

                    var _controlData = new ControlGenerationData(data);
                    _controlData.mustBreak = _controlData.returns == typeof(Void);
                    _controlData.mustReturn = !_controlData.mustBreak;

                    if (((ControlOutput)outputs[i]).hasValidConnection)
                    {
                        _controlData.NewScope();
                        output += ((Unit)_connection.destination.unit).GenerateControl(_connection.destination as ControlInput, _controlData, indent + 2);
                        output += "\n";
                        _controlData.ExitScope();
                    }

                    if (_controlData.mustBreak && !_controlData.hasBroke) output += CodeBuilder.Indent(indent + 2) + MakeSelectableForThisUnit("break".ControlHighlight() + ";\n");
                    if (_controlData.mustReturn && !_controlData.hasReturned) output += CodeBuilder.Indent(indent + 2) + MakeSelectableForThisUnit("break".ControlHighlight() + ";\n");
                }

                var connection = Unit.@default.connection;

                output += CodeBuilder.Indent(indent + 1) + MakeSelectableForThisUnit("default ".ConstructHighlight() + ":");
                output += "\n";

                var controlData = new ControlGenerationData(data);
                controlData.mustBreak = controlData.returns == typeof(Void);
                controlData.mustReturn = !controlData.mustBreak;

                if (Unit.@default.hasValidConnection)
                {
                    controlData.NewScope();
                    output += ((Unit)connection.destination.unit).GenerateControl(connection.destination as ControlInput, controlData, indent + 2);
                    output += "\n";
                    controlData.ExitScope();
                }

                if (controlData.mustBreak && !controlData.hasBroke) output += CodeBuilder.Indent(indent + 2) + MakeSelectableForThisUnit("break".ControlHighlight() + ";\n");
                if (controlData.mustReturn && !controlData.hasReturned) output += CodeBuilder.Indent(indent + 2) + MakeSelectableForThisUnit("break".ControlHighlight() + ";\n");

                output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("}");
                output += "\n";

                return output;
            }

            return base.GenerateControl(input, data, indent);
        }

        public override string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input == Unit.selector)
            {
                return ShouldCast(input, data) ? $"(({typeof(string).As().CSharpName(false, true)}){GetNextValueUnit(input, data)})" : GetNextValueUnit(input, data);
            }

            return base.GenerateValue(input, data);
        }

    }
}
