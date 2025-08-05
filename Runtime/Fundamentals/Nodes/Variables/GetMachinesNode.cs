using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

#if VISUAL_SCRIPTING_1_7
using SMachine = Unity.VisualScripting.ScriptMachine;
#else
using SMachine = Unity.VisualScripting.FlowMachine;
#endif

namespace Unity.VisualScripting.Community
{
    [UnitTitle("Get Machines")]
    [UnitSubtitle("With asset")]
    [TypeIcon(typeof(SMachine))]
    [UnitCategory("Community/Graphs")]
    [RenamedFrom("Bolt.Addons.Community.Fundamentals.GetMachinesUnit")]
    public sealed class GetMachinesNode : Unit
    {
        [DoNotSerialize]
        [NullMeansSelf]
        [PortLabelHidden]
        public ValueInput target;
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput asset;
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput machines;

        protected override void Definition()
        {
            target = ValueInput("target", (GameObject)null);
            target.NullMeansSelf();
            asset = ValueInput("asset", (ScriptGraphAsset)null);
            machines = ValueOutput("machine", (flow) =>
            {
                var machines = flow.GetValue<GameObject>(target).GetComponents<SMachine>();
                var _machines = new List<SMachine>();
                var targetAsset = flow.GetValue<ScriptGraphAsset>(asset);
                for (int i = 0; i < machines.Length; i++)
                {
                    if (machines[i].nest.macro == targetAsset) _machines.Add(machines[i]);
                }

                return _machines.ToArrayPooled();
            });
        }
    }
}