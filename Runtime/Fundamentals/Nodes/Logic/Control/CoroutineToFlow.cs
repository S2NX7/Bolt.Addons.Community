using Unity.VisualScripting;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Converts a Coroutine flow to a normal flow.
    /// </summary>
    [RenamedFrom("CorutineConverter")]
    [RenamedFrom("Unity.VisualScripting.Community.CorutineConverter")]
    [UnitTitle("CoroutineToFlow")]
    [UnitCategory("Community\\Control")]
    [TypeIcon(typeof(Flow))]
    public class CoroutineToFlow : Unit
    {

        [DoNotSerialize]
        public ControlInput In;
        [DoNotSerialize]
        public ControlOutput Converted;
        [DoNotSerialize]
        [PortLabel("Coroutine")]
        public ControlOutput Corutine;
        private VariableDeclarationCollection GetVariables(Flow flow)
        {
            return (VariableDeclarationCollection)typeof(VariableDeclarations).GetField("collection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValueOptimized(flow.variables);
        }

        protected override void Definition()
        {
            In = ControlInput("In", Convert);
            Converted = ControlOutput("Flow");
            Corutine = ControlOutput("Coroutine");

            Succession(In, Converted);
            Succession(In, Corutine);
        }

        private ControlOutput Convert(Flow flow)
        {
            var GraphRef = flow.stack.ToReference();

            if (!flow.isCoroutine)
            {
                Debug.LogWarning("CoroutineToFlow node is used to convert a Corutine flow to a normal flow there is no point in using it in a regular flow", flow.stack.gameObject);
                return Converted;
            }
            else
            {
                var Convertedflow = Flow.New(GraphRef);
                Convertedflow.Run(Converted);
                return Corutine;
            }
        }
    }

}