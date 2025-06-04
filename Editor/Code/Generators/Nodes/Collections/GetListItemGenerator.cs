using System;
using System.Collections;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(GetListItem))]
    public class GetListItemGenerator : NodeGenerator<GetListItem>
    {
        public GetListItemGenerator(Unit unit) : base(unit)
        {
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            var code = MakeSelectableForThisUnit($"[") + GenerateValue(Unit.index, data) + MakeSelectableForThisUnit("]");
            data.CreateSymbol(Unit, typeof(object), code);
            data.SetExpectedType(Unit.list.type);
            var listCode = GenerateValue(Unit.list, data);
            var (type, isMet) = data.RemoveExpectedType();
            if (isMet)
            {
                data.SetSymbolType(Unit, GetCollectionType(type));
            }
            var collectionType = GetCollectionType(type);
            if (collectionType != null && collectionType != typeof(object) && data.GetExpectedType() != null && data.GetExpectedType() != typeof(object))
            {
                var _isMet = data.GetExpectedType().IsAssignableFrom(collectionType);
                data.SetCurrentExpectedTypeMet(_isMet, collectionType);
            }

            return new ValueCode(listCode + code, data.GetExpectedType(), data.GetExpectedType() != null && !data.IsCurrentExpectedTypeMet() && !data.GetExpectedType().IsAssignableFrom(GetCollectionType(type)), true, Unit);
        }

        private Type GetCollectionType(Type type)
        {
            if (type != null && type.IsGenericType && typeof(IList).IsAssignableFrom(type))
                return type.GetGenericArguments()[0];
            else if (type != null && type.IsArray)
                return type.GetElementType();
            else
                return typeof(object);
        }
    }
}