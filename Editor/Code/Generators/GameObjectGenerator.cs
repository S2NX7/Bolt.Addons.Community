using System;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.CSharp;
using System.Linq;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;
using System.Collections;
using Unity.VisualScripting.Community.Utility;
using Mono.Cecil;


#if PACKAGE_INPUT_SYSTEM_EXISTS
using Unity.VisualScripting.InputSystem;
using UnityEngine.InputSystem;
#endif

namespace Unity.VisualScripting.Community
{
    [Serializable]
    [CodeGenerator(typeof(GameObject))]
    public sealed class GameObjectGenerator : CodeGenerator<GameObject>
    {
        #region Static Fields
        private static readonly Dictionary<string, string> EVENT_NAMES = new Dictionary<string, string>()
        {
            { "OnStart", "Start" },
            { "OnUpdate", "Update" },
            { "OnAwake", "Awake" },
            { "OnFixedUpdate", "FixedUpdate" },
            { "OnLateUpdate", "LateUpdate" },
        };

        private static readonly HashSet<Type> UNITY_METHOD_TYPES = new HashSet<Type>
        {
#if MODULE_PHYSICS_EXISTS
            typeof(OnCollisionEnter),
            typeof(OnCollisionExit),
            typeof(OnCollisionStay),
            typeof(OnJointBreak),
            typeof(OnTriggerEnter),
            typeof(OnTriggerExit),
            typeof(OnTriggerStay),
            typeof(OnControllerColliderHit),
            typeof(OnParticleCollision),
#endif
#if MODULE_PHYSICS_2D_EXISTS
            typeof(OnCollisionEnter2D),
            typeof(OnCollisionExit2D),
            typeof(OnCollisionStay2D),
            typeof(OnJointBreak2D),
            typeof(OnTriggerEnter2D),
            typeof(OnTriggerExit2D),
            typeof(OnTriggerStay2D),
#endif
            typeof(OnApplicationFocus),
            typeof(OnApplicationPause),
            typeof(OnApplicationQuit),
            typeof(Start),
            typeof(Update),
            typeof(FixedUpdate),
            typeof(LateUpdate),
            typeof(OnBecameVisible),
            typeof(OnBecameInvisible),
#if PACKAGE_INPUT_SYSTEM_EXISTS
            typeof(OnInputSystemEventButton),
            typeof(OnInputSystemEventVector2),
            typeof(OnInputSystemEventFloat),
#endif
        };
        #endregion

        #region Private Fields
        // Todo: find a better way to handle these
        private Dictionary<CustomEvent, int> _customEventIds;
        private Dictionary<BoltNamedAnimationEvent, int> _namedAnimationEventIds;
        private Dictionary<BoltUnityEvent, int> _unityEventIds;

        private readonly HashSet<Unit> _specialUnits = new HashSet<Unit>();
        private List<Unit> _allUnits;
        private IReadOnlyList<Unit> _eventUnits;
        private HashSet<string> _processedMethodNames = new HashSet<string>();
        private Dictionary<Type, bool> _delegateTypeCache = new Dictionary<Type, bool>();
        private Dictionary<Type, int> _generatorCount = new Dictionary<Type, int>();
        #endregion
        public ScriptMachine[] components = new ScriptMachine[0];
        public ScriptMachine current;

        // Todo: find a better way to handle these
        List<IEventUnit> focusTrueUnits = new List<IEventUnit>();
        List<IEventUnit> focusFalseUnits = new List<IEventUnit>();
        List<IEventUnit> pauseTrueUnits = new List<IEventUnit>();
        List<IEventUnit> pauseFalseUnits = new List<IEventUnit>();

        private ControlGenerationData data;

        public override string Generate(int indent)
        {
            if (Data == null) return "";
            CodeBuilder.Indent(1);
            components = Data != null ? Data.GetComponents<ScriptMachine>() : null;
            if (components == null || components.Length == 0) return string.Empty;

            if (current == null)
            {
                current = components[0];
            }
            if (current == null || current.GetReference() == null) return "";
            var @class = ClassGenerator.Class(RootAccessModifier.Public, ClassModifier.None, $"{(current.nest.graph.title?.Length > 0 ? current.nest.graph.title : Data.name + "_ScriptMachine_" + components.ToList().IndexOf(current))}".LegalMemberName(), typeof(MonoBehaviour));
            if (current.nest.graph.units.Any(u => u is GraphInput || u is GraphOutput))
            {
                @class.beforeUsings = "/* Warning: This graph appears to be a subgraph. Direct generation of subgraphs is not supported. */\n".WarningHighlight();
            }
            data = new ControlGenerationData(typeof(MonoBehaviour), current.GetReference())
            {
                gameObject = Data
            };
            @class.generateUsings = true;
            Initialize();
            AddNamespaces(@class);
            GenerateVariableDeclarations(@class);
            GenerateAwakeHandlers(@class);
            GenerateEventMethods(@class);
            GenerateSpecialUnits(@class);

            return @class.Generate(0);
        }

        private void Initialize()
        {
            _customEventIds = new Dictionary<CustomEvent, int>(2);
            _namedAnimationEventIds = new Dictionary<BoltNamedAnimationEvent, int>(2);
            _unityEventIds = new Dictionary<BoltUnityEvent, int>(2);
            _specialUnits.Clear();
            _processedMethodNames.Clear();
            _delegateTypeCache.Clear();
            _generatorCount.Clear();

            focusTrueUnits.Clear();
            focusFalseUnits.Clear();
            pauseTrueUnits.Clear();
            pauseFalseUnits.Clear();

            _allUnits = current.nest.graph.GetUnitsRecursive(Recursion.New(Recursion.defaultMaxDepth)).Cast<Unit>().ToList();

            _eventUnits = _allUnits
                .Where(unit => (unit is IEventUnit || unit.GetGenerator() is MethodNodeGenerator) && !(unit is CustomEvent evt && evt.graph != current.nest.graph) && unit is not ReturnEvent && unit is not TriggerReturnEvent)
                .ToList()
                .AsReadOnly();

            _specialUnits.UnionWith(_allUnits.Where(u => u is Timer || u is Cooldown));
        }
        private void AddNamespaces(ClassGenerator @class)
        {
            var usings = new List<string> { "Unity", "UnityEngine", "Unity.VisualScripting" };
            Dictionary<Type, int> generatorCount = new Dictionary<Type, int>();
            foreach (Unit unit in _allUnits)
            {
                var generator = unit.GetGenerator();
                if (unit is Timer timer)
                {
                    if (!_generatorCount.ContainsKey(typeof(TimerGenerator)))
                    {
                        _generatorCount[typeof(TimerGenerator)] = 0;
                    }
                    _specialUnits.Add(timer);
                    (generator as TimerGenerator).count = _generatorCount[typeof(TimerGenerator)];
                    _generatorCount[typeof(TimerGenerator)]++;
                }
                else if (unit is Cooldown cooldown)
                {
                    if (!_generatorCount.ContainsKey(typeof(CooldownGenerator)))
                    {
                        _generatorCount[typeof(CooldownGenerator)] = 0;
                    }
                    _specialUnits.Add(cooldown);
                    (generator as CooldownGenerator).count = _generatorCount[typeof(CooldownGenerator)];
                    _generatorCount[typeof(CooldownGenerator)]++;
                }

                if (generator is VariableNodeGenerator variableNodeGenerator)
                {
                    if (!generatorCount.ContainsKey(variableNodeGenerator.GetType()))
                    {
                        generatorCount[variableNodeGenerator.GetType()] = 0;
                    }
                    variableNodeGenerator.count = generatorCount[variableNodeGenerator.GetType()];
                    var field = FieldGenerator.Field(variableNodeGenerator.AccessModifier, variableNodeGenerator.FieldModifier, variableNodeGenerator.Type, variableNodeGenerator.Name);
                    if (variableNodeGenerator.HasDefaultValue)
                        field.CustomDefault(variableNodeGenerator.DefaultValue.As().Code(variableNodeGenerator.IsNew, variableNodeGenerator.Literal, true, "", variableNodeGenerator.NewLineLiteral, true, false));
                    @class.AddField(field);
                    generatorCount[variableNodeGenerator.GetType()]++;

                }

#if PACKAGE_INPUT_SYSTEM_EXISTS && !PACKAGE_INPUT_SYSTEM_1_2_0_OR_NEWER_EXISTS
                else if (unit is OnInputSystemEvent eventUnit && generator is MethodNodeGenerator methodNodeGenerator)
                {
                    if (!eventUnit.trigger.hasValidConnection) continue;
                    var field = FieldGenerator.Field(AccessModifier.Private, FieldModifier.None, typeof(bool), GetInputSystemEventVariableName(unit as OnInputSystemEvent, methodNodeGenerator));
                    @class.AddField(field);
                }
#endif
                if (unit is IEventUnit iEvent)
                {
                    if (iEvent is OnApplicationFocus focusEvent)
                    {
                        focusTrueUnits.Add(focusEvent);
                    }
                    else if (iEvent is OnApplicationLostFocus lostFocusEvent)
                    {
                        focusFalseUnits.Add(lostFocusEvent);
                    }
                    else if (iEvent is OnApplicationPause pauseEvent)
                    {
                        pauseTrueUnits.Add(pauseEvent);
                    }
                    else if (iEvent is OnApplicationResume resumeEvent)
                    {
                        pauseFalseUnits.Add(resumeEvent);
                    }
                    else if (generator is InterfaceNodeGenerator interfaceNodeGenerator)
                    {
                        foreach (var interfaceType in interfaceNodeGenerator.InterfaceTypes)
                        {
                            @class.ImplementInterface(interfaceType);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(generator.NameSpaces))
                {
                    foreach (var ns in generator.NameSpaces.Split(","))
                    {
                        var @namespace = ns.Replace("`", ",").Trim();

                        usings.Add(@namespace);
                    }
                }
            }
            @class.AddUsings(usings);
        }

        private void GenerateVariableDeclarations(ClassGenerator @class)
        {
            var values = CodeGeneratorValueUtility.GetAllValues(Data);
            var index = 0;
            foreach (var variable in values)
            {
                var field = FieldGenerator.Field(AccessModifier.Public, FieldModifier.None, variable.Value != null ? variable.Value.GetType() : typeof(UnityEngine.Object), data.AddLocalNameInScope(variable.Key.LegalMemberName()));
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

            foreach (VariableDeclaration variable in current.nest.graph.variables)
            {
                var type = Type.GetType(variable.typeHandle.Identification);
                type = typeof(IDelegate).IsAssignableFrom(type) ? (variable.value as IDelegate)?.GetDelegateType() ?? (Activator.CreateInstance(type) as IDelegate)?.GetDelegateType() : type;
                var name = data.AddLocalNameInScope(variable.name.LegalMemberName(), type);
                var field = FieldGenerator.Field(AccessModifier.Public, FieldModifier.None, type, name, variable.value);
                @class.AddField(field);
            }
        }

#if PACKAGE_INPUT_SYSTEM_EXISTS && !PACKAGE_INPUT_SYSTEM_1_2_0_OR_NEWER_EXISTS
        private string GetInputSystemEventVariableName(OnInputSystemEvent onInputSystemEvent, MethodNodeGenerator methodNodeGenerator)
        {
            if (onInputSystemEvent is OnInputSystemEventButton)
            {
                return $"button{methodNodeGenerator.count}_wasRunning";
            }
            else if (onInputSystemEvent is OnInputSystemEventFloat)
            {
                return $"float{methodNodeGenerator.count}_wasRunning";
            }
            else
            {
                return $"vector2{methodNodeGenerator.count}_wasRunning";
            }
        }
#endif

        private void GenerateAwakeHandlers(ClassGenerator @class)
        {
            var customEvents = current.nest.graph.units.OfType<CustomEvent>();
            var awakeRequiredUnits = _allUnits.Where(unit => unit.GetGenerator() is AwakeMethodNodeGenerator).ToList();
            if (customEvents.Any() || awakeRequiredUnits.Count > 0)
            {
                var awakeMethod = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), "Awake");
                int id = 0;
                string body = "";
                foreach (CustomEvent eventUnit in customEvents)
                {
                    data.NewScope();
                    data.SetReturns(eventUnit.coroutine ? typeof(IEnumerator) : typeof(void));
                    data.AddLocalNameInScope("args", typeof(CustomEventArgs));

                    _customEventIds.Add(eventUnit, id);
                    id++;
                    body += CodeUtility.MakeClickable(eventUnit, $"{CodeBuilder.Indent(2)}{"CSharpUtility".TypeHighlight()}.RegisterCustomEvent(") + eventUnit.GenerateValue(eventUnit.target) + CodeUtility.MakeClickable(eventUnit, $", " + GetMethodName(eventUnit) + "Runner);");

                    body += "\n";

                    var eventName = GetMethodName(eventUnit) + "Runner";
                    string runnerCode = GetCustomEventRunnerCode(eventUnit, data);
                    var method = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), eventName);
                    method.AddParameter(ParameterGenerator.Parameter("args", typeof(CustomEventArgs), ParameterModifier.None));
                    method.Body(runnerCode);
                    @class.AddMethod(method);
                    data.ExitScope();
                }
                var nodeIDs = new Dictionary<Type, int>();
                foreach (var unit in awakeRequiredUnits)
                {
                    var generator = unit.GetGenerator() as AwakeMethodNodeGenerator;
                    if (nodeIDs.ContainsKey(unit.GetType()))
                    {
                        nodeIDs[unit.GetType()]++;
                        generator.count = nodeIDs[unit.GetType()];
                    }
                    else
                    {
                        nodeIDs.Add(unit.GetType(), 0);
                        generator.count = nodeIDs[unit.GetType()];
                    }
                    if (generator.OutputPort != null && generator.OutputPort.hasValidConnection)
                    {
                        data.NewScope();
                        data.SetReturns(generator.ReturnType);
                        body += generator.GenerateAwakeCode(data, 0) + "\n";
                        data.ExitScope();
                    }
                }
                awakeMethod.Body(body);
                @class.AddMethod(awakeMethod);
            }
        }

        private void GenerateEventMethods(ClassGenerator @class)
        {
            var methodBodies = new Dictionary<string, MethodGenerator>();
            var coroutineBodies = new Dictionary<string, MethodGenerator>();

            bool addedSpecialUpdateCode = false;
            bool addedSpecialFixedUpdateCode = false;

            if (focusTrueUnits.Count > 0 || focusFalseUnits.Count > 0)
            {
                const int indent = 1;
                var method = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), "OnApplicationFocus");
                method.AddParameter(ParameterGenerator.Parameter("focus", typeof(bool), ParameterModifier.None));

                if (focusTrueUnits.Count > 0)
                {
                    data.NewScope();
                    data.SetReturns(typeof(void));
                    string body = string.Join("\n", focusTrueUnits.Select(u => GetMethodBody(u, data, indent)));
                    method.body += $"{"if".ControlHighlight()} ({"focus".VariableHighlight()})\n{{\n{body}\n}}\n";
                    data.ExitScope();
                }

                if (focusFalseUnits.Count > 0)
                {
                    data.NewScope();
                    data.SetReturns(typeof(void));
                    string body = string.Join("\n", focusFalseUnits.Select(u => GetMethodBody(u, data, indent)));
                    method.body += $"{"else".ControlHighlight()}\n{{\n{body}\n}}\n";
                    data.ExitScope();
                }

                @class.AddMethod(method);
            }

            if (pauseTrueUnits.Count > 0 || pauseFalseUnits.Count > 0)
            {
                const int indent = 1;
                var method = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), "OnApplicationPause");
                method.AddParameter(ParameterGenerator.Parameter("paused", typeof(bool), ParameterModifier.None));

                if (pauseTrueUnits.Count > 0)
                {
                    data.NewScope();
                    data.SetReturns(typeof(void));
                    string body = string.Join("\n", pauseTrueUnits.Select(u => GetMethodBody(u, data, indent)));
                    method.body += $"{"if".ControlHighlight()} ({"paused".VariableHighlight()})\n{{\n{body}\n}}\n";
                    data.ExitScope();
                }

                if (pauseFalseUnits.Count > 0)
                {
                    data.NewScope();
                    data.SetReturns(typeof(void));
                    string body = string.Join("\n", pauseFalseUnits.Select(u => GetMethodBody(u, data, indent)));
                    method.body += $"{"else".ControlHighlight()}\n{{\n{body}\n}}\n";
                    data.ExitScope();
                }

                @class.AddMethod(method);
            }

            foreach (Unit Unit in _eventUnits)
            {
                if (Unit is OnApplicationFocus or OnApplicationLostFocus or OnApplicationPause or OnApplicationResume) continue;
                if (Unit is IEventUnit unit)
                {
                    string unityMethodName = GetMethodName(unit);
                    string coroutineMethodName = GetMethodName(unit, true);
                    var parameters = GetMethodParameters(unit);

                    data.NewScope();

                    string specialUnitCode = string.Empty;
                    bool isUpdate = unit is Update;
                    bool isFixedUpdate = unit is FixedUpdate;
                    bool isCoroutine = unit.coroutine;

                    if (isUpdate && !addedSpecialUpdateCode)
                    {
                        addedSpecialUpdateCode = true;
                        specialUnitCode = GenerateSpecialUnitCode(true);
                    }
#if PACKAGE_INPUT_SYSTEM_EXISTS
                    else if (isFixedUpdate && !addedSpecialFixedUpdateCode && UnityEngine.InputSystem.InputSystem.settings.updateMode != UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                    {
                        addedSpecialFixedUpdateCode = true;
                        specialUnitCode = GenerateSpecialUnitCode(false);
                    }
#endif

                    if (isCoroutine)
                    {
                        var generator = Unit.GetMethodGenerator();
                        const int indent = 0;
                        data.SetReturns(typeof(IEnumerator));
                        string coroutineBody = GetMethodBody(unit, data, indent);
                        bool HasReturned = data.HasReturned;
                        if (!coroutineBodies.TryGetValue(coroutineMethodName, out var coroutineMethod))
                        {
                            coroutineMethod = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(IEnumerator), coroutineMethodName);
                            if (!HasReturned) coroutineMethod.SetWarning("Not all code paths return a value");
                            if (generator != null && generator.Parameters.Count > 0)
                            {
                                foreach (var param in generator.Parameters)
                                {
                                    coroutineMethod.AddParameter(ParameterGenerator.Parameter(param.name, param.type, ParameterModifier.None));
                                    data.AddLocalNameInScope(param.name, param.type);
                                }
                            }
                            coroutineBodies[coroutineMethodName] = coroutineMethod;
                        }
                        coroutineMethod.body += coroutineBody;

                        if (!methodBodies.TryGetValue(unityMethodName, out var unityMethod))
                        {
                            unityMethod = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), unityMethodName);
                            if (Unit is BoltNamedAnimationEvent or BoltAnimationEvent or BoltUnityEvent)
                            {
                                unityMethod.SetSummary($"Handles the linked {Unit.GetType().Name} event logic.\nUse this method when assigning the event callback in the Unity Inspector.");
                                if (Unit is BoltNamedAnimationEvent namedAnimationEvent && namedAnimationEvent.name.hasValidConnection)
                                {
                                    unityMethod.SetWarning("Note: Dynamic event names (e.g., connected to variables) are not supported. This will only generate the method for the first resolved name at generation time.");
                                }
                                else if (Unit is BoltUnityEvent unityEvent && unityEvent.name.hasValidConnection)
                                {
                                    unityMethod.SetWarning("Note: Dynamic event names (e.g., connected to variables) are not supported. This will only generate the method for the first resolved name at generation time.");
                                }
                            }
                            foreach (var param in parameters)
                            {
                                unityMethod.AddParameter(ParameterGenerator.Parameter(param.name, param.type, ParameterModifier.None));
                                data.AddLocalNameInScope(param.name, param.type);
                            }
                            methodBodies[unityMethodName] = unityMethod;

                            if (!string.IsNullOrEmpty(specialUnitCode))
                                unityMethod.body += specialUnitCode;
                        }
                        if (Unit is BoltNamedAnimationEvent animationEvent)
                        {
                            var code = animationEvent.CreateClickableString(CodeBuilder.Indent(indent)).Clickable("if ".ControlHighlight()).Parentheses(inside => inside.Clickable($"{"animationEvent".VariableHighlight()}.{"stringParameter".VariableHighlight()} == ").Ignore(animationEvent.GenerateValue(animationEvent.name, data))).NewLine();
                            unityMethod.body += code.Braces(inner => inner.Indent(indent + 1).Clickable($"StartCoroutine({coroutineMethodName}({(generator != null ? string.Join(", ", generator.Parameters.Select(p => p.name.VariableHighlight())) : "")}));"), true, indent).NewLine().Build();
                        }
                        else
                            unityMethod.body += CodeUtility.MakeClickable(unit as Unit, $"StartCoroutine({coroutineMethodName}({(generator != null ? string.Join(", ", generator.Parameters.Select(p => p.name.VariableHighlight())) : "")}));") + "\n";
                    }
                    else
                    {
                        const int indent = 0;
                        data.SetReturns(typeof(void));
                        string body = GetMethodBody(unit, data, indent);

                        if (!methodBodies.TryGetValue(unityMethodName, out var method))
                        {
                            method = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), unityMethodName);
                            if (Unit is BoltNamedAnimationEvent or BoltAnimationEvent or BoltUnityEvent)
                            {
                                method.SetSummary($"Handles the linked {Unit.GetType().Name} event logic.\nUse this method when assigning the event callback in the Unity Inspector.");
                                if (Unit is BoltNamedAnimationEvent namedAnimationEvent && namedAnimationEvent.name.hasValidConnection)
                                {
                                    method.SetWarning("Note: Dynamic event names (e.g., connected to variables) are not supported. This will only generate the method for the first resolved name at generation time.");
                                }
                                else if (Unit is BoltUnityEvent unityEvent && unityEvent.name.hasValidConnection)
                                {
                                    method.SetWarning("Note: Dynamic event names (e.g., connected to variables) are not supported. This will only generate the method for the first resolved name at generation time.");
                                }
                            }
                            foreach (var param in parameters)
                            {
                                method.AddParameter(ParameterGenerator.Parameter(param.name, param.type, ParameterModifier.None));
                            }

                            methodBodies[unityMethodName] = method;

                            if (!string.IsNullOrEmpty(specialUnitCode))
                                method.body += specialUnitCode;
                        }
                        if (Unit is BoltNamedAnimationEvent animationEvent)
                        {
                            var code = animationEvent.CreateClickableString(CodeBuilder.Indent(indent)).Clickable("if ".ControlHighlight()).Parentheses(inside => inside.Clickable($"{"animationEvent".VariableHighlight()}.{"stringParameter".VariableHighlight()} == ").Ignore(animationEvent.GenerateValue(animationEvent.name, data))).NewLine();
                            methodBodies[unityMethodName].body += code.Braces(inner => inner.Ignore(GetMethodBody(unit, data, indent + 1)), true, indent);
                        }
                        else
                            methodBodies[unityMethodName].body += body;
                    }

                    data.ExitScope();
                }
                else if (Unit.GetGenerator() is MethodNodeGenerator methodNodeGenerator)
                {
                    methodNodeGenerator.Data = data;
                    data.SetReturns(methodNodeGenerator.ReturnType);
                    string body = string.IsNullOrEmpty(methodNodeGenerator.MethodBody) ? methodNodeGenerator.GenerateControl(null, data, 0) : methodNodeGenerator.MethodBody;
                    if (!methodBodies.TryGetValue(methodNodeGenerator.Name, out var method))
                    {
                        method = MethodGenerator.Method(methodNodeGenerator.AccessModifier, methodNodeGenerator.MethodModifier, methodNodeGenerator.ReturnType, methodNodeGenerator.Name);
                        method.AddGenerics(methodNodeGenerator.GenericCount);
                        foreach (var param in methodNodeGenerator.Parameters)
                        {
                            if (methodNodeGenerator.GenericCount == 0 || !param.usesGeneric)
                                method.AddParameter(ParameterGenerator.Parameter(param.name, param.type, param.modifier));
                            else if (methodNodeGenerator.GenericCount > 0 && param.usesGeneric)
                            {
                                var genericString = method.generics[param.generic].name;
                                method.AddParameter(ParameterGenerator.Parameter(param.name, genericString.TypeHighlight(), param.type, param.modifier));
                            }
                        }
                        methodBodies[methodNodeGenerator.Name] = method;
                    }

                    methodBodies[methodNodeGenerator.Name].body += body;
                }
            }

            foreach (var method in methodBodies.Values)
                @class.AddMethod(method);

            foreach (var coroutine in coroutineBodies.Values)
                @class.AddMethod(coroutine);
        }

        private string GenerateSpecialUnitCode(bool isUpdate)
        {
            var code = string.Join("\n", _specialUnits.Select(t =>
                CodeBuilder.Indent(2) +
                CodeUtility.MakeClickable(t, t.GetVariableGenerator().Name.VariableHighlight() + ".Update();")))
                + "\n";

#if PACKAGE_INPUT_SYSTEM_EXISTS
            if (isUpdate)
            {
                if (UnityEngine.InputSystem.InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                {
                    foreach (var inputUnit in _allUnits.OfType<OnInputSystemEvent>())
                    {
                        if (!inputUnit.trigger.hasValidConnection) continue;
                        code += CodeUtility.MakeClickable(inputUnit, inputUnit.GetMethodGenerator().Name + "();") + "\n";
                    }
                }
            }
            else
            {
                foreach (var inputUnit in _allUnits.OfType<OnInputSystemEvent>())
                {
                    if (!inputUnit.trigger.hasValidConnection) continue;
                    code += CodeUtility.MakeClickable(inputUnit, inputUnit.GetMethodGenerator().Name + "();") + "\n";
                }
            }
#endif

            return code;
        }

        private void GenerateSpecialUnits(ClassGenerator @class)
        {
            data.NewScope();
            bool hasUpdate = false;
            bool hasFixedUpdate = false;
#if PACKAGE_INPUT_SYSTEM_EXISTS
            bool hasInputSystemNode = false;
#endif

            foreach (var unit in _allUnits)
            {
                if (unit is Update) { hasUpdate = true; continue; }
                if (unit is FixedUpdate) { hasFixedUpdate = true; continue; }
#if PACKAGE_INPUT_SYSTEM_EXISTS
                if (unit is OnInputSystemEvent onInputSystemEvent && onInputSystemEvent.trigger.hasValidConnection) { hasInputSystemNode = true; continue; }
#endif
            }
            if (_specialUnits.Count > 0 && !hasUpdate)
            {
                var update = new Update();
                var updateMethod = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), "Update");

                var specialCode = string.Join("\n", _specialUnits.Select(t =>
                    CodeUtility.MakeClickable(t, t.GetVariableGenerator().Name.VariableHighlight() + ".Update();")));

#if PACKAGE_INPUT_SYSTEM_EXISTS
                if (UnityEngine.InputSystem.InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                {
                    foreach (var unit in _allUnits.OfType<OnInputSystemEvent>())
                    {
                        if (!unit.trigger.hasValidConnection) continue;

                        specialCode += CodeUtility.MakeClickable(unit, unit.GetMethodGenerator().Name + "();") + "\n";
                    }
                }
#endif
                updateMethod.body = specialCode;
                @class.AddMethod(updateMethod);
            }

#if PACKAGE_INPUT_SYSTEM_EXISTS
            else if (UnityEngine.InputSystem.InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate
                     && !hasUpdate
                     && hasInputSystemNode)
            {
                var updateMethod = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), "Update");

                var specialCode = "";
                foreach (var unit in _allUnits.OfType<OnInputSystemEvent>())
                {
                    if (!unit.trigger.hasValidConnection) continue;

                    specialCode += CodeUtility.MakeClickable(unit, unit.GetMethodGenerator().Name + "();") + "\n";
                }

                updateMethod.body = specialCode;
                @class.AddMethod(updateMethod);
            }
            else if (UnityEngine.InputSystem.InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate
                     && !hasFixedUpdate
                     && hasInputSystemNode)
            {
                var fixedMethod = MethodGenerator.Method(AccessModifier.Private, MethodModifier.None, typeof(void), "FixedUpdate");

                var specialCode = "";
                foreach (var unit in _allUnits.OfType<OnInputSystemEvent>())
                {
                    if (!unit.trigger.hasValidConnection) continue;

                    specialCode += CodeUtility.MakeClickable(unit, unit.GetMethodGenerator().Name + "();") + "\n";
                }

                fixedMethod.body = specialCode;
                @class.AddMethod(fixedMethod);
            }
#endif

            data.ExitScope();
        }

        private string GetCustomEventRunnerCode(CustomEvent eventUnit, ControlGenerationData data)
        {
            var output = "";
            output += CodeUtility.MakeClickable(eventUnit, "if ".ControlHighlight() + $"({"args".VariableHighlight()}.{"name".VariableHighlight()} == ") + eventUnit.GenerateValue(eventUnit.name, data) + CodeUtility.MakeClickable(eventUnit, ")") + "\n";
            output += CodeUtility.MakeClickable(eventUnit, "{") + "\n";
            output += CodeBuilder.Indent(1) + (eventUnit.coroutine ? CodeUtility.MakeClickable(eventUnit, $"StartCoroutine(" + GetMethodName(eventUnit) + $"({"args".VariableHighlight()}));") : CodeUtility.MakeClickable(eventUnit, GetMethodName(eventUnit) + $"({"args".VariableHighlight()});"));
            output += "\n" + CodeUtility.MakeClickable(eventUnit, "}") + "\n";
            return output;
        }

        private string GetMethodName(IEventUnit eventUnit, bool getCoroutine = false)
        {
            var UnitTitle = BoltFlowNameUtility.UnitTitle(eventUnit.GetType(), false, false).LegalMemberName();

            string methodName;

            if (EVENT_NAMES.TryGetValue(UnitTitle, out var title))
            {
                methodName = title;
            }
            else
            {
                methodName = UnitTitle;
            }

            if (eventUnit is CustomEvent customEvent)
            {
                if (!customEvent.name.hasValidConnection)
                {
                    methodName = (string)customEvent.defaultValues[customEvent.name.key];
                }
                else
                {
                    if (NodeGenerator.CanPredictConnection(customEvent.name, data))
                    {
                        data.TryGetGraphPointer(out var graphPointer);
                        methodName = Flow.Predict<string>(customEvent.name, graphPointer.AsReference());
                    }
                    else
                        return "CustomEvent" + _customEventIds[customEvent];
                }
            }
            else if (eventUnit is BoltNamedAnimationEvent animationEvent)
            {
                if (NodeGenerator.CanPredictConnection(animationEvent.name, data))
                {
                    data.TryGetGraphPointer(out var graphPointer);
                    methodName = Flow.Predict<string>(animationEvent.name, graphPointer.AsReference()) + "_AnimationEvent";
                }
                else if (!animationEvent.name.hasValidConnection)
                    methodName = animationEvent.defaultValues[animationEvent.name.key] as string + "_AnimationEvent";
                else
                {
                    if (!_namedAnimationEventIds.TryGetValue(animationEvent, out int count))
                    {
                        count = 0;
                    }
                    _namedAnimationEventIds[animationEvent] = count + 1;
                    methodName = "AnimationEvent" + _namedAnimationEventIds[animationEvent];
                }
                return methodName + (getCoroutine && eventUnit.coroutine ? "_Coroutine" : "");
            }
            else if (eventUnit is BoltUnityEvent unityEvent)
            {
                if (NodeGenerator.CanPredictConnection(unityEvent.name, data))
                {
                    data.TryGetGraphPointer(out var graphPointer);
                    methodName = Flow.Predict<string>(unityEvent.name, graphPointer.AsReference()) + "_UnityEvent";
                }
                else if (!unityEvent.name.hasValidConnection)
                    methodName = unityEvent.defaultValues[unityEvent.name.key] as string + "_UnityEvent";
                else
                {
                    if (!_unityEventIds.TryGetValue(unityEvent, out int count))
                    {
                        count = 0;
                    }
                    _unityEventIds[unityEvent] = count + 1;
                    methodName = "UnityEvent" + _unityEventIds[unityEvent];
                }
                return methodName + (getCoroutine && eventUnit.coroutine ? "_Coroutine" : "");
            }
            else if ((eventUnit as Unit).GetMethodGenerator(false) is MethodNodeGenerator methodNodeGenerator)
            {
                return methodNodeGenerator.Name + (getCoroutine && eventUnit.coroutine && UNITY_METHOD_TYPES.Contains(eventUnit.GetType()) ? "_Coroutine" : "");
            }

            return methodName;
        }

        private string GetMethodBody(IEventUnit eventUnit, ControlGenerationData data, int indent)
        {
            var variablesCode = "";
            foreach (var variable in eventUnit.graph.variables)
            {
                if (!data.ContainsNameInAnyScope(variable.name))
                {
                    variablesCode += CodeUtility.MakeClickable(eventUnit as Unit, Type.GetType(variable.typeHandle.Identification).As().CSharpName(false, true) + " " + variable.name.LegalMemberName().VariableHighlight() + (variable.value != null ? $" = " + "" + $"{variable.value.As().Code(true, true, true)}" : string.Empty) + ";") + "\n";
                    data.AddLocalNameInScope(variable.name, Type.GetType(variable.typeHandle.Identification) ?? typeof(object));
                }
            }
            var methodBody = variablesCode + (eventUnit as Unit).GenerateControl(null, data, indent);

            return methodBody;
        }

        private List<TypeParam> GetMethodParameters(IEventUnit eventUnit)
        {
            return (eventUnit as Unit).GetMethodGenerator(false)?.Parameters ?? new List<TypeParam>();
        }
    }
}
