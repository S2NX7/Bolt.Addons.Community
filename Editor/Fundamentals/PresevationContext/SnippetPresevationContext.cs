using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    public class SnippetPreservationContext<TSourceUnit> : UnitPreservationContext<TSourceUnit> where TSourceUnit : SnippetSourceUnit
    {
        private SnippetType snippetType;
        private GraphSnippet graphSnippet;
        private string currentQuery;
        public SnippetPreservationContext(Unit unitToPreserveFrom, FlowGraph graph, SnippetType snippetType, GraphSnippet graphSnippet, string currentQuery) : base(unitToPreserveFrom, graph)
        {
            this.snippetType = snippetType;
            this.graphSnippet = graphSnippet;
            this.currentQuery = currentQuery;
            Create();
        }

        protected override void InitializeInitialPortKey()
        {
            if (snippetType == SnippetType.ControlInput)
            {
                initialPortKey = unitToPreserveFrom.controlInputs
                    .FirstOrDefault(c => c.connections.Any(conn => conn.source.unit is TSourceUnit))
                    ?.key ?? "";
            }
            else if (snippetType == SnippetType.ValueInput)
            {
                initialPortKey = unitToPreserveFrom.valueOutputs
                    .FirstOrDefault(c => c.connections.Any(conn => conn.destination.unit is TSourceUnit))
                    ?.key ?? "";
            }
        }

        protected override void ProcessValueInputs()
        {
            string[] queryParts = currentQuery.Split(',');

            foreach (var input in unitToPreserveFrom.valueInputs.Where(i => i.hasValidConnection))
            {
                var connectedUnit = input.connection.source.unit as Unit;
                bool usingDefault = false;
                if (connectedUnit is SnippetInputNode snippetInputNode)
                {
                    var snippetArgument = graphSnippet.snippetArguments.Find(arg => arg.argumentName == snippetInputNode.argumentName);
                    if (snippetArgument != null)
                    {
                        usingDefault = input.hasDefaultValue;
                        int argumentIndex = graphSnippet.snippetArguments.IndexOf(snippetArgument);
                        if (argumentIndex >= 0 && argumentIndex < queryParts.Length)
                        {
                            string argumentValueString = queryParts[argumentIndex + 1];
                            Type argType = snippetArgument.argumentType;
                            if (!usingDefault)
                            {
                                var literal = new Literal(argType, ParseLiteral(argumentValueString, argType));
                                literal.position = input.connection.source.unit.position;
                                connectedUnit = literal;
                            }
                            else
                                defaultValues[input.key] = ParseLiteral(argumentValueString, argType);
                        }
                    }
                    else
                        continue;
                }
                if (!usingDefault)
                {
                    var preservation = new SnippetPreservationContext<TSourceUnit>(connectedUnit, graph, snippetType, graphSnippet, currentQuery);

                    connectedPreservation.Add(preservation);
                    connectedPorts.Add(((unitToRestoreTo, input.key, PortType.ValueInput), (preservation, input.connection.source.key)));
                }
            }
        }

        private object ParseLiteral(string input, Type type)
        {
            if (type == typeof(bool) && bool.TryParse(input, out bool boolResult))
            {
                return boolResult;
            }

            if (type == typeof(char))
            {
                return input[0];
            }

            if (type == typeof(byte) && byte.TryParse(input, out byte byteResult))
            {
                return byteResult;
            }

            if (type == typeof(sbyte) && sbyte.TryParse(input, out sbyte sbyteResult))
            {
                return sbyteResult;
            }

            if (type == typeof(short) && short.TryParse(input, out short shortResult))
            {
                return shortResult;
            }

            if (type == typeof(ushort) && ushort.TryParse(input, out ushort ushortResult))
            {
                return ushortResult;
            }

            if (type == typeof(int) && int.TryParse(input, out int intResult))
            {
                return intResult;
            }

            if (type == typeof(uint) && uint.TryParse(input, out uint uintResult))
            {
                return uintResult;
            }

            if (type == typeof(long) && long.TryParse(input, out long longResult))
            {
                return longResult;
            }

            if (type == typeof(ulong) && ulong.TryParse(input, out ulong ulongResult))
            {
                return ulongResult;
            }

            if (type == typeof(float) && float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatResult))
            {
                return floatResult;
            }

            if (type == typeof(double) && double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleResult))
            {
                return doubleResult;
            }

            if (type == typeof(decimal) && decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalResult))
            {
                return decimalResult;
            }

            return input.TrimStart(" ");
        }

        // Process control outputs and connect them to preserved units
        protected override void ProcessControlOutputs()
        {
            foreach (var controlOutput in unitToPreserveFrom.controlOutputs.Where(o => o.hasValidConnection))
            {
                var connectedUnit = controlOutput.connection.destination.unit as Unit;
                if (processedUnits.Contains(connectedUnit)) return;

                processedUnits.Add(connectedUnit);
                var preservation = new SnippetPreservationContext<TSourceUnit>(connectedUnit, graph, snippetType, graphSnippet, currentQuery);

                connectedPreservation.Add(preservation);
                connectedPorts.Add(((unitToRestoreTo, controlOutput.key, PortType.ControlOutput), (preservation, controlOutput.connection.destination.key)));
            }
        }

        public static SnippetPreservationContext<TSourceUnit> AddSnippet(FlowGraph graph, FlowCanvas canvas, Unit originalUnit, SnippetType snippetType, GraphSnippet graphSnippet, string currentQuery)
        {
            var sourcePort = canvas.connectionSource;
            var preservation = new SnippetPreservationContext<TSourceUnit>(originalUnit, graph, snippetType, graphSnippet, currentQuery)
            {
                sourcePort = sourcePort
            };

            var fuzzyWindowPosition = canvas.connectionEnd;
            var offsetPosition = fuzzyWindowPosition;

            canvas.CancelConnection();
            preservation.AddToGraph(offsetPosition);
            preservation.Connect();
            preservation.Reset();
            return preservation;
        }
    }
}