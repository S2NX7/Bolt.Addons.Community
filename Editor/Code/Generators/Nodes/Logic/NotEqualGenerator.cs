using Unity.VisualScripting;
using Unity.VisualScripting.Community.Libraries.Humility;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(NotEqual))]
    public sealed class NotEqualGenerator : NodeGenerator<NotEqual>
    {
        public NotEqualGenerator(NotEqual unit) : base(unit)
        {
        }

        public override string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input == Unit.a)
            {
                if (Unit.a.hasAnyConnection)
                {
                    var bIsConnected = Unit.b.hasValidConnection;
                    var bIsLiteral = IsSourceLiteral(Unit.b, out var sourceType);
                    if (bIsConnected && bIsLiteral)
                    {
                        data.SetExpectedType(sourceType);
                    }
                    var code = base.GenerateValue(Unit.a, data);
                    if (bIsConnected && bIsLiteral)
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
                    var aIsConnected = Unit.a.hasValidConnection;
                    var aIsLiteral = IsSourceLiteral(Unit.a, out var sourceType);
                    if (aIsConnected && aIsLiteral)
                    {
                        data.SetExpectedType(sourceType);
                    }
                    var code = base.GenerateValue(Unit.b, data);
                    if (aIsConnected && aIsLiteral)
                    {
                        data.RemoveExpectedType();
                    }
                    return code;
                }
                else
                {
                    return Unit.numeric ? Unit.defaultValues["b"].As().Code(true, Unit) : base.GenerateValue(input, data);
                }
            }

            return base.GenerateValue(input, data);
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {

            if (output == Unit.comparison)
            {
                return GenerateValue(Unit.a, data) + MakeClickableForThisUnit(" != ") + GenerateValue(Unit.b, data);
            }

            return base.GenerateValue(output, data);
        }
    }
}