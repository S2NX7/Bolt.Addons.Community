﻿using Unity.VisualScripting.Community.Libraries.Humility;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using System.Reflection;
using System;
using UnityEngine;
using System.Linq;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Unit))]
    public class NodeGenerator : Decorator<NodeGenerator, NodeGeneratorAttribute, Unit>
    {
        public Unit unit;

        public bool hasNamespace;

        public string NameSpace = "";

        public string UniqueID = "";
        public string variableName = "";

        #region Subgraphs
        public List<ControlOutput> connectedGraphOutputs = new List<ControlOutput>();
        public List<ValueInput> connectedValueInputs = new List<ValueInput>();
        #endregion

        public Recursion recursion;

        public NodeGenerator(Unit unit)
        {
            this.unit = unit;
            recursion = Recursion.New(10);
        }

        public virtual string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input.hasValidConnection)
            {
                return GetNextValueUnit(input, data);
            }
            else if (input.hasDefaultValue)
            {
                return unit.defaultValues[input.key].As().Code(true, true, true, "", false);
            }
            else
            {
                return $"/* \"{input.key} Requires Input\" */";
            }
        }

        public virtual string GenerateValue(ValueOutput output, ControlGenerationData data) { return $"/* Port '{output.key}' of '{output.unit.GetType().Name}' Missing Generator. */"; }

        public virtual string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            return CodeUtility.MakeSelectable(unit, CodeBuilder.Indent(indent) + $"/* Port '{input.key}' of '{input.unit.GetType().Name}' Missing Generator. */");
        }


        public string GetNextUnit(ControlOutput controlOutput, ControlGenerationData data, int indent)
        {
            return controlOutput.hasValidConnection ? (controlOutput.connection.destination.unit as Unit).GenerateControl(controlOutput.connection.destination, data, indent) : string.Empty;
        }
        public string GetNextValueUnit(ValueInput valueInput, ControlGenerationData data, bool MakeSelectable = true)
        {
            return valueInput.hasValidConnection ? MakeSelectable ? CodeUtility.MakeSelectable(valueInput.connection.source.unit as Unit, (valueInput.connection.source.unit as Unit).GenerateValue(valueInput.connection.source, data)) : (valueInput.connection.source.unit as Unit).GenerateValue(valueInput.connection.source, data) : string.Empty;
        }

        public bool ShouldCast(ValueInput input, bool ignoreInputType = false, ControlGenerationData data = null)
        {
            if (input.hasValidConnection)
            {
                Type sourceType = GetSourceType(input, data);
                Type targetType = input.type;

                if (sourceType == null || targetType == null)
                {
                    return false;
                }

                if (!IsCastingRequired(sourceType, targetType, ignoreInputType))
                {
                    return false;
                }

                // Check if casting is possible
                return IsCastingPossible(sourceType, targetType);
            }

            return false;
        }

        public Type GetSourceType(ValueInput valueInput, ControlGenerationData data)
        {
            if(data != null && valueInput.hasValidConnection && data.TryGetVariableType(GetSingleDecorator(valueInput.connection.source.unit as Unit, valueInput.connection.source.unit as Unit).variableName, out Type type))
            {
                return type;
            }

            if(valueInput.hasValidConnection)
            {
                return valueInput.connection.source.type;
            }
            return null;
        }

        private bool IsCastingRequired(Type sourceType, Type targetType, bool ignoreInputType)
        {
            if (!ignoreInputType && targetType == typeof(object))
            {
                return false;
            }

            if (sourceType == targetType)
            {
                return false;
            }

            if (targetType.IsAssignableFrom(sourceType))
            {
                return false;
            }

            if(targetType == typeof(Transform) && sourceType == typeof(GameObject))
            {
                return true;
            }

            return true;
        }

        private bool IsCastingPossible(Type sourceType, Type targetType)
        {
            if (targetType.IsAssignableFrom(sourceType))
            {
                return true;
            }

            if (targetType.IsInterface && targetType.IsAssignableFrom(sourceType))
            {
                return true;
            }

            if (IsNumericConversionCompatible(targetType, sourceType))
            {
                return true;
            }

            if (IsNullableConversionCompatible(sourceType, targetType))
            {
                return true;
            }

            return false;
        }

        private bool IsNumericConversionCompatible(Type targetType, Type sourceType)
        {
            Type[] numericTypes = { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) };

            if (Array.Exists(numericTypes, t => t == targetType) &&
                Array.Exists(numericTypes, t => t == sourceType))
            {
                return true;
            }

            return false;
        }

        private bool IsNullableConversionCompatible(Type sourceType, Type targetType)
        {
            if (Nullable.GetUnderlyingType(targetType) != null)
            {
                Type underlyingTargetType = Nullable.GetUnderlyingType(targetType);
                return underlyingTargetType.IsAssignableFrom(sourceType);
            }

            if (Nullable.GetUnderlyingType(sourceType) != null)
            {
                Type underlyingSourceType = Nullable.GetUnderlyingType(sourceType);
                return targetType.IsAssignableFrom(underlyingSourceType);
            }

            return false;
        }
    }

    public class NodeGenerator<TUnit> : NodeGenerator where TUnit : Unit
    {
        public TUnit Unit;

        public NodeGenerator(Unit unit) : base(unit) { this.unit = unit; Unit = (TUnit)unit; }
    }
}

public static class CodeUtility
{
    private const string UniqueFormat = "[CommunityAddonsCodeSelectable({0})]{1}[CommunityAddonsCodeSelectableEnd({0})]";

    public static string HighlightCode(string code, string unitId)
    {
        var pattern = $@"\[CommunityAddonsCodeSelectable\({unitId}\)\](.*?)(\[CommunityAddonsCodeSelectableEnd\({unitId}\)\])";

        var highlightedCode = Regex.Replace(code, pattern, "<b class='highlight'>$1</b>", RegexOptions.Singleline);

        // Removing the end tag after highlighting
        highlightedCode = Regex.Replace(highlightedCode, @"\[CommunityAddonsCodeSelectableEnd\(" + Regex.Escape(unitId) + @"\)\]", "");

        return highlightedCode;
    }

    private static string AppendUniqueString(string code, string unit)
    {
        return string.Format(UniqueFormat, unit, code);
    }

    public static string RemoveAllSelectableTags(string multilineCode)
    {
        var pattern = @"\[CommunityAddonsCodeSelectable([^\]]*)\](.*?)(\[CommunityAddonsCodeSelectableEnd\([^\]]*\)\])";
        var pattern2 = @"\[CommunityAddonsCodeSelectable([^\]]*)\](.*?)";
        var strippedCode = Regex.Replace(multilineCode, pattern, "$2", RegexOptions.Singleline);
        strippedCode = Regex.Replace(strippedCode, pattern2, "");
        return strippedCode;
    }

    // Generates code with selectable tags for a unit
    public static string MakeSelectable(Unit unit, string code)
    {
        return AppendUniqueString(code, unit.ToString());
    }

    public static string RemoveCustomHighlights(string highlightedCode)
    {
        var pattern = @"<b class='highlight'>(.*?)<\/b>";
        var cleanedCode = Regex.Replace(highlightedCode, pattern, "$1", RegexOptions.Singleline);
        return cleanedCode;
    }
}
