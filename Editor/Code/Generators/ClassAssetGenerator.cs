﻿using Unity.VisualScripting.Community.Libraries.CSharp;
using System;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.Humility;
using System.Reflection;
using Unity.VisualScripting.Community.Utility;

namespace Unity.VisualScripting.Community
{
    [Serializable]
    [CodeGenerator(typeof(ClassAsset))]
    public sealed class ClassAssetGenerator : MemberTypeAssetGenerator<ClassAsset, ClassFieldDeclaration, ClassMethodDeclaration, ClassConstructorDeclaration>
    {
        /// <summary>
        /// Units that require the Update method in a monobehaviour to function
        /// </summary>
        private readonly List<Type> specialUnitTypes = new List<Type>() { typeof(Timer) };
        private List<Unit> specialUnits = new List<Unit>();
        private ControlGenerationData UpdateGenerationData;
        protected override TypeGenerator OnGenerateType(ref string output, NamespaceGenerator @namespace)
        {
            if (Data == null)
                return ClassGenerator.Class(RootAccessModifier.Public, ClassModifier.None, "", null);

            string className = Data.title.LegalMemberName();
            Type baseType = Data.scriptableObject
                ? typeof(ScriptableObject)
                : (Data.inheritsType && Data.inherits.type != null ? Data.GetInheritedType() : typeof(object));

            var @class = ClassGenerator.Class(RootAccessModifier.Public, ClassModifier.None, className, baseType);

            if (Data.definedEvent)
                @class.ImplementInterface(typeof(IDefinedEvent));

            if (Data.inspectable)
                @class.AddAttribute(AttributeGenerator.Attribute<InspectableAttribute>());

            if (Data.serialized)
                @class.AddAttribute(AttributeGenerator.Attribute<SerializableAttribute>());

            if (Data.includeInSettings)
                @class.AddAttribute(AttributeGenerator.Attribute<IncludeInSettingsAttribute>().AddParameter(true));

            if (Data.scriptableObject)
            {
                @class.AddAttribute(AttributeGenerator.Attribute<CreateAssetMenuAttribute>()
                    .AddParameter("menuName", Data.menuName)
                    .AddParameter("fileName", Data.fileName)
                    .AddParameter("order", Data.order));
            }

            if (Data.inheritsType && Data.inherits.type != null)
            {
                @class.AddUsings(new List<string> { Data.inherits.type.Namespace });
            }

            foreach (var attribute in Data.attributes)
            {
                var attrGenerator = AttributeGenerator.Attribute(attribute.GetAttributeType());
                foreach (var param in attribute.parameters)
                {
                    if (param.defaultValue is IList list)
                    {
                        if (param.isParamsParameter)
                        {
                            foreach (var item in list)
                            {
                                attrGenerator.AddParameter(item);
                            }
                        }
                        else if (!attrGenerator.parameterValues.Contains(param))
                        {
                            attrGenerator.AddParameter(param.defaultValue);
                        }
                    }
                    else if (!attrGenerator.parameterValues.Contains(param))
                    {
                        attrGenerator.AddParameter(param.defaultValue);
                    }
                }
                @class.AddAttribute(attrGenerator);
            }

            foreach (var constructorData in Data.constructors)
            {
                var parameters = constructorData.parameters;
                if (@class.constructors.Any(f => f.parameters.Select(param => param.generator.type).SequenceEqual(parameters.Select(param => param.type))))
                {
                    continue;
                }

                var constructor = ConstructorGenerator.Constructor(constructorData.scope, constructorData.modifier, constructorData.CallType, className);

                if (constructorData.graph.units.Count > 0)
                {
                    @class.AddUsings(ProcessGraphUnits(constructorData.graph, @class));
                    var generationData = CreateGenerationData(typeof(void));
                    constructor.Body(FunctionNodeGenerator.GetSingleDecorator(constructorData.graph.units[0] as Unit, constructorData.graph.units[0] as Unit).GenerateControl(null, generationData, 0));

                    foreach (var param in parameters)
                    {
                        param.showCall = true;
                        if (!string.IsNullOrEmpty(param.name))
                        {
                            constructor.AddParameter(param.useInCall, CreateParameter(param));
                        }
                    }

                    @class.AddConstructor(constructor);
                }
            }

            foreach (var variableData in Data.variables)
            {
                if (!string.IsNullOrEmpty(variableData.name) && variableData.type != null)
                {
                    if (@class.fields.Any(f => f.name == variableData.FieldName) || @class.properties.Any(p => p.name == variableData.FieldName))
                    {
                        continue;
                    }

                    if (variableData.isProperty)
                    {
                        ProcessProperty(variableData, @class);
                    }
                    else
                    {
                        ProcessField(variableData, @class);
                    }
                }
            }

            foreach (var methodData in Data.methods)
            {
                if (!string.IsNullOrEmpty(methodData.name) && methodData.returnType != null)
                {
                    if (@class.methods.Any(m => m.name == methodData.name && m.parameters.Select(p => p.type).SequenceEqual(methodData.parameters.Select(p => p.type))))
                    {
                        continue;
                    }

                    var method = MethodGenerator.Method(methodData.scope, methodData.modifier, methodData.returnType, methodData.name);
                    AddMethodAttributes(method, methodData);

                    if (methodData.graph.units.Count > 0)
                    {
                        var generationData = CreateGenerationData(methodData.returnType);
                        if (methodData.methodName.Replace(" ", "") == "Update")
                        {
                            UpdateGenerationData = generationData;
                        }
                        @class.AddUsings(ProcessGraphUnits(methodData.graph, @class));
                        var unit = methodData.graph.units[0] as FunctionNode;
                        method.Body(FunctionNodeGenerator.GetSingleDecorator(unit, unit).GenerateControl(null, generationData, 0));

                        foreach (var param in methodData.parameters)
                        {
                            if (!string.IsNullOrEmpty(param.name))
                            {
                                method.AddParameter(CreateParameter(param));
                            }
                        }
                    }
                    @class.AddMethod(method);
                }
            }

            if (specialUnits.Count > 0)
            {
                HashSet<Unit> visited = new HashSet<Unit>();
                if (@class.methods.Any(m => m.name.Replace(" ", "") == "Update"))
                {
                    var method = @class.methods.First(m => m.name.Replace(" ", "") == "Update");
                    foreach (var specialUnit in specialUnits)
                    {
                        if (!visited.Add(specialUnit))
                            continue;
                        var generator = NodeGenerator.GetSingleDecorator(specialUnit, specialUnit);
                        if (specialUnit is Timer timer && !string.IsNullOrEmpty(generator.variableName) && Data.inheritsType && Data.GetInheritedType() == typeof(MonoBehaviour))
                        {
                            var convertedGenerator = generator as TimerGenerator;
                            method.beforeBody += CodeBuilder.Indent(2) + CodeUtility.MakeSelectable(timer, generator.variableName.VariableHighlight() + ".Update();") + "\n";
                        }
                    }
                }
            }

            @namespace.AddClass(@class);
            return @class;
        }

        private List<string> ProcessGraphUnits(FlowGraph graph, ClassGenerator @class)
        {
            var usings = new List<string>();
            foreach (var _unit in graph.GetUnitsRecursive(Recursion.New(Recursion.defaultMaxDepth)).Cast<Unit>())
            {
                var generator = NodeGenerator.GetSingleDecorator(_unit, _unit);
                TriggerHandleOtherGenerators(_unit.GetType(), @class, generator);
                if (_unit.GetType() == typeof(Timer))
                {
                    specialUnits.Add(_unit);
                }

                AddNamespacesToUsings(generator, usings);
            }

            // Add usings to constructor or method as necessary
            return usings;
        }

        private void ProcessProperty(ClassFieldDeclaration variableData, ClassGenerator @class)
        {
            var property = PropertyGenerator.Property(variableData.scope, variableData.propertyModifier, variableData.type, variableData.name, variableData.defaultValue != null, variableData.getterScope, variableData.setterScope);
            property.Default(variableData.defaultValue);
            AddAttributesToProperty(property, variableData.attributes);

            // Handle getter
            if (variableData.get)
            {
                var generationData = CreateGenerationData(variableData.type);
                @class.AddUsings(ProcessGraphUnits(variableData.getter.graph, @class));
                property.MultiStatementGetter(variableData.getterScope, NodeGenerator.GetSingleDecorator(variableData.getter.graph.units[0] as Unit, variableData.getter.graph.units[0] as Unit).GenerateControl(null, generationData, 0));
            }

            // Handle setter
            if (variableData.set)
            {
                @class.AddUsings(ProcessGraphUnits(variableData.setter.graph, @class));
                var generationData = CreateGenerationData(typeof(void));
                property.MultiStatementSetter(variableData.setterScope, NodeGenerator.GetSingleDecorator(variableData.setter.graph.units[0] as Unit, variableData.setter.graph.units[0] as Unit).GenerateControl(null, generationData, 0));
            }

            @class.AddProperty(property);
        }

        private void ProcessField(ClassFieldDeclaration variableData, ClassGenerator @class)
        {
            var field = FieldGenerator.Field(variableData.scope, variableData.fieldModifier, variableData.type, variableData.name, variableData.defaultValue);
            AddAttributesToField(field, variableData.attributes);
            @class.AddField(field);
        }

        private void AddAttributesToProperty(PropertyGenerator property, List<AttributeDeclaration> attributes)
        {
            foreach (var attribute in attributes)
            {
                var attrGenerator = AttributeGenerator.Attribute(attribute.GetAttributeType());
                AddParametersToAttribute(attrGenerator, attribute.parameters);
                property.AddAttribute(attrGenerator);
            }
        }

        private void AddAttributesToField(FieldGenerator field, List<AttributeDeclaration> attributes)
        {
            foreach (var attribute in attributes)
            {
                var attrGenerator = AttributeGenerator.Attribute(attribute.GetAttributeType());
                AddParametersToAttribute(attrGenerator, attribute.parameters);
                field.AddAttribute(attrGenerator);
            }
        }

        private void AddParametersToAttribute(AttributeGenerator attrGenerator, List<TypeParam> parameters)
        {
            foreach (var param in parameters)
            {
                if (param.defaultValue is IList list)
                {
                    if (param.isParamsParameter)
                    {
                        foreach (var item in list)
                        {
                            attrGenerator.AddParameter(item);
                        }
                    }
                    else
                    {
                        attrGenerator.AddParameter(param.defaultValue);
                    }
                }
                else if (!attrGenerator.parameterValues.Contains(param))
                {
                    attrGenerator.AddParameter(param.defaultValue);
                }
            }
        }

        private ParameterGenerator CreateParameter(TypeParam parameter)
        {
            return ParameterGenerator.Parameter(
                parameter.name,
                parameter.type,
                parameter.modifier,
                parameter.attributes,
                parameter.hasDefault,
                parameter.isParamsParameter,
                parameter.defaultValue
            );
        }

        private ControlGenerationData CreateGenerationData(Type returnType)
        {
            var generationData = new ControlGenerationData();
            if (Data.inheritsType)
            {
                generationData.ScriptType = Data.GetInheritedType();
            }
            generationData.returns = returnType;
            foreach (var variable in Data.variables)
            {
                generationData.AddLocalNameInScope(variable.FieldName);
            }
            return generationData;
        }

        private void AddNamespacesToUsings(NodeGenerator generator, List<string> usings)
        {
            if (!string.IsNullOrEmpty(generator.NameSpace))
            {
                usings.Add(generator.NameSpace);
            }
        }

        private void AddMethodAttributes(MethodGenerator method, ClassMethodDeclaration methodData)
        {
            foreach (var attribute in methodData.attributes)
            {
                var attrGenerator = AttributeGenerator.Attribute(attribute.GetAttributeType());
                foreach (var param in attribute.parameters)
                {
                    attrGenerator.AddParameter(param.defaultValue);
                }
                method.AddAttribute(attrGenerator);
            }
        }

        private Dictionary<Type, Action<ClassGenerator, NodeGenerator>> HandleOtherGeneratorsMethods = new Dictionary<Type, Action<ClassGenerator, NodeGenerator>>();

        private void TriggerHandleOtherGenerators(Type type, ClassGenerator @class, NodeGenerator generator)
        {
            if (!HandleOtherGeneratorsMethods.TryGetValue(type, out var cachedDelegate))
            {
                var method = GetType()
                    .GetMethod(nameof(HandleOtherGenerators),
                               BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic)
                    .MakeGenericMethod(type);

                cachedDelegate = (Action<ClassGenerator, NodeGenerator>)
                    Delegate.CreateDelegate(typeof(Action<ClassGenerator, NodeGenerator>), this, method);

                HandleOtherGeneratorsMethods[type] = cachedDelegate;
            }

            cachedDelegate.Invoke(@class, generator);
        }

        private void HandleOtherGenerators<T>(ClassGenerator @class, NodeGenerator generator) where T : Unit
        {
            if (generator is VariableNodeGenerator<T> variableGenerator)
            {
                var existingFields = new HashSet<string>(@class.fields.Select(f => f.name));
                variableGenerator.count = 0;

                while (existingFields.Contains(variableGenerator.Name))
                {
                    variableGenerator.count++;
                }

                @class.AddField(FieldGenerator.Field(variableGenerator.AccessModifier, variableGenerator.FieldModifier, variableGenerator.Type, variableGenerator.Name));
            }
            else if (generator is MethodNodeGenerator<T> methodGenerator)
            {
                var existingMethods = new HashSet<string>(@class.methods.Select(m => m.name));
                methodGenerator.count = 0;

                while (existingMethods.Contains(methodGenerator.Name))
                {
                    methodGenerator.count++;
                }

                var method = MethodGenerator.Method(methodGenerator.AccessModifier, methodGenerator.MethodModifier, methodGenerator.ReturnType, methodGenerator.Name);
                method.AddGenerics(methodGenerator.GenericCount);

                foreach (var param in methodGenerator.Parameters)
                {
                    if (methodGenerator.GenericCount == 0 || !param.usesGeneric)
                        method.AddParameter(ParameterGenerator.Parameter(param.name, param.type, param.modifier));
                    else if (methodGenerator.GenericCount > 0 && param.usesGeneric)
                    {
                        var genericString = method.generics[param.generic];
                        method.AddParameter(ParameterGenerator.Parameter(param.name, genericString, param.modifier));
                    }
                }

                foreach (var variable in Data.variables)
                {
                    methodGenerator.Data.AddLocalNameInScope(variable.FieldName);
                }

                method.Body(methodGenerator.MethodBody);
                @class.AddMethod(method);
            }
        }
    }
}