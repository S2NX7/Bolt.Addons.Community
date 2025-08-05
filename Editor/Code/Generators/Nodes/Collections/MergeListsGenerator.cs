using Unity.VisualScripting;
using System;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Linq;
using Unity.VisualScripting.Community.Libraries.Humility;
using System.Collections;
using System.Collections.Generic;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(MergeLists))]
    public class MergeListsGenerator : NodeGenerator<MergeLists>
    {
        public MergeListsGenerator(Unit unit) : base(unit)
        {
            NameSpaces = "Unity.VisualScripting.Community";
        }

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            if (data.GetExpectedType() != null && GetExpectedType(data.GetExpectedType()) != null)
            {
                return CodeBuilder.CallCSharpUtilityGenericMethod(Unit, MakeClickableForThisUnit(nameof(CSharpUtility.MergeLists)), Unit.multiInputs.Select(input => GenerateValue(input, data)).ToArray(), GetExpectedType(data.GetExpectedType()));
            }
            else
                return CodeBuilder.CallCSharpUtilityMethod(Unit, MakeClickableForThisUnit(nameof(CSharpUtility.MergeLists)), Unit.multiInputs.Select(input => GenerateValue(input, data)).ToArray());
        }

        private Type GetExpectedType(Type type)
        {
            if (typeof(IList).IsAssignableFrom(type) || typeof(IList<>).IsAssignableFrom(type))
            {
                NameSpaces = type.Namespace;
                if (type.IsGenericType)
                {
                    var types = type.GetGenericArguments();
                    NameSpaces += "," + types[0].Namespace;
                    return types[0];
                }
                else if (type == typeof(AotList))
                {
                    return typeof(object);
                }
            }
            return typeof(object);
        }
    }
}