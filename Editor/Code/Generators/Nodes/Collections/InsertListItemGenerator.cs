using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.CSharp;
using UnityEngine;

[NodeGenerator(typeof(InsertListItem))]
public class InsertListItemGenerator : NodeGenerator<InsertListItem>
{
    public InsertListItemGenerator(Unit unit) : base(unit)
    {
    }

    public override string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
    {
        var output = string.Empty;
        List<string> t = new List<string>();
        output += CodeBuilder.Indent(indent) + GenerateValue(Unit.listInput, data) + MakeSelectableForThisUnit($".Insert(") + GenerateValue(Unit.index, data) + MakeSelectableForThisUnit(", ") + GenerateValue(Unit.item, data) + MakeSelectableForThisUnit(");") + "\n";
        output += GetNextUnit(Unit.exit, data, indent);
        return output;
    }
}
