using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Utility;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(OnButtonClick))]
    public class OnButtonClickGenerator : EventListenerMethodGenerator<OnButtonClick>
    {
        public OnButtonClickGenerator(Unit unit) : base(unit) { NameSpaces = "UnityEngine.UI"; }
        public override ControlOutput OutputPort => Unit.trigger;

        protected override bool IsCoroutine()
        {
            return Unit.coroutine;
        }

        protected override string GetListenerSetupCode()
        {
            return $".GetComponent<{"Button".TypeHighlight()}>()?.{"onClick".VariableHighlight()}?.AddListener({(!Unit.coroutine ? Name : $"() => StartCoroutine({Name}())")});";
        }

        protected override ValueInput GetTargetValueInput()
        {
            return Unit.target;
        }
    }
}
