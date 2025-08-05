namespace Unity.VisualScripting.Community
{
    [Descriptor(typeof(LogicParams))]
    public class LogicParamsDescriptor : UnitDescriptor<LogicParams>
    {
        public LogicParamsDescriptor(LogicParams unit) : base(unit) { }

        protected override EditorTexture DefinedIcon()
        {
            return unit.BranchingType switch
            {
                LogicParamNode.BranchType.And => typeof(And).Icon(),
                LogicParamNode.BranchType.Or => typeof(Or).Icon(),
                LogicParamNode.BranchType.GreaterThan => unit.AllowEquals ? typeof(GreaterOrEqual).Icon() : typeof(Greater).Icon(),
                LogicParamNode.BranchType.LessThan => unit.AllowEquals ? typeof(LessOrEqual).Icon() : typeof(Less).Icon(),
                LogicParamNode.BranchType.Equal => typeof(Equal).Icon(),
                _ => base.DefinedIcon(),
            };
        }
    }
}