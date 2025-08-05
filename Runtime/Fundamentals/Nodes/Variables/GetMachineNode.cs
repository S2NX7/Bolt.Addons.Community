using UnityEngine;

#if VISUAL_SCRIPTING_1_7
using SMachine = Unity.VisualScripting.ScriptMachine;
#else
using SMachine = Unity.VisualScripting.FlowMachine;
#endif

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Used to get a machine from a game object that is using the ScriptGraphAsset inputed
    /// </summary>
    [UnitTitle("Get Machine")]
    [UnitSubtitle("With asset")]
    [TypeIcon(typeof(SMachine))]
    [UnitCategory("Community/Graphs")]
    [RenamedFrom("Bolt.Addons.Community.Fundamentals.GetMachineUnit")]
    public sealed class GetMachineNode : Unit
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
        public ValueOutput machine;

        protected override void Definition()
        {
            target = ValueInput("target", (GameObject)null);
            target.NullMeansSelf();
            asset = ValueInput("asset", (ScriptGraphAsset)null);
            machine = ValueOutput("machine", (flow) =>
            {
                var machines = flow.GetValue<GameObject>(target).GetComponents<SMachine>();
                SMachine _machine = null;
                var targetAsset = flow.GetValue<ScriptGraphAsset>(asset); 
                for (int i = 0; i < machines.Length; i++)
                {
                    if (machines[i].nest.macro == targetAsset) return machines[i];
                }

                return _machine;
            });
        }
    }
}