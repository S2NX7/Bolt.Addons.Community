using System;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.Humility;

public class ValueCode
{
    public ValueCode(string code, Unit unit = null)
    {
        this.code = code;
        this.unit = unit;
    }

    public ValueCode(string code, Type castType, Unit unit = null)
    {
        this.code = code;
        this.castType = castType;
        this.unit = unit;
    }

    public ValueCode(string code, Type castType, bool shouldCast, bool convertType = true, Unit unit = null)
    {
        this.code = code;
        if (shouldCast)
        {
            this.castType = castType;
        }
        this.convertType = convertType;
        this.unit = unit;
    }

    private bool convertType;
    private Type castType;
    private string code;
    private Unit unit;
    public bool isCasted
    {
        get
        {
            return castType != null;
        }
    }

    public string GetCode()
    {
        var cast = isCasted ? convertType ? $"(({castType.As().CSharpName(false, true)})" : $"({castType.As().CSharpName(false, true)})" : string.Empty;
        var _code = unit != null ? CodeUtility.MakeClickable(unit, cast) + code + (isCasted && convertType ? CodeUtility.MakeClickable(unit, ")") : string.Empty) : cast + code + (isCasted && convertType ? ")" : string.Empty);
        return _code;
    }

    public static implicit operator string(ValueCode valueCode)
    {
        return valueCode.GetCode();
    }

    public static string operator -(ValueCode valueCode, string str)
    {
        return valueCode.GetCode().Replace(str, "");
    }

    public override string ToString()
    {
        return GetCode();
    }
}