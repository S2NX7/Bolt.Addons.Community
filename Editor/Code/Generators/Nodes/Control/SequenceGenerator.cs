using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using UnityEngine;

[NodeGenerator(typeof(Sequence))]
public class SequenceGenerator : NodeGenerator<Sequence>
{
    public SequenceGenerator(Unit unit) : base(unit) { }

    public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
    {
        var outputBuilder = new StringBuilder();

        var outputs = Unit.multiOutputs;

        foreach (var controlOutput in outputs)
        {
            if (controlOutput.hasValidConnection)
            {
                outputBuilder.Append(GetNextUnit(controlOutput, data, indent));
            }
        }

        return outputBuilder.ToString();
    }
}