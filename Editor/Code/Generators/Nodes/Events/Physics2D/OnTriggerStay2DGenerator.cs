using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Utility;
using UnityEngine;

#if MODULE_PHYSICS_2D_EXISTS
namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(OnTriggerStay2D))]
    public class OnTriggerStay2DGenerator : UnityMethodGenerator<OnTriggerStay2D, Collider2D>
    {
        public OnTriggerStay2DGenerator(Unit unit) : base(unit)
        {
        }

        public override List<ValueOutput> OutputValues => new List<ValueOutput>() { Unit.collider };

        public override List<TypeParam> Parameters => new List<TypeParam>() { new TypeParam(typeof(Collider2D), "other") };

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            return MakeSelectableForThisUnit("other".VariableHighlight());
        }
    }
}
#endif