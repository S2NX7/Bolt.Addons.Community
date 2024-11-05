﻿using Unity.VisualScripting;
using Unity.VisualScripting.Community.Libraries.Humility;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Equal))]
    public sealed class EqualGenerator : NodeGenerator<Equal>
    {
        public EqualGenerator(Equal unit) : base(unit)
        {
        }

        public override string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input == Unit.a)
            {
                if (Unit.a.hasAnyConnection)
                {
                    if (Unit.b.hasValidConnection && Unit.b.GetPsudoSource()?.unit is Literal literal)
                    {
                        data.SetExpectedType(literal.type);
                    }
                    var code = base.GenerateValue(Unit.a, data);
                    if (Unit.b.hasValidConnection && Unit.b.GetPsudoSource()?.unit is Literal)
                    {
                        data.RemoveExpectedType();
                    }
                    return code;
                }
            }

            if (input == Unit.b)
            {
                if (Unit.b.hasAnyConnection)
                {
                    if (Unit.a.hasValidConnection && Unit.a.GetPsudoSource()?.unit is Literal literal)
                    {
                        data.SetExpectedType(literal.type);
                    }
                    var code = base.GenerateValue(Unit.b, data);
                    if (Unit.a.hasValidConnection && Unit.a.GetPsudoSource()?.unit is Literal)
                    {
                        data.RemoveExpectedType();
                    }
                    return code;
                }
                else
                {
                    return Unit.numeric ? Unit.defaultValues["b"].As().Code(true, unit) : base.GenerateValue(input, data);
                }
            }

            return base.GenerateValue(input, data);
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {

            if (output == Unit.comparison)
            {
                return GenerateValue(Unit.a, data) + MakeSelectableForThisUnit(" == ") + GenerateValue(Unit.b, data);
            }

            return base.GenerateValue(output, data);
        }
    }
}