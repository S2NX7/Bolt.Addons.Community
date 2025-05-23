namespace Unity.VisualScripting.Community 
{
    public class TrySurroundCommand : SurroundCommand
    {
        private TryCatch tryCatch = new TryCatch();
        public override Unit SurroundUnit => tryCatch;
    
        public override ControlOutput surroundSource => tryCatch.@try;
    
        public override ControlOutput surroundExit => sequenceUnit.multiOutputs[1];
    
        public override ControlInput unitEnterPort => tryCatch.enter;
    
        public override IUnitPort autoConnectPort => null;
    
        public override string DisplayName => "Try";
    
        public override bool SequenceExit => true;
    } 
}
