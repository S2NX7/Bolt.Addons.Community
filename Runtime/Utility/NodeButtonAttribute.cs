using System;

namespace Unity.VisualScripting.Community.Utility
{
    [RenamedFrom("Bolt.Community.Addons.Utility.UnitButtonAttribute")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class NodeButtonAttribute : Attribute
    {
        public string action;
        public string label;
        public NodeButtonAttribute(string action, string label = null)
        {
            this.action = action;
            this.label = label;
        }
    }
}