using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Utility;
using UnityEngine;

#if MODULE_PHYSICS_2D_EXISTS
namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(OnCollisionEnter2D))]
    public class OnCollisionEnter2DGenerator : UnityMethodGenerator<OnCollisionEnter2D, Collision2D>
    {
        public OnCollisionEnter2DGenerator(Unit unit) : base(unit)
        {
        }

        public override List<ValueOutput> OutputValues => new List<ValueOutput>() { Unit.collider, Unit.contacts, Unit.relativeVelocity, Unit.enabled, Unit.data };

        public override List<TypeParam> Parameters => new List<TypeParam>() { new TypeParam(typeof(Collision2D), "other") };

        public override string GenerateValue(ValueOutput output, ControlGenerationData data)
        {
            if (output == Unit.collider)
            {
                return MakeSelectableForThisUnit("other".VariableHighlight() + "." + "collider".VariableHighlight());
            }
            else if (output == Unit.contacts)
            {
                return MakeSelectableForThisUnit("other".VariableHighlight() + "." + "contacts".VariableHighlight());
            }
            else if (output == Unit.relativeVelocity)
            {
                return MakeSelectableForThisUnit("other".VariableHighlight() + "." + "relativeVelocity".VariableHighlight());
            }
            else if (output == Unit.enabled)
            {
                return MakeSelectableForThisUnit("other".VariableHighlight() + "." + "enabled".VariableHighlight());
            }
            else
            {
                return MakeSelectableForThisUnit("other".VariableHighlight());
            }
        }
    }
}
#endif