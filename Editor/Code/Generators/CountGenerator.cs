namespace Unity.VisualScripting.Community 
{
    public abstract class CountGenerator : NodeGenerator
    {
        protected CountGenerator(Unit unit) : base(unit) { }
        public int count;
    } 
}