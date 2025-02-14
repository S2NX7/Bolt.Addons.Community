﻿using Unity.VisualScripting.Community.Libraries.Humility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.VisualScripting.Community.Libraries.CSharp
{
    [RenamedFrom("Bolt.Addons.Community.Libraries.CSharp.MethodGenerator")]
    public sealed class MethodGenerator : BodyGenerator
    {
        public AccessModifier scope;
        public MethodModifier modifier;
        public string name;
        public Type returnType;
        public List<ParameterGenerator> parameters = new List<ParameterGenerator>();
        public List<AttributeGenerator> attributes = new List<AttributeGenerator>();
        public List<string> generics = new List<string>();
        public string body;
        public string beforeBody;
        public string warning;

        private MethodGenerator() { }

        public static MethodGenerator Method(AccessModifier scope, MethodModifier modifier, Type returnType, string name)
        {
            var method = new MethodGenerator();
            method.scope = scope;
            method.modifier = modifier;
            method.name = name;
            method.returnType = returnType;
            return method;
        }

        protected override sealed string GenerateBefore(int indent)
        {
            var attributes = string.Empty;
            foreach (AttributeGenerator attr in this.attributes)
            {
                attributes += attr.Generate(indent) + "\n";
            }
            var _warning = !string.IsNullOrEmpty(warning) ? CodeBuilder.Indent(indent) + $"/* {warning} */\n".WarningHighlight() : string.Empty;
            var modSpace = modifier == MethodModifier.None ? string.Empty : " ";
            var genericTypes = generics.Count > 0 ? $"<{string.Join(", ", generics)}>" : string.Empty;
            return attributes + _warning + CodeBuilder.Indent(indent) + (scope == AccessModifier.None ? "" : scope.AsString().ToLower().ConstructHighlight() + " ") + modifier.AsString().ConstructHighlight() + modSpace + returnType.As().CSharpName() + " " + name.LegalMemberName() + genericTypes + CodeBuilder.Parameters(this.parameters);
        }

        protected override sealed string GenerateBody(int indent)
        {
            if (string.IsNullOrEmpty(name)) { return string.Empty; }
            return string.IsNullOrEmpty(body) ? string.Empty : body.Contains("\n") ? beforeBody + body.Replace("\n", "\n" + CodeBuilder.Indent(indent)).Insert(0, CodeBuilder.Indent(indent)) : CodeBuilder.Indent(indent) + beforeBody + "\n" + CodeBuilder.Indent(indent) + body;
        }

        protected override sealed string GenerateAfter(int indent)
        {
            return string.Empty;
        }

        public MethodGenerator AddAttribute(AttributeGenerator generator)
        {
            attributes.Add(generator);
            return this;
        }

        public MethodGenerator Body(string body)
        {
            this.body = body;
            return this;
        }

        public MethodGenerator AddGeneric()
        {
            var count = generics.Count == 1 ? "" : (generics.Count - 1).ToString();
            generics.Add("T" + count);
            return this;
        }

        public MethodGenerator AddGenerics(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                    generics.Add("T".TypeHighlight());
                else
                    generics.Add(("T" + i).TypeHighlight());
            }
            return this;
        }

        public MethodGenerator AddParameter(ParameterGenerator parameter)
        {
            parameters.Add(parameter);
            return this;
        }

        public MethodGenerator SetWarning(string warning)
        {
            this.warning = warning;
            return this;
        }

        public override List<string> Usings()
        {
            var usings = new List<string>();

            if (!usings.Contains(returnType.Namespace) && !returnType.Is().PrimitiveStringOrVoid()) usings.Add(returnType.Namespace);

            for (int i = 0; i < attributes.Count; i++)
            {
                usings.MergeUnique(attributes[i].Usings());
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                if (!parameters[i].useAssemblyQualifiedType && !usings.Contains(parameters[i].Using()) && !parameters[i].type.Is().PrimitiveStringOrVoid()) usings.Add(parameters[i].Using());
            }

            return usings;
        }
    }
}