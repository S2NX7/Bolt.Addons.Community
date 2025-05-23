using Unity.VisualScripting.Community.Libraries.Humility;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Collections.Generic;
using System;

namespace Unity.VisualScripting.Community
{
    [NodeGenerator(typeof(Unit))]
    public class NodeGenerator : Decorator<NodeGenerator, NodeGeneratorAttribute, Unit>
    {
        public Unit unit;

        public string NameSpaces = "";

        public string variableName = "";
        private int currentRecursionDepth = CSharpPreviewSettings.RecursionDepth;
        public Recursion recursion = Recursion.New(CSharpPreviewSettings.RecursionDepth);

        #region Subgraphs
        public List<ControlOutput> connectedGraphOutputs = new List<ControlOutput>();
        public List<ValueInput> connectedValueInputs = new List<ValueInput>();
        #endregion

        public NodeGenerator(Unit unit)
        {
            this.unit = unit;
        }

        public void UpdateRecursion()
        {
            if (currentRecursionDepth != CSharpPreviewSettings.RecursionDepth)
            {
                recursion = Recursion.New(CSharpPreviewSettings.RecursionDepth);
                currentRecursionDepth = CSharpPreviewSettings.RecursionDepth;
            }
        }

        public virtual string GenerateValue(ValueInput input, ControlGenerationData data)
        {
            if (input.hasValidConnection)
            {
                return GetNextValueUnit(input, data);
            }
            else if (input.hasDefaultValue)
            {
                return input.unit.defaultValues[input.key].As().Code(true, unit, true, true, "", true, true);
            }
            else
            {
                return MakeSelectableForThisUnit($"/* \"{input.key} Requires Input\" */".WarningHighlight());
            }
        }

        public virtual string GenerateValue(ValueOutput output, ControlGenerationData data) { return MakeSelectableForThisUnit($"/* Port '{output.key}' of '{output.unit.GetType().Name}' Missing Generator. */".WarningHighlight()); }

        public virtual string GenerateControl(ControlInput input, ControlGenerationData data, int indent)
        {
            return CodeBuilder.Indent(indent) + MakeSelectableForThisUnit($"/*{(input != null ? " Port '" + input.key + "' of " : "")}'{unit.GetType().Name}' Missing Generator. */".WarningHighlight());
        }

        public string GetNextUnit(ControlOutput controlOutput, ControlGenerationData data, int indent)
        {
            return controlOutput.hasValidConnection ? (controlOutput.connection.destination.unit as Unit).GenerateControl(controlOutput.connection.destination, data, indent) : string.Empty;
        }
        public string GetNextValueUnit(ValueInput valueInput, ControlGenerationData data)
        {
            return valueInput.hasValidConnection ? (valueInput.connection.source.unit as Unit).GenerateValue(valueInput.connection.source, data) : string.Empty;
        }

        public bool ShouldCast(ValueInput input, ControlGenerationData data, bool ignoreInputType = false)
        {
            if (input.hasValidConnection)
            {
                Type sourceType = GetSourceType(input, data);
                Type targetType = input.type;

                if (sourceType == null || targetType == null)
                {
                    return false;
                }

                if (data.IsCurrentExpectedTypeMet())
                {
                    return false;
                }

                if (!IsCastingRequired(sourceType, targetType, ignoreInputType))
                {
                    return false;
                }

                return IsCastingPossible(sourceType, targetType);
            }

            return false;
        }

        public Type GetSourceType(ValueInput valueInput, ControlGenerationData data)
        {
            if (data == null)
            {
                return valueInput.type;
            }
            if (valueInput.hasValidConnection && data.TryGetSymbol(valueInput.GetPesudoSource().unit as Unit, out var symbol))
            {
                return symbol.Type;
            }

            if (data != null && valueInput.hasValidConnection && valueInput.GetPesudoSource() != null && data.TryGetVariableType(GetSingleDecorator(valueInput.GetPesudoSource().unit as Unit, valueInput.GetPesudoSource().unit as Unit).variableName, out Type type))
            {
                return type;
            }

            if (valueInput.hasValidConnection && valueInput.GetPesudoSource() != null && GetSingleDecorator(valueInput.GetPesudoSource().unit as Unit, valueInput.GetPesudoSource().unit as Unit) is LocalVariableGenerator localVariable && localVariable.variableType != null)
            {
                return localVariable.variableType;
            }

            if (valueInput.hasValidConnection && valueInput.GetPesudoSource() != null && valueInput.GetPesudoSource()?.type != typeof(object))
            {
                return valueInput.GetPesudoSource().type;
            }

            if (valueInput.hasValidConnection)
            {
                return valueInput.connection.source.type;
            }

            return null;
        }

        private bool IsCastingRequired(Type sourceType, Type targetType, bool ignoreInputType)
        {
            bool isRequired = true;
            if (!ignoreInputType && targetType == typeof(object))
            {
                isRequired = false;
            }

            if (sourceType == targetType)
            {
                isRequired = false;
            }

            if (targetType.IsAssignableFrom(sourceType))
            {
                isRequired = false;
            }

            if (sourceType == typeof(object) && targetType != typeof(object))
            {
                isRequired = true;
            }

            return isRequired;
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

        public string MakeSelectableForThisUnit(string code, bool condition = true)
        {
            if (condition)
                return CodeUtility.MakeSelectable(unit, code);
            else
                return code;
        }

        protected bool CanPredictConnection(ValueInput target, ControlGenerationData data)
        {
            if (target.connection.source.unit is UnifiedVariableUnit variableUnit)
            {
                // This is one of the main problems so we check this first.
                if (variableUnit.kind == VariableKind.Scene)
                {
                    if (data.TryGetGraphPointer(out var graphPointer) && graphPointer.scene != null)
                        return Flow.CanPredict(target, graphPointer.AsReference());
                    else return false;
                }
            }

            return true;
        }
    }

    public class NodeGenerator<TUnit> : NodeGenerator where TUnit : Unit
    {
        public TUnit Unit;

        public NodeGenerator(Unit unit) : base(unit) { this.unit = unit; Unit = (TUnit)unit; }
    }
}