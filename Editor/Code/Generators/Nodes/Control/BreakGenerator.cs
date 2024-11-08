﻿using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Break))]
    public sealed class BreakGenerator : NodeGenerator<Break>
    {
        public BreakGenerator(Break unit) : base(unit)
        {
        }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            data.hasBroke = true;

            if (input == Unit.enter)
            {
                return CodeBuilder.Indent(indent) + MakeSelectableForThisUnit("break".ControlHighlight() + ";") + "\n";
            }

            return base.GenerateControl(input, data, indent);
        }
    }
}