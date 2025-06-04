using System;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Cache))]
    public class CacheGenerator : VariableNodeGenerator
    {
        private Cache Unit => unit as Cache;
        public override AccessModifier AccessModifier => AccessModifier.Private;

        public override FieldModifier FieldModifier => FieldModifier.None;

        public override string Name => "Cache" + count;

        public override Type Type => type;

        public override object DefaultValue => throw new NotImplementedException();

        public override bool HasDefaultValue => false;

        private Type type = typeof(object);
        public CacheGenerator(Unit unit) : base(unit) { }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            return Name.VariableHighlight();
        }

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            var output = string.Empty;
            output += CodeBuilder.Indent(indent) + MakeSelectableForThisUnit(Name.VariableHighlight() + " = ") + GenerateValue(Unit.input, data) + MakeSelectableForThisUnit(";") + "\n";
            output += GetNextUnit(Unit.exit, data, indent);
            var sourceType = GetSourceType(Unit.input, data);
            if (sourceType != type && sourceType != null)
            {
                type = sourceType;
            }
            else if (type == null)
            {
                type = typeof(object);
            }
            return output;
        }
    }
}
