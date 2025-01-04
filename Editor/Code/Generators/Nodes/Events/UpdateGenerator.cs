using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Utility;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Update))]
    public class UpdateGenerator : MethodNodeGenerator
    {
        public UpdateGenerator(Update unit) : base(unit)
        {
        }

        public override AccessModifier AccessModifier => AccessModifier.Private;

        public override MethodModifier MethodModifier => MethodModifier.None;

        public override string Name => "Update";

        public override Type ReturnType => typeof(void);

        public override List<TypeParam> Parameters => new List<TypeParam>();

        public override ControlOutput OutputPort => (unit as Update).trigger;

        public override List<ValueOutput> OutputValues => new List<ValueOutput>();

        public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            return GetNextUnit(OutputPort, Data ?? data, indent);
        }
    }
}