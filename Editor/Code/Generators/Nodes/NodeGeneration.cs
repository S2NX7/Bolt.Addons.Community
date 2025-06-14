using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using Unity.VisualScripting.Community.Libraries.CSharp;

namespace Unity.VisualScripting.Community
{
    public static class NodeGeneration
    {
        public static string GenerateValue<T>(this T node, ValueInput input, ControlGenerationData data = null) where T : Unit
        {
            var generator = GetGenerator(node);
            generator.UpdateRecursion();

            if (!generator.recursion?.TryEnter(node) ?? false)
            {
                return generator.MakeClickableForThisUnit(CodeUtility.ToolTip($"{input.key} is infinitely generating itself. Consider reviewing your graph logic.", "Infinite recursion detected!", ""));
            }

            try
            {
                return generator.GenerateValue(input, data);
            }
            finally
            {
                generator.recursion?.Exit(node);
            }
        }

        public static string GenerateValue<T>(this T node, ValueOutput output, ControlGenerationData data = null) where T : Unit
        {
            var generator = GetGenerator(node);
            generator.UpdateRecursion();

            if (!generator.recursion?.TryEnter(node) ?? false)
            {
                return generator.MakeClickableForThisUnit(CodeUtility.ToolTip($"{output.key} is infinitely generating itself. Consider reviewing your graph logic.", "Infinite recursion detected!", ""));
            }

            try
            {
                return generator.GenerateValue(output, data);
            }
            finally
            {
                generator.recursion?.Exit(node);
            }
        }

        public static string GenerateControl<T>(this T node, ControlInput input, ControlGenerationData data, int indent) where T : Unit
        {
            var generator = GetGenerator(node);
            generator.UpdateRecursion();

            if (!generator.recursion?.TryEnter(node) ?? false)
            {
                return CodeBuilder.Indent(indent) + generator.MakeClickableForThisUnit(CodeUtility.ToolTip("This node appears to cause infinite recursion(The flow is leading back to this node). Consider using a While loop instead.", "Infinite recursion detected!", ""));
            }

            try
            {
                return generator.GenerateControl(input, data, indent);
            }
            finally
            {
                generator.recursion?.Exit(node);
            }
        }

        private static readonly Dictionary<Unit, NodeGenerator> generatorCache = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeGenerator GetGenerator(this Unit node)
        {
            if (!generatorCache.TryGetValue(node, out var generator))
            {
                generator = NodeGenerator.GetSingleDecorator(node, node);
                generatorCache[node] = generator;
            }

            return generator;
        }

        public static void ClearGeneratorCache() => generatorCache.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodNodeGenerator GetMethodGenerator<T>(this T node) where T : Unit
        {
            if (GetGenerator(node) is MethodNodeGenerator methodNodeGenerator)
                return methodNodeGenerator;

            throw new InvalidOperationException($"{node.GetType()} is not a method generator.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableNodeGenerator GetVariableGenerator<T>(this T node) where T : Unit
        {
            if (GetGenerator(node) is VariableNodeGenerator variableNodeGenerator)
                return variableNodeGenerator;

            throw new InvalidOperationException($"{node.GetType()} is not a variable generator.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LocalVariableGenerator GetLocalVariableGenerator<T>(this T node) where T : Unit
        {
            if (GetGenerator(node) is LocalVariableGenerator localVariableGenerator)
                return localVariableGenerator;

            throw new InvalidOperationException($"{node.GetType()} is not a local variable generator.");
        }

        public static IUnitValuePort GetPesudoSource(this ValueInput input)
        {
            if (!input.hasValidConnection)
                return input;

            var source = input.connection.source;

            return source.unit switch
            {
                GraphInput graphInput => FindConnectedInput(GetGenerator(graphInput), source.key),
                SubgraphUnit subgraph => FindConnectedSubgraphOutput(subgraph, source.key),
                _ => source
            };
        }

        private static IUnitValuePort FindConnectedInput(NodeGenerator generator, string key)
        {
            foreach (var valueInput in generator.connectedValueInputs)
            {
                if (valueInput.key == key)
                {
                    if (valueInput.hasValidConnection)
                        return valueInput.connection.source;

                    if (valueInput.hasDefaultValue)
                        return valueInput;
                }
            }
            return null;
        }

        private static IUnitValuePort FindConnectedSubgraphOutput(SubgraphUnit subgraph, string key)
        {
            var graph = subgraph.nest?.graph;
            if (graph?.units.FirstOrDefault(u => u is GraphOutput) is not GraphOutput output)
                return null;

            foreach (var valueInput in output.valueInputs)
            {
                if (valueInput.key == key && valueInput.hasValidConnection)
                    return valueInput.connection.source;
            }

            return null;
        }
    }
}