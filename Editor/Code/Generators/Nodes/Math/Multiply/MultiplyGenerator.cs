using Unity.VisualScripting;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    public abstract class MultiplyGenerator<T> : NodeGenerator<Multiply<T>>
    {
        public MultiplyGenerator(Unit unit) : base(unit) { }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            var a = GenerateValue(Unit.a, data);
            var b = GenerateValue(Unit.b, data);
            return CodeUtility.MakeSelectable(Unit, $"{a} * {b}");
        }

        public override string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input.hasValidConnection)
            {
                return GetNextValueUnit(input, data);
            }
            else if (input.hasDefaultValue)
            {
                if (data.GetExpectedType() == typeof(int))
                {
                    return int.Parse(unit.defaultValues[input.key].ToString()).As().Code(true, true, true, "", false);
                }
                return unit.defaultValues[input.key].As().Code(true, true, true, "", false);
            }
            else
            {
                return $"/* \"{input.key} Requires Input\" */";
            }
        }
    }
}