using Unity.VisualScripting.Community.Libraries.CSharp;
using System;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.Humility;
using System.Reflection;
using Unity.VisualScripting.Community.Utility;
using UnityEngine.InputSystem;
using Unity.VisualScripting.InputSystem;

namespace Unity.VisualScripting.Community
{
    [Serializable]
    [CodeGenerator(typeof(ClassAsset))]
    public sealed class ClassAssetGenerator : MemberTypeAssetGenerator<ClassAsset, ClassFieldDeclaration, ClassMethodDeclaration, ClassConstructorDeclaration>
    {
        /// <summary>
        /// Units that require the Update method in a monobehaviour to function
        /// </summary>
        private static readonly HashSet<Type> _specialUnitTypes = new HashSet<Type> {
            typeof(Timer),
            typeof(Cooldown),
#if PACKAGE_INPUT_SYSTEM_EXISTS
            typeof(OnInputSystemEventButton),
            typeof(OnInputSystemEventVector2),
            typeof(OnInputSystemEventFloat)
#endif
        };

        private readonly HashSet<Unit> _specialUnits = new HashSet<Unit>();
        private readonly Dictionary<Type, NodeGenerator> _generatorCache = new Dictionary<Type, NodeGenerator>(32);
        private readonly HashSet<string> _processedFields = new HashSet<string>();
        private readonly HashSet<string> _processedMethods = new HashSet<string>();
        private readonly List<string> _usingsCache = new List<string>(100);
        private ControlGenerationData data;
        protected override TypeGenerator OnGenerateType(ref string output, NamespaceGenerator @namespace)
        {
            _specialUnits.Clear();
            _processedFields.Clear();
            _processedMethods.Clear();

            if (Data == null)
                return ClassGenerator.Class(RootAccessModifier.Public, ClassModifier.None, "", null);
            CreateGenerationData();
            string className = Data.title.LegalMemberName();
            Type baseType = Data.scriptableObject
                ? typeof(ScriptableObject)
                : (Data.inheritsType && Data.inherits.type != null ? Data.GetInheritedType() : typeof(object));

            var @class = ClassGenerator.Class(RootAccessModifier.Public, Data.classModifier, className, baseType);
            @class.beforeUsings = "#pragma warning disable\n".ConstructHighlight();

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

            if (Data.inheritsType && Data.inherits.type != null && Data.classModifier != ClassModifier.Static && Data.classModifier != ClassModifier.StaticPartial)
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
                        if (param.modifier == Libraries.CSharp.ParameterModifier.Params)
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
                foreach (var item in attribute.fields)
                {
                    attrGenerator.AddParameter(item.Key, item.Value);
                }
                @class.AddAttribute(attrGenerator);
            }

            foreach (var @interface in Data.interfaces)
            {
                @class.ImplementInterface(@interface.type);
            }

            foreach (var constructorData in Data.constructors)
            {
                var parameters = constructorData.parameters;
                if (@class.constructors.Any(f => f.parameters.Select(param => param.generator.type).SequenceEqual(parameters.Select(param => param.type))))
                {
                    continue;
                }

                var constructor = ConstructorGenerator.Constructor(constructorData.scope, constructorData.modifier, constructorData.initializerType, className);

                if (constructorData.graph.units.Count == 0) continue;
                @class.AddUsings(ProcessGraphUnits(constructorData.graph, @class, constructorData.GetReference()));
                data.NewScope();
                data.SetExpectedType(typeof(void));
                data.SetGraphPointer(constructorData.GetReference().AsReference());
                constructor.Body((constructorData.graph.units[0] as Unit).GenerateControl(null, data, 0));
                data.ExitScope();
                foreach (var param in parameters)
                {
                    param.showInitalizer = true;
                    if (!string.IsNullOrEmpty(param.name))
                    {
                        constructor.AddParameter(param.useInInitializer, CreateParameter(param));
                    }
                }

                @class.AddConstructor(constructor);
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
                    if (methodData.graph.units.Count == 0) continue;
                    var method = MethodGenerator.Method(methodData.scope, methodData.modifier, methodData.returnType, methodData.name);
                    method.AddGenerics(methodData.genericParameters.ToArray());
                    AddMethodAttributes(method, methodData);

                    data.NewScope();
                    data.SetExpectedType(methodData.returnType);
                    data.SetGraphPointer(methodData.GetReference().AsReference());
                    @class.AddUsings(ProcessGraphUnits(methodData.graph, @class, methodData.GetReference()));
                    var unit = methodData.graph.units[0] as FunctionNode;
                    method.Body(unit.GenerateControl(null, data, 0));
                    data.ExitScope();
                    foreach (var param in methodData.parameters)
                    {
                        if (!string.IsNullOrEmpty(param.name))
                        {
                            method.AddParameter(CreateParameter(param));
                        }
                    }

                    @class.AddMethod(method);
                }
            }

            if (_specialUnits.Count > 0)
            {
                data.NewScope();
                bool addedSpecialUpdatedCode = false;
#if PACKAGE_INPUT_SYSTEM_EXISTS
                bool addedSpecialFixedUpdatedCode = false;
#endif
                HashSet<Unit> visited = new HashSet<Unit>();
                if (!addedSpecialUpdatedCode && @class.methods.Any(m => m.name.Replace(" ", "") == "Update"))
                {
                    addedSpecialUpdatedCode = true;
                    var method = @class.methods.First(m => m.name.Replace(" ", "") == "Update");
                    if (Data.inheritsType && typeof(MonoBehaviour).IsAssignableFrom(Data.GetInheritedType()))
                    {
                        if (!string.IsNullOrEmpty(method.body))
                            method.beforeBody += string.Join("\n", _specialUnits.Select(unit => CodeUtility.MakeClickable(unit, (unit.GetGenerator() as VariableNodeGenerator)?.Name.VariableHighlight() + ".Update();")).ToArray());
                        else
                            method.AddToBody(string.Join("\n", _specialUnits.Select(unit => CodeUtility.MakeClickable(unit, (unit.GetGenerator() as VariableNodeGenerator)?.Name.VariableHighlight() + ".Update();")).ToArray()));
                    }
#if PACKAGE_INPUT_SYSTEM_EXISTS
                    if (UnityEngine.InputSystem.InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate && Data.inheritsType && typeof(MonoBehaviour).IsAssignableFrom(Data.GetInheritedType()))
                    {
                        foreach (var unit in _specialUnits.Where(unit => unit is OnInputSystemEvent).Cast<OnInputSystemEvent>())
                        {
                            if (!unit.trigger.hasValidConnection) continue;
                            method.beforeBody += CodeBuilder.Indent(2) + CodeUtility.MakeClickable(unit, MethodNodeGenerator.GetSingleDecorator<MethodNodeGenerator>(unit, unit).Name + "();") + "\n";
                        }
                    }
#endif
                }
                else if (!addedSpecialUpdatedCode)
                {
                    addedSpecialUpdatedCode = true;
                    var method = MethodGenerator.Method(AccessModifier.None, MethodModifier.None, typeof(void), "Update");
                    if (Data.inheritsType && typeof(MonoBehaviour).IsAssignableFrom(Data.GetInheritedType()))
                    {
                        method.AddToBody(string.Join("\n", _specialUnits.Select(unit => CodeUtility.MakeClickable(unit, (unit.GetGenerator() as VariableNodeGenerator)?.Name.VariableHighlight() + ".Update();")).ToArray()));
                    }

#if PACKAGE_INPUT_SYSTEM_EXISTS
                    if (UnityEngine.InputSystem.InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate && Data.inheritsType && typeof(MonoBehaviour).IsAssignableFrom(Data.GetInheritedType()))
                    {
                        foreach (var unit in _specialUnits.Where(unit => unit is OnInputSystemEvent).Cast<OnInputSystemEvent>())
                        {
                            if (!unit.trigger.hasValidConnection) continue;
                            method.AddToBody(CodeBuilder.Indent(2) + CodeUtility.MakeClickable(unit, MethodNodeGenerator.GetSingleDecorator<MethodNodeGenerator>(unit, unit).Name + "();") + "\n");
                        }
                    }
#endif
                    @class.AddMethod(method);
                }
#if PACKAGE_INPUT_SYSTEM_EXISTS
                if (UnityEngine.InputSystem.InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate && Data.inheritsType && typeof(MonoBehaviour).IsAssignableFrom(Data.GetInheritedType()))
                {
                    if (!addedSpecialFixedUpdatedCode && @class.methods.Any(m => m.name.Replace(" ", "") == "FixedUpdate"))
                    {
                        addedSpecialFixedUpdatedCode = true;
                        var method = @class.methods.First(m => m.name.Replace(" ", "") == "FixedUpdate");
                        foreach (var unit in _specialUnits.Where(unit => unit is OnInputSystemEvent).Cast<OnInputSystemEvent>())
                        {
                            if (!unit.trigger.hasValidConnection) continue;
                            method.beforeBody += CodeBuilder.Indent(2) + CodeUtility.MakeClickable(unit, unit.GetMethodGenerator()?.Name + "();") + "\n";
                        }
                    }
                    else if (!addedSpecialFixedUpdatedCode)
                    {
                        addedSpecialFixedUpdatedCode = true;
                        var method = MethodGenerator.Method(AccessModifier.None, MethodModifier.None, typeof(void), "FixedUpdate");
                        foreach (var unit in _specialUnits.Where(unit => unit is OnInputSystemEvent).Cast<OnInputSystemEvent>())
                        {
                            if (!unit.trigger.hasValidConnection) continue;
                            method.beforeBody += CodeBuilder.Indent(2) + CodeUtility.MakeClickable(unit, MethodNodeGenerator.GetSingleDecorator<MethodNodeGenerator>(unit, unit)?.Name + "();") + "\n";
                        }
                        @class.AddMethod(method);
                    }
                }
                data.ExitScope();
            }
#endif
            var values = CodeGeneratorValueUtility.GetAllValues(Data);
            var index = 0;
            foreach (var variable in values)
            {
                var field = FieldGenerator.Field(AccessModifier.Public, FieldModifier.None, variable.Value != null ? variable.Value.GetType() : typeof(UnityEngine.Object), variable.Key.LegalMemberName());
                if (index == 0)
                {
                    var attribute = AttributeGenerator.Attribute(typeof(FoldoutAttribute));
                    attribute.AddParameter("ObjectReferences");
                    field.AddAttribute(attribute);
                }
                else
                {
                    field.AddAttribute(AttributeGenerator.Attribute(typeof(HideInInspector)));
                }

                if (index == values.Count - 1)
                {
                    field.AddAttribute(AttributeGenerator.Attribute(typeof(FoldoutEndAttribute)));
                }
                @class.AddField(field);
                index++;
            }
            @namespace.AddClass(@class);
            return @class;
        }
        
        private List<string> ProcessGraphUnits(FlowGraph graph, ClassGenerator @class, GraphPointer graphPointer)
        {
            _usingsCache.Clear();
            var units = graph.GetUnitsRecursive(Recursion.New(Recursion.defaultMaxDepth));

            foreach (Unit unit in units)
            {
                var generator = _generatorCache.TryGetValue(unit.GetType(), out var cachedGenerator)
                    ? cachedGenerator
                    : NodeGenerator.GetSingleDecorator(unit, unit);

                if (!_generatorCache.ContainsKey(unit.GetType()))
                {
                    _generatorCache[unit.GetType()] = generator;
                }

                HandleOtherGenerators(@class, generator);

                if (_specialUnitTypes.Contains(unit.GetType()))
                {
                    _specialUnits.Add(unit);
                }

                if (!string.IsNullOrEmpty(generator.NameSpaces))
                {
                    var namespaces = generator.NameSpaces.Split(',');
                    foreach (var ns in namespaces)
                    {
                        if (!_usingsCache.Contains(ns))
                        {
                            _usingsCache.Add(ns);
                        }
                    }
                }
            }
            return _usingsCache;
        }

        private void ProcessProperty(ClassFieldDeclaration variableData, ClassGenerator @class)
        {
            var property = PropertyGenerator.Property(variableData.scope, variableData.propertyModifier, variableData.type, variableData.name, variableData.defaultValue != null, variableData.getterScope, variableData.setterScope);
            property.Default(variableData.defaultValue);
            AddAttributesToProperty(property, variableData.attributes);

            // Handle getter
            if (variableData.get)
            {
                data.NewScope();
                data.SetExpectedType(variableData.type);
                data.SetGraphPointer(variableData.getter.GetReference().AsReference());
                @class.AddUsings(ProcessGraphUnits(variableData.getter.graph, @class, variableData.getter.GetReference()));
                property.MultiStatementGetter(variableData.getterScope, (variableData.getter.graph.units[0] as Unit).GenerateControl(null, data, 0));
                data.ExitScope();
            }

            // Handle setter
            if (variableData.set)
            {
                data.NewScope();
                data.SetExpectedType(variableData.type);
                data.SetGraphPointer(variableData.setter.GetReference().AsReference());
                @class.AddUsings(ProcessGraphUnits(variableData.setter.graph, @class, variableData.setter.GetReference()));
                property.MultiStatementSetter(variableData.setterScope, (variableData.setter.graph.units[0] as Unit).GenerateControl(null, data, 0));
                data.ExitScope();
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
                AddFieldParametersToAttribute(attrGenerator, attribute.fields);
                property.AddAttribute(attrGenerator);
            }
        }

        private void AddAttributesToField(FieldGenerator field, List<AttributeDeclaration> attributes)
        {
            foreach (var attribute in attributes)
            {
                var attrGenerator = AttributeGenerator.Attribute(attribute.GetAttributeType());
                AddParametersToAttribute(attrGenerator, attribute.parameters);
                AddFieldParametersToAttribute(attrGenerator, attribute.fields);
                field.AddAttribute(attrGenerator);
            }
        }

        private void AddFieldParametersToAttribute(AttributeGenerator attrGenerator, Dictionary<string, object> fields)
        {
            foreach (var item in fields)
            {
                attrGenerator.AddParameter(item.Key, item.Value);
            }
        }

        private void AddParametersToAttribute(AttributeGenerator attrGenerator, List<TypeParam> parameters)
        {
            foreach (var param in parameters)
            {
                if (param.defaultValue is IList list)
                {
                    if (param.modifier == Libraries.CSharp.ParameterModifier.Params)
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
                parameter.defaultValue
            );
        }

        private void CreateGenerationData()
        {
            data = new ControlGenerationData(Data.inheritsType ? Data.GetInheritedType() : typeof(object), null);
            foreach (var variable in Data.variables)
            {
                if (!string.IsNullOrEmpty(variable.FieldName))
                    data.AddLocalNameInScope(variable.FieldName, variable.type);
            }
        }

        private void AddNamespacesToUsings(NodeGenerator generator, List<string> usings)
        {
            if (!string.IsNullOrEmpty(generator.NameSpaces))
            {
                usings.Add(generator.NameSpaces);
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
                AddFieldParametersToAttribute(attrGenerator, attribute.fields);
                method.AddAttribute(attrGenerator);
            }
        }

        private void HandleOtherGenerators(ClassGenerator @class, NodeGenerator generator)
        {
            if (generator is VariableNodeGenerator variableGenerator)
            {
                var existingFields = new HashSet<string>(@class.fields.Select(f => f.name));
                variableGenerator.count = 0;

                while (existingFields.Contains(variableGenerator.Name))
                {
                    variableGenerator.count++;
                }

                @class.AddField(FieldGenerator.Field(variableGenerator.AccessModifier, variableGenerator.FieldModifier, variableGenerator.Type, variableGenerator.Name, variableGenerator.HasDefaultValue ? variableGenerator.DefaultValue : null));
            }
            else if (generator is MethodNodeGenerator methodGenerator && methodGenerator.unit is not IEventUnit)
            {
                var existingMethods = new HashSet<string>(@class.methods.Select(m => m.name));
                methodGenerator.count = 0;

                while (existingMethods.Contains(methodGenerator.Name))
                {
                    methodGenerator.count++;
                }
                data.NewScope();
                foreach (var item in @class.fields)
                {
                    data.AddLocalNameInScope(item.name, item.type);
                }
                var method = MethodGenerator.Method(methodGenerator.AccessModifier, methodGenerator.MethodModifier, methodGenerator.ReturnType, methodGenerator.Name);
                method.AddGenerics(methodGenerator.GenericCount);

                foreach (var param in methodGenerator.Parameters)
                {
                    if (methodGenerator.GenericCount == 0 || !param.usesGeneric)
                        method.AddParameter(ParameterGenerator.Parameter(param.name, param.type, param.modifier));
                    else if (methodGenerator.GenericCount > 0 && param.usesGeneric)
                    {
                        var genericString = method.generics[param.generic].name;
                        method.AddParameter(ParameterGenerator.Parameter(param.name, genericString, param.type, param.modifier));
                    }
                }

                foreach (var variable in Data.variables)
                {
                    methodGenerator.Data.AddLocalNameInScope(variable.FieldName, variable.type);
                }
                methodGenerator.Data = data;
                method.Body(string.IsNullOrEmpty(methodGenerator.MethodBody) ? methodGenerator.GenerateControl(methodGenerator.unit.controlInputs.Count == 0 ? null : methodGenerator.unit.controlInputs[0], data, 0) : methodGenerator.MethodBody); @class.AddMethod(method);
                data.ExitScope();
            }
        }
    }
}