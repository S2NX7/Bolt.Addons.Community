﻿using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.VisualScripting.Community
{
    public sealed class ControlGenerationData
    {
        private readonly Stack<GeneratorScope> scopes = new();
        private readonly Stack<GeneratorScope> preservedScopes = new();
        private readonly Stack<(Type type, bool isMet)> expectedTypes = new();
        public readonly Dictionary<object, object> generatorData = new();
        private readonly Dictionary<Unit, UnitSymbol> unitSymbols = new();

        private GraphPointer graphPointer;
        public GameObject gameObject { get; set; }
        public Type ScriptType { get; private set; } = typeof(object);
        #region Expected Types
        public Type GetExpectedType()
        {
            if (expectedTypes.Count > 0)
            {
                return expectedTypes.Peek().type;
            }
            else return null;
        }

        public bool IsCurrentExpectedTypeMet()
        {
            if (expectedTypes.Count > 0)
            {
                return expectedTypes.Peek().isMet;
            }
            else return false;
        }

        public bool SetCurrentExpectedTypeMet(bool isMet, Type metAs)
        {
            if (expectedTypes.Count > 0)
            {
                var type = metAs ?? expectedTypes.Peek().type;
                var currentExpectedType = expectedTypes.Pop();
                currentExpectedType.isMet = isMet;
                currentExpectedType.type = type;
                expectedTypes.Push(currentExpectedType);
                return isMet;
            }
            else
            {
                return false;
            }
        }

        public void SetExpectedType(Type type)
        {
            if (type != null)
            {
                expectedTypes.Push((type, false));
            }
        }

        public (Type type, bool isMet) RemoveExpectedType()
        {
            if (expectedTypes.Count > 0)
                return expectedTypes.Pop();
            return (typeof(object), false);
        }
        #endregion

        public string AddLocalNameInScope(string name, Type type = null)
        {
            if (scopes.Count > 0)
            {
                string newName = name;
                int count = 0;

                var scope = PeekScope();

                while (ContainsNameInAnyScope(newName))
                {
                    count++;
                    newName = name + count;
                }

                scope.scopeVariables.Add(newName, type);
                scope.nameMapping[name] = newName;

                return newName;
            }
            else
            {
                NewScope();
                return AddLocalNameInScope(name, type);
            }
        }
        #region Scope Management
        public void NewScope()
        {
            var parent = PeekScope();
            var newScope = new GeneratorScope(parent);

            if (parent != null)
            {
                newScope.MustReturn = parent.MustReturn;
                newScope.MustBreak = parent.MustBreak;
                newScope.Returns = parent.Returns;
            }

            scopes.Push(newScope);
        }


        public GeneratorScope ExitScope()
        {
            var exitingScope = scopes.Pop();
            preservedScopes.Push(exitingScope);

            if (exitingScope.ParentScope != null)
            {
                exitingScope.ParentScope.HasReturned = exitingScope.HasReturned;
                exitingScope.ParentScope.HasBroke = exitingScope.HasBroke;
            }

            return exitingScope;
        }

        public GeneratorScope PeekScope()
        {
            if (scopes.Count > 0)
                return scopes.Peek();
            else
                return null;
        }

        public Type Returns => PeekScope()?.Returns ?? typeof(void);
        public bool MustReturn => PeekScope()?.MustReturn ?? false;
        public bool HasReturned => PeekScope()?.HasReturned ?? false;
        public bool MustBreak => PeekScope()?.MustBreak ?? false;
        public bool HasBroke => PeekScope()?.HasBroke ?? false;

        public void SetMustReturn(bool value)
        {
            var scope = PeekScope();
            if (scope != null) scope.MustReturn = value;
        }
        public void SetHasReturned(bool value)
        {
            var scope = PeekScope();
            if (scope != null) scope.HasReturned = value;
        }
        public void SetMustBreak(bool value)
        {
            var scope = PeekScope();
            if (scope != null) scope.MustBreak = value;
        }
        public void SetHasBroke(bool value)
        {
            var scope = PeekScope();
            if (scope != null) scope.HasBroke = value;
        }
        public void SetReturns(Type type)
        {
            var scope = PeekScope();
            if (scope != null) scope.Returns = type;
            SetMustReturn(!type.Is().Void());
        }

        public bool ContainsNameInAnyScope(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var scope in scopes)
            {
                if (scope.scopeVariables.ContainsKey(name)) return true;
            }
            foreach (var preservedScope in preservedScopes)
            {
                if (preservedScope.scopeVariables.ContainsKey(name)) return true;
            }
            return false;
        }
        #endregion

        # region Variable Management
        public string GetVariableName(string name)
        {
            foreach (var scope in scopes)
            {
                if (scope.nameMapping.TryGetValue(name, out string variableName))
                {
                    return variableName;
                }
            }

            foreach (var preservedScope in preservedScopes)
            {
                if (preservedScope.nameMapping.TryGetValue(name, out string variableName))
                {
                    return variableName;
                }
            }

            return name;
        }


        public Type GetVariableType(string name)
        {
            foreach (var scope in scopes)
            {
                if (scope.scopeVariables.TryGetValue(name, out var type))
                {
                    return type;
                }
            }

            return null;
        }

        public bool TryGetVariableType(string name, out Type type)
        {
            if (GetVariableType(name) == null)
            {
                type = null;
                return false;
            }
            type = GetVariableType(name);
            return true;
        }
        #endregion

        #region Symbol Management
        public void CreateSymbol(Unit unit, Type Type, Dictionary<string, object> Metadata = null)
        {
            if (!unitSymbols.ContainsKey(unit))
            {
                unitSymbols.Add(unit, new UnitSymbol(unit, Type, Metadata));
            }
        }

        public bool TryGetSymbol(Unit unit, out UnitSymbol symbol)
        {
            if (unit == null)
            {
                symbol = null;
                return false;
            }
            return unitSymbols.TryGetValue(unit, out symbol);
        }

        public void SetSymbolType(Unit unit, Type type)
        {
            if (unitSymbols.TryGetValue(unit, out var symbol))
            {
                symbol.Type = type;
            }
            else
                throw new MissingReferenceException($"No symbol found for {unit}");
        }
        #endregion

        public bool TryGetGameObject(out GameObject gameObject)
        {
            if (this.gameObject != null)
            {
                gameObject = this.gameObject;
                return true;
            }
            gameObject = null;
            return false;
        }

        public bool TryGetGraphPointer(out GraphPointer graphPointer)
        {
            if (this.graphPointer != null)
            {
                graphPointer = this.graphPointer;
                return true;
            }
            graphPointer = null;
            return false;
        }

        public void SetGraphPointer(GraphReference graphReference)
        {
            // This should only happen if this is not a scriptmachine
            // It's set so that Scene variables are predicatable in assets
            // because GraphPointer.scene uses GameObject.scene
            if (gameObject == null && graphReference != null && graphReference.gameObject == null)
            {
                var firstObject = SceneManager.GetActiveScene().GetRootGameObjects()[0];
                var target = firstObject.IsUnityNull() ? new GameObject("C# Preview Placeholder") : firstObject;
                typeof(GraphPointer).GetProperty("gameObject").SetValue(graphReference, target);
            }
            graphPointer = graphReference;
        }

        public ControlGenerationData(Type ScriptType, GraphPointer graphPointer)
        {
            this.ScriptType = ScriptType;
            this.graphPointer = graphPointer;
        }

        public sealed class GeneratorScope
        {
            public Dictionary<string, Type> scopeVariables { get; private set; }
            public Dictionary<string, string> nameMapping { get; private set; }
            public GeneratorScope ParentScope { get; private set; }

            public Type Returns { get; set; } = typeof(void);
            public bool MustBreak { get; set; }
            public bool HasBroke { get; set; }
            public bool MustReturn { get; set; }
            public bool HasReturned { get; set; }

            public GeneratorScope(GeneratorScope parentScope)
            {
                scopeVariables = new();
                nameMapping = new();
                ParentScope = parentScope;
            }
        }
    }
}