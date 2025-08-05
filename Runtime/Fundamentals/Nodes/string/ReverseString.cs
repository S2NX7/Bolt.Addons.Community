using System.Linq;
using Unity.VisualScripting;

namespace Unity.VisualScripting.Community
{
    [RenamedFrom("ReverseStringNode")]
    [UnitCategory("Community\\Utility\\string")]
    [UnitTitle("Reverse String")]
    [TypeIcon(typeof(string))]
    public class ReverseStringNode : Unit
    {
        /// <summary>
        /// The input string to be reversed.
        /// </summary>
        [DoNotSerialize]
        public ValueInput input { get; private set; }

        /// <summary>
        /// The reversed output string.
        /// </summary>
        [DoNotSerialize]
        public ValueOutput output { get; private set; }

        protected override void Definition()
        {
            input = ValueInput<string>("input", string.Empty);
            output = ValueOutput<string>("output", (flow) => CSharpUtility.ReverseString(flow.GetValue<string>(input)));

            Requirement(input, output);
        }
    }
}