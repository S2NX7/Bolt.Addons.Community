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
                    return (Unit.a.connection.source.unit as Unit).GenerateValue(Unit.a.connection.source, data);
                }
            }

            if (input == Unit.b)
            {
                if (Unit.b.hasAnyConnection)
                {
                    return (Unit.b.connection.source.unit as Unit).GenerateValue(Unit.b.connection.source, data);
                }
                else
                {
                    return Unit.numeric ? Unit.defaultValues["b"].As().Code(true) : base.GenerateValue(input, data);
                }
            }

            return base.GenerateValue(input, data);
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            if (output == Unit.comparison)
            {
                return GenerateValue(Unit.a, data) + " == " + GenerateValue(Unit.b, data);
            }

            return base.GenerateValue(output, data);
        }
    }
}