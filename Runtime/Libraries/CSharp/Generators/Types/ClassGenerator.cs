using Unity.VisualScripting.Community.Libraries.Humility;
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using System.Linq;

namespace Unity.VisualScripting.Community.Libraries.CSharp
{
    /// <summary>
    /// A generator that retains data for creating a new class as a string.
    /// </summary>
    [RenamedFrom("Bolt.Addons.Community.Libraries.CSharp.ClassGenerator")]
    public sealed class ClassGenerator : TypeGenerator
    {
        public RootAccessModifier scope;
        public AccessModifier nestedScope;
        public ClassModifier modifier;
#pragma warning disable 0414
        public bool isNested;
#pragma warning restore 0414
        public string name;
        public List<AttributeGenerator> attributes = new List<AttributeGenerator>(4);
        public List<FieldGenerator> fields = new List<FieldGenerator>(16);
        public List<PropertyGenerator> properties = new List<PropertyGenerator>(8);
        public List<MethodGenerator> methods = new List<MethodGenerator>(16);
        public List<ConstructorGenerator> constructors = new List<ConstructorGenerator>(2);
        public List<ClassGenerator> classes = new List<ClassGenerator>(4);
        public List<StructGenerator> structs = new List<StructGenerator>(2);
        public List<EnumGenerator> enums = new List<EnumGenerator>(2);
        public List<InterfaceGenerator> subInterfaces = new List<InterfaceGenerator>(2);
        public List<Type> interfaces = new List<Type>(4);
        private readonly System.Text.StringBuilder _codeBuilder = new(4096);
        private readonly HashSet<string> _usingsSet = new HashSet<string>();
        public Type inherits;
        public bool generateUsings;
        private bool useAssemblyQualifiedNameForInheritance;
        public string assemblyQualifiedInheritanceNamespace;
        public string assemblyQualifiedInheritanceType;
        public string beforeUsings;
        private ClassGenerator() { }

        /// <summary>
        /// Create a root class generator based on required parameters.
        /// </summary>
        public static ClassGenerator Class(RootAccessModifier scope, ClassModifier modifier, string name, Type inherits)
        {
            var @class = new ClassGenerator();
            @class.scope = scope;
            @class.modifier = modifier;
            @class.name = name;
            @class.inherits = inherits == null ? typeof(object) : inherits;
            @class.isNested = false;
            return @class;
        }

        /// <summary>
        /// Create a root class generator based on required parameters.
        /// </summary>
        public static ClassGenerator Class(RootAccessModifier scope, ClassModifier modifier, string name, string inherits, string inheritsNamespace, List<string> usings = null)
        {
            var @class = new ClassGenerator();
            @class.scope = scope;
            @class.modifier = modifier;
            @class.name = name;
            @class.assemblyQualifiedInheritanceNamespace = inheritsNamespace;
            @class.assemblyQualifiedInheritanceType = inherits;
            @class.isNested = false;
            @class.usings = usings;
            @class.useAssemblyQualifiedNameForInheritance = true;
            return @class;
        }

        /// <summary>
        /// Create a nested class generator based on required parameters.
        /// </summary>
        public static ClassGenerator Class(AccessModifier nestedScope, ClassModifier modifier, string name, Type inherits)
        {
            var @class = new ClassGenerator();
            @class.nestedScope = nestedScope;
            @class.modifier = modifier;
            @class.name = name;
            @class.inherits = inherits == null ? typeof(object) : inherits;
            @class.isNested = true;
            return @class;
        }

        protected override string GenerateBefore(int indent)
        {
            var output = !string.IsNullOrEmpty(beforeUsings) ? CodeBuilder.Indent(indent) + beforeUsings + "\n" : string.Empty;
            if (generateUsings)
            {
                var usings = Usings();
                var hasUsings = false;
                for (int i = 0; i < usings.Count; i++)
                {
                    if (!string.IsNullOrEmpty(usings[i]))
                    {
                        output += "using".ConstructHighlight() + " " + usings[i] + ";" + ((i < usings.Count - 1) ? "\n" : string.Empty);
                        hasUsings = true;
                    }
                }
                if (hasUsings) output += "\n\n";
            }

            for (int i = 0; i < attributes.Count; i++)
            {
                output += attributes[i].Generate(indent) + "\n";
            }

            var canShowInherits = !(inherits == null && string.IsNullOrEmpty(assemblyQualifiedInheritanceType) || inherits == typeof(object) && inherits.BaseType == null);
            output += CodeBuilder.Indent(indent) + scope.AsString().ConstructHighlight() + (modifier == ClassModifier.None ? string.Empty : " " + modifier.AsString().ConstructHighlight()) + " class ".ConstructHighlight() + name.LegalMemberName().TypeHighlight();
            output += (canShowInherits || interfaces.Count > 0) && SupportsInheritance() ? " : " : string.Empty;
            output += (canShowInherits || interfaces.Count > 0) && SupportsInheritance() ? (inherits == null ? assemblyQualifiedInheritanceType : inherits != typeof(object) ? inherits.As().CSharpName() : string.Empty) + (interfaces.Count > 0 && IsVaildInheritance() ? ", " : string.Empty) : string.Empty;

            output += string.Join(", ", interfaces.ConvertAll(i => i.As().CSharpName()));

            return output;
        }

        private bool SupportsInheritance()
        {
            return modifier != ClassModifier.Static && modifier != ClassModifier.StaticPartial;
        }

        private bool IsVaildInheritance()
        {
            return (inherits != null && inherits != typeof(object)) || !string.IsNullOrEmpty(assemblyQualifiedInheritanceType);
        }

        protected override string GenerateBody(int indent)
        {
            _codeBuilder.Clear();

            if (fields.Count > 0)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (!string.IsNullOrEmpty(fields[i].name))
                    {
                        _codeBuilder.Append(fields[i].Generate(indent));
                        if (i < fields.Count - 1) _codeBuilder.Append('\n');
                    }
                }

                if (properties.Count > 0 || constructors.Count > 0 || methods.Count > 0 ||
                    classes.Count > 0 || structs.Count > 0 || enums.Count > 0)
                {
                    _codeBuilder.Append("\n\n");
                }
            }

            if (properties.Count > 0)
            {
                for (int i = 0; i < properties.Count; i++)
                {
                    if (!string.IsNullOrEmpty(properties[i].name))
                    {
                        _codeBuilder.Append(properties[i].Generate(indent));
                        if (i < properties.Count - 1) _codeBuilder.Append("\n\n");
                    }
                }

                if (constructors.Count > 0 || methods.Count > 0 || classes.Count > 0 ||
                    structs.Count > 0 || enums.Count > 0)
                {
                    _codeBuilder.Append("\n\n");
                }
            }

            if (constructors.Count > 0)
            {
                for (int i = 0; i < constructors.Count; i++)
                {
                    _codeBuilder.Append(constructors[i].Generate(indent));
                    if (i < constructors.Count - 1) _codeBuilder.Append("\n\n");
                }

                if (methods.Count > 0 || classes.Count > 0 || structs.Count > 0 || enums.Count > 0)
                {
                    _codeBuilder.Append("\n\n");
                }
            }

            if (methods.Count > 0)
            {
                for (int i = 0; i < methods.Count; i++)
                {
                    if (!string.IsNullOrEmpty(methods[i].name))
                    {
                        _codeBuilder.Append(methods[i].Generate(indent));
                        if (i < methods.Count - 1) _codeBuilder.Append("\n\n");
                    }
                }

                if (classes.Count > 0 || structs.Count > 0 || enums.Count > 0)
                {
                    _codeBuilder.Append("\n\n");
                }
            }

            if (classes.Count > 0)
            {
                for (int i = 0; i < classes.Count; i++)
                {
                    _codeBuilder.Append(classes[i].Generate(indent));
                    if (i < classes.Count - 1) _codeBuilder.Append("\n\n");
                }

                if (structs.Count > 0 || enums.Count > 0)
                {
                    _codeBuilder.Append("\n\n");
                }
            }

            if (structs.Count > 0)
            {
                for (int i = 0; i < structs.Count; i++)
                {
                    _codeBuilder.Append(structs[i].Generate(indent));
                    if (i < structs.Count - 1) _codeBuilder.Append("\n\n");
                }

                if (enums.Count > 0)
                {
                    _codeBuilder.Append("\n\n");
                }
            }

            if (enums.Count > 0)
            {
                for (int i = 0; i < enums.Count; i++)
                {
                    _codeBuilder.Append(enums[i].Generate(indent));
                    if (i < enums.Count - 1) _codeBuilder.Append("\n\n");
                }
            }

            for (int i = 0; i < subInterfaces.Count; i++)
            {
                _codeBuilder.Append(subInterfaces[i].Generate(indent));
                if (i < subInterfaces.Count - 1) _codeBuilder.Append("\n\n");
            }

            return _codeBuilder.ToString();
        }

        protected override string GenerateAfter(int indent)
        {
            return "\n";
        }

        public override List<string> Usings()
        {
            _usingsSet.Clear();

            if (usings != null)
            {
                foreach (var @using in usings)
                {
                    _usingsSet.Add(@using);
                }
            }

            if (useAssemblyQualifiedNameForInheritance)
            {
                if (!string.IsNullOrEmpty(assemblyQualifiedInheritanceNamespace) &&
                    assemblyQualifiedInheritanceNamespace + "." + assemblyQualifiedInheritanceType != "System.Void")
                {
                    _usingsSet.Add(assemblyQualifiedInheritanceNamespace);
                }
            }
            else if (inherits != null && !inherits.Is().PrimitiveStringOrVoid())
            {
                _usingsSet.Add(inherits.Namespace);
            }

            foreach (var @interface in interfaces)
            {
                if (!string.IsNullOrEmpty(@interface.Namespace))
                {
                    _usingsSet.Add(@interface.Namespace);
                }
            }

            foreach (var attribute in attributes)
            {
                foreach (var @using in attribute.Usings())
                {
                    _usingsSet.Add(@using);
                }
            }

            foreach (var field in fields)
            {
                foreach (var @using in field.Usings())
                {
                    _usingsSet.Add(@using);
                }
            }

            foreach (var property in properties)
            {
                foreach (var @using in property.Usings())
                {
                    _usingsSet.Add(@using);
                }
            }

            foreach (var method in methods)
            {
                foreach (var @using in method.Usings())
                {
                    _usingsSet.Add(@using);
                }
            }

            return _usingsSet.ToList();
        }

        public ClassGenerator Inherit(Type type)
        {
            inherits = type;
            return this;
        }
        /// <summary>
        /// Add an interface to this class.
        /// </summary>
        public ClassGenerator ImplementInterface(Type type)
        {
            if (interfaces.Contains(type)) return this;
            interfaces.Add(type);
            return this;
        }

        /// <summary>
        /// Add an interface to this class.
        /// </summary>
        public ClassGenerator AddConstructor(ConstructorGenerator constructor)
        {
            constructors.Add(constructor);
            return this;
        }

        /// <summary>
        /// Add an attribute above this class.
        /// </summary>
        public ClassGenerator AddAttribute(AttributeGenerator generator)
        {
            attributes.Add(generator);
            return this;
        }

        /// <summary>
        /// Add a method to this class.
        /// </summary>
        public ClassGenerator AddMethod(MethodGenerator generator)
        {
            methods.Add(generator);
            return this;
        }

        /// <summary>
        /// Add a field to this class.
        /// </summary>
        public ClassGenerator AddField(FieldGenerator generator)
        {
            fields.Add(generator);
            return this;
        }

        /// <summary>
        /// Add a property to this class.
        /// </summary>
        /// <param name="generator"></param>
        /// <returns></returns>
        public ClassGenerator AddProperty(PropertyGenerator generator)
        {
            properties.Add(generator);
            return this;
        }

        /// <summary>
        /// Adds a nested class to this class.
        /// </summary>
        /// <param name="generator"></param>
        /// <returns></returns>
        public ClassGenerator AddClass(ClassGenerator generator)
        {
            classes.Add(generator);
            return this;
        }

        /// <summary>
        /// Add a nested struct to this class.
        /// </summary>
        public ClassGenerator AddStruct(StructGenerator generator)
        {
            structs.Add(generator);
            return this;
        }

        /// <summary>
        /// Add a nested enum to this class.
        /// </summary>
        public ClassGenerator AddEnum(EnumGenerator generator)
        {
            enums.Add(generator);
            return this;
        }

        /// <summary>
        /// Add an interface to this class.
        /// </summary>
        public ClassGenerator AddInterface(InterfaceGenerator generator)
        {
            subInterfaces.Add(generator);
            return this;
        }
    }
}
