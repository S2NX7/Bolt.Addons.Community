using System.Collections.Generic;

namespace Unity.VisualScripting.Community 
{
    [Widget(typeof(ClearSavedVariables))]
    public class ClearVariablesUnitWidget : UnitWidget<ClearSavedVariables>
    {
        public ClearVariablesUnitWidget(FlowCanvas canvas, ClearSavedVariables unit) : base(canvas, unit)
        {
        }

        protected override NodeColorMix baseColor => NodeColorMix.TealReadable;
    } 
}
