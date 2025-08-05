using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;
using Unity.VisualScripting.Community.Libraries.Humility;
using Unity.VisualScripting.Community.Libraries.CSharp;
using UnityEngine.SceneManagement;

namespace Unity.VisualScripting.Community
{

    public interface IGraphProvider
    {
        string Name { get; }
        bool IsEnabled { get; }
        IEnumerable<(GraphReference, IGraphElement)> GetElements();
        void HandleMatch(NodeFinderWindow.MatchObject match);
        Object GetAssetForElement(GraphReference reference);
    }

    public interface IMatchHandler
    {
        bool CanHandle(IGraphElement element);
        NodeFinderWindow.MatchObject HandleMatch(IGraphElement element, string pattern, NodeFinderWindow.SearchMode searchMode);
    }
    public class NodeFinderWindow : EditorWindow
    {
        public enum SearchMode
        {
            Relevant,
            StartsWith
        }
        public abstract class BaseGraphProvider : IGraphProvider
        {
            protected readonly NodeFinderWindow _window;
            public abstract string Name { get; }
            private bool _isEnabled = true;
            public virtual bool IsEnabled => _isEnabled;

            protected BaseGraphProvider(NodeFinderWindow window)
            {
                _window = window;
            }

            public abstract IEnumerable<(GraphReference, IGraphElement)> GetElements();
            public abstract void HandleMatch(MatchObject match);

            public virtual void SetEnabled(bool enabled)
            {
                _isEnabled = enabled;
            }

            protected Dictionary<Object, List<MatchObject>> MatchMap { get; } = new();
            protected List<Object> SortedKeys { get; private set; } = new();

            public virtual void ClearResults()
            {
                MatchMap.Clear();
                SortedKeys.Clear();
            }

            public virtual void AddMatch(MatchObject match, Object key)
            {
                if (!MatchMap.TryGetValue(key, out var list))
                {
                    list = new List<MatchObject>();
                    MatchMap[key] = list;
                    SortedKeys.Add(key);
                }

                if (!list.Any(m => m.Unit == match.Unit))
                {
                    list.Add(match);
                }
                else
                {
                    list[list.IndexOf(list.First(m => m.Unit == match.Unit))] = match;
                }
            }

            public virtual IEnumerable<(Object key, List<MatchObject> matches)> GetResults()
            {
                SortedKeys.Sort((a, b) => string.Compare(GetSortKey(a), GetSortKey(b), StringComparison.Ordinal));
                return SortedKeys.Select(key => (key, MatchMap[key]));
            }

            protected virtual string GetSortKey(Object key)
            {
                return key.name;
            }

            public virtual Object GetAssetForElement(GraphReference reference)
            {
                return reference?.rootObject;
            }
        }

        private readonly Dictionary<Type, IGraphProvider> _graphProviders = new();
        private readonly Dictionary<Type, IMatchHandler> _matchHandlers = new();
        private readonly Dictionary<Type, List<MatchObject>> _matchMap = new();
        private readonly List<MatchObject> _matchObjects = new();

        private float _lastSearchTime;
        private const float SearchCooldown = 0.5f;


        private string _pattern = "";
        private string _previousPattern = "";
        private Vector2 _scrollViewRoot;
        private bool _matchError = true;
        private float _lastErrorCheckTime;
        private const float ErrorCheckInterval = 1.0f;


        private class FilterOption
        {
            public string Label { get; set; }
            public bool IsEnabled { get; set; }
            public Action<bool> OnToggled { get; set; }
            public bool RequiresSearch { get; set; }
            public Type ProviderType { get; set; }
        }

        private readonly List<FilterOption> _filters = new();

        private bool _needsSearch = false;

        private bool _showProviderFilters = true;
        private bool _showTypeFilters = true;
        private bool _showSpecialFilters = true;
        private Dictionary<MatchType, bool> _typeFilters = new();
        private SearchMode _searchMode;

        [MenuItem("Window/Community Addons/Node Finder")]
        public static void Open()
        {
            var window = GetWindow<NodeFinderWindow>();


            GUIContent searchIconContent = EditorGUIUtility.IconContent("d_ViewToolZoom");
            window.titleContent = new GUIContent("Node Finder", searchIconContent.image);
        }

        private void OnDisable()
        {
            _matchObjects.Clear();
            _matchMap.Clear();
        }

        private void OnEnable()
        {
            RegisterDefaultProviders();
            RegisterDefaultHandlers();
            InitializeFilters();
            InitializeTypeFilters();
            Search();
        }

        private void RegisterDefaultProviders()
        {
            RegisterProvider(new ScriptGraphProvider(this));
            RegisterProvider(new StateGraphProvider(this));
            RegisterProvider(new ClassAssetProvider(this));
            RegisterProvider(new StructAssetProvider(this));
            RegisterProvider(new ScriptMachineProvider(this));
            RegisterProvider(new StateMachineProvider(this));
        }

        private void RegisterDefaultHandlers()
        {
            RegisterHandler(new UnitMatchHandler());
            RegisterHandler(new GroupMatchHandler());
#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
            RegisterHandler(new StickyNoteMatchHandler());
#endif
            RegisterHandler(new CommentsMatchHandler());
            RegisterHandler(new ErrorMatchHandler());
        }

        private void RegisterProvider<T>(T provider) where T : IGraphProvider
        {
            _graphProviders[provider.GetType()] = provider;
        }

        private void RegisterHandler<T>(T handler) where T : IMatchHandler
        {
            _matchHandlers[handler.GetType()] = handler;
        }

        private void OnGUI()
        {
            Event e = Event.current;
            DrawSearchBar();
            GUILayout.Space(6);
            DrawFilters();
            DrawSeparator();
            if (e.keyCode == KeyCode.Return || _pattern != _previousPattern)
            {
                _needsSearch = true;
            }

            if (_needsSearch)
            {
                Search();
                _previousPattern = _pattern;
                _needsSearch = false;
            }
            DrawResults();
            if (_matchError && Time.realtimeSinceStartup - _lastErrorCheckTime >= ErrorCheckInterval)
            {
                _needsSearch = false;
                SearchForErrors();
                _lastErrorCheckTime = Time.realtimeSinceStartup;
                Repaint();
            }
        }

        private void DrawSearchBar()
        {
            var findLabelStyle = new GUIStyle(LudiqStyles.toolbarLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            HUMEditor.Horizontal().Box(HUMEditorColor.DefaultEditorBackground.Darken(0.1f), Color.black, new RectOffset(0, 0, 0, 0), new RectOffset(1, 1, 1, 1), () =>
            {
                HUMEditor.Horizontal().Box(HUMEditorColor.DefaultEditorBackground, Color.black, 7, () =>
                {
                    EditorGUILayout.LabelField("Find:", findLabelStyle, GUILayout.Width(40));
                    _pattern = EditorGUILayout.TextField(_pattern, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
                    GUILayout.Space(8);
                    _searchMode = (SearchMode)EditorGUILayout.EnumPopup(_searchMode, GUILayout.Width(80));
                }, false, false);
            });
        }

        private void InitializeFilters()
        {
            _filters.Clear();

            foreach (var provider in _graphProviders.Values)
            {
                AddFilter(provider.Name,
                    () => provider.IsEnabled,
                    (enabled) =>
                    {
                        if (provider is BaseGraphProvider baseProvider)
                        {
                            baseProvider.SetEnabled(enabled);
                            if (enabled) Search();
                        }
                    },
                    true,
                    provider.GetType());
            }


            AddFilter("Errors",
                () => _matchError,
                (enabled) =>
                {
                    _matchError = enabled;
                    if (enabled) SearchForErrors();
                });
        }

        private void InitializeTypeFilters()
        {
            foreach (MatchType type in Enum.GetValues(typeof(MatchType)))
            {
                if (!_typeFilters.ContainsKey(type))
                {
                    _typeFilters[type] = true;
                }
            }
        }

        public void AddFilter(string label, Func<bool> getter, Action<bool> onToggled, bool requiresSearch = true, Type providerType = null)
        {
            _filters.Add(new FilterOption
            {
                Label = label,
                IsEnabled = getter(),
                OnToggled = onToggled,
                RequiresSearch = requiresSearch,
                ProviderType = providerType
            });
        }

        private void DrawFilters()
        {
            var filterLabelStyle = new GUIStyle(LudiqStyles.toolbarLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };

            var foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            HUMEditor.Vertical().Box(HUMEditorColor.DefaultEditorBackground.Darken(0.1f), Color.black, new RectOffset(0, 0, 0, 0), new RectOffset(1, 1, 1, 1), () =>
            {
                HUMEditor.Vertical().Box(HUMEditorColor.DefaultEditorBackground.Darken(0.1f), Color.black, new RectOffset(2, 2, 2, 2), new RectOffset(1, 1, 1, 1), () =>
                {

                    _showProviderFilters = EditorGUILayout.Foldout(_showProviderFilters, "Provider Filters", true, foldoutStyle);
                    if (_showProviderFilters)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginHorizontal();
                        var providerFilters = _filters.Where(f => f.ProviderType != null).ToList();
                        for (int i = 0; i < providerFilters.Count; i++)
                        {
                            DrawFilterToggle(providerFilters[i]);
                            GUILayout.Space(4);
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUI.indentLevel--;
                    }
                });
                DrawSeparator();
                HUMEditor.Vertical().Box(HUMEditorColor.DefaultEditorBackground.Darken(0.1f), Color.black, new RectOffset(2, 2, 2, 2), new RectOffset(1, 1, 1, 1), () =>
                {

                    _showTypeFilters = EditorGUILayout.Foldout(_showTypeFilters, "Type Filters", true, foldoutStyle);
                    if (_showTypeFilters)
                    {
                        EditorGUI.indentLevel++;

                        EditorGUILayout.BeginHorizontal();
                        var types = Enum.GetValues(typeof(MatchType)).Cast<MatchType>().ToList();
                        for (int i = 0; i < types.Count; i++)
                        {
                            DrawTypeFilterToggle(types[i]);
                            GUILayout.Space(4);

                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUI.indentLevel--;
                    }
                });
                DrawSeparator();
                HUMEditor.Vertical().Box(HUMEditorColor.DefaultEditorBackground.Darken(0.1f), Color.black, new RectOffset(2, 2, 2, 2), new RectOffset(1, 1, 1, 1), () =>
                {

                    _showSpecialFilters = EditorGUILayout.Foldout(_showSpecialFilters, "Special Filters", true, foldoutStyle);
                    if (_showSpecialFilters)
                    {
                        var specialFilters = _filters.Where(f => f.ProviderType == null).ToList();
                        if (specialFilters.Any())
                        {
                            EditorGUILayout.BeginHorizontal();
                            foreach (var filter in specialFilters)
                            {
                                DrawFilterToggle(filter);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                });
            });
        }

        private void DrawSeparator()
        {
            GUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, Color.gray);
            GUILayout.Space(4);
        }


        private void DrawFilterToggle(FilterOption filter)
        {
            bool previousState = filter.IsEnabled;
            filter.IsEnabled = GUILayout.Toggle(filter.IsEnabled, filter.Label, EditorStyles.toolbarButton);

            if (filter.IsEnabled != previousState)
            {
                filter.OnToggled?.Invoke(filter.IsEnabled);
                if (filter.RequiresSearch)
                {
                    Search();
                }
            }
        }

        private void DrawTypeFilterToggle(MatchType type)
        {
            bool previousState = _typeFilters[type];
            _typeFilters[type] = GUILayout.Toggle(previousState, type.ToString(), EditorStyles.toolbarButton);

            if (_typeFilters[type] != previousState)
            {
                Search();
            }
        }

        private void DrawResults()
        {
            HUMEditor.Vertical().Box(HUMEditorColor.DefaultEditorBackground.Darken(0.1f), Color.black, new RectOffset(0, 0, 0, 0), new RectOffset(1, 1, 1, 1), () =>
            {
                _scrollViewRoot = EditorGUILayout.BeginScrollView(_scrollViewRoot);

                bool empty = string.IsNullOrEmpty(_pattern) || _matchObjects.Count == 0;
                bool isShowingErrors = false;

                if (!empty)
                {
                    EditorGUILayout.LabelField($"Total Results: {_matchObjects.Count}", EditorStyles.boldLabel);

                    foreach (var provider in _graphProviders.Values.Where(p => p.IsEnabled))
                    {
                        foreach (var (key, matches) in (provider as BaseGraphProvider)?.GetResults() ?? Enumerable.Empty<(Object, List<MatchObject>)>())
                        {
                            if (!ShouldShowItem(matches)) continue;
                            DrawResultGroup(key, matches, ref isShowingErrors);
                        }
                    }
                }
                else if (_matchError)
                {
                    foreach (var provider in _graphProviders.Values.Where(p => p.IsEnabled))
                    {
                        foreach (var (key, matches) in (provider as BaseGraphProvider)?.GetResults() ?? Enumerable.Empty<(Object, List<MatchObject>)>())
                        {
                            if (!IsError(matches)) continue;
                            isShowingErrors = true;
                            DrawResultGroup(key, matches, ref isShowingErrors, true);
                        }
                    }
                }

                if (empty && !isShowingErrors)
                {
                    EditorGUILayout.HelpBox("No results found.", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            });
        }

        private void DrawResultGroup(Object key, List<MatchObject> matches, ref bool isShowingErrors, bool errorsOnly = false)
        {
            EditorGUIUtility.SetIconSize(new Vector2(16, 16));

            var headerStyle = new GUIStyle(LudiqStyles.toolbarLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            var icon = GetGroupIcon(key);
            var label = GetGroupLabel(key);

            GUILayout.Label(new GUIContent(label, icon), headerStyle);

            foreach (var match in matches)
            {
                if (errorsOnly && !match.Matches.Any(m => m == MatchType.Error)) continue;
                DrawMatchItem(match, ref isShowingErrors);
            }
        }

        private Texture GetUnitIcon(IGraphElement element)
        {
            if (element is null) return typeof(Null).Icon()[1];
            if (element is MemberUnit)
            {
                if (element is InvokeMember invokeMember)
                {
                    var descriptor = invokeMember.Descriptor<InvokeMemberDescriptor>();
                    return descriptor.Icon()[1];
                }
                else if (element is GetMember getMember)
                {
                    var descriptor = getMember.Descriptor<GetMemberDescriptor>();
                    return descriptor.Icon()[1];
                }
                else
                {
                    var descriptor = element.Descriptor<SetMemberDescriptor>();
                    return descriptor.Icon()[1];
                }
            }
            else if (element is Literal literal)
            {
                var descriptor = literal.Descriptor<LiteralDescriptor>();
                return descriptor.Icon()[1];
            }
            else if (element is UnifiedVariableUnit unifiedVariableUnit)
            {
                var descriptor = unifiedVariableUnit.Descriptor<UnitDescriptor<UnifiedVariableUnit>>();
                return descriptor.Icon()[1];
            }
            else if (element is CommentNode commentNode)
            {
                return commentNode.Descriptor<CommentDescriptor>().Icon()[1];
            }
            else
            {
                var icon = element.GetType().Icon();
                return icon[1];
            }
        }

        private void SearchForErrors()
        {
            if (Time.realtimeSinceStartup - _lastSearchTime < SearchCooldown) return;

            _lastSearchTime = Time.realtimeSinceStartup;

            var handler = _matchHandlers[typeof(ErrorMatchHandler)];
            foreach (var provider in _graphProviders.Values.Where(p => p.IsEnabled))
            {
                List<IGraphElement> elements = new();
                foreach (var element in provider.GetElements())
                {
                    (handler as ErrorMatchHandler).graphPointer = element.Item1;
                    if (!handler.CanHandle(element.Item2) || elements.Contains(element.Item2)) continue;
                    elements.Add(element.Item2);
                    var match = handler.HandleMatch(element.Item2, _pattern, _searchMode);
                    if (match != null)
                    {
                        match.Reference = element.Item1;
                        ProcessMatch(match, provider);
                    }
                }
            }
        }

        private void Search()
        {
            if (Time.realtimeSinceStartup - _lastSearchTime < SearchCooldown) return;

            _lastSearchTime = Time.realtimeSinceStartup;

            _matchObjects.Clear();

            foreach (var provider in _graphProviders.Values.Where(p => p.IsEnabled))
            {
                if (provider is BaseGraphProvider baseProvider)
                {
                    baseProvider.ClearResults();
                }
                List<IGraphElement> elements = new();
                foreach (var element in provider.GetElements())
                {
                    foreach (var handler in _matchHandlers.Values)
                    {
                        if (!handler.CanHandle(element.Item2) || elements.Contains(element.Item2)) continue;
                        elements.Add(element.Item2);
                        var match = handler.HandleMatch(element.Item2, _pattern, _searchMode);
                        if (match != null)
                        {
                            match.Reference = element.Item1;
                            ProcessMatch(match, provider);
                        }
                    }
                }
            }
        }

        private void ProcessMatch(MatchObject match, IGraphProvider provider)
        {
            if (match == null || match.Matches.Count == 0) return;

            _matchObjects.Add(match);

            if (provider is ScriptGraphProvider)
                match.ScriptGraphAsset = provider.GetAssetForElement(match.Reference) as ScriptGraphAsset;
            else if (provider is StateGraphProvider)
                match.StateGraphAsset = provider.GetAssetForElement(match.Reference) as StateGraphAsset;
            else if (provider is ClassAssetProvider)
                match.ClassAsset = provider.GetAssetForElement(match.Reference) as ClassAsset;
            else if (provider is StructAssetProvider)
                match.StructAsset = provider.GetAssetForElement(match.Reference) as StructAsset;


            provider.HandleMatch(match);
        }

        public IEnumerable<GraphReference> GetReferences(ClassAsset asset)
        {
            foreach (var constructor in asset.constructors)
            {
                yield return constructor.GetReference().AsReference();
            }
            foreach (var variable in asset.variables)
            {
                if (variable.isProperty)
                {
                    if (variable.get)
                        yield return variable.getter.GetReference().AsReference();
                    if (variable.set)
                        yield return variable.setter.GetReference().AsReference();
                }
            }
            foreach (var method in asset.methods)
            {
                yield return method.GetReference().AsReference();
            }
        }

        public IEnumerable<GraphReference> GetReferences(StructAsset asset)
        {
            foreach (var constructor in asset.constructors)
            {
                yield return constructor.GetReference().AsReference();
            }
            foreach (var variable in asset.variables)
            {
                if (variable.isProperty)
                {
                    if (variable.get)
                        yield return variable.getter.GetReference().AsReference();
                    if (variable.set)
                        yield return variable.setter.GetReference().AsReference();
                }
            }
            foreach (var method in asset.methods)
            {
                yield return method.GetReference().AsReference();
            }
        }

        bool ShouldShowItem(IEnumerable<MatchObject> list)
        {
            foreach (var match in list)
            {
                foreach (var matchType in match.Matches)
                {
                    if (_typeFilters[matchType])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        bool IsError(IEnumerable<MatchObject> list)
        {
            foreach (var match in list)
            {
                if (_matchError && match.Matches.Contains(MatchType.Error))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetGraphName(Graph graph)
        {
            return !string.IsNullOrEmpty(graph.title) ? graph.title : "Unnamed Graph";
        }

        void FocusMatchObject(MatchObject match)
        {
            if (match.ScriptGraphAsset != null)
            {
                var asset = match.ScriptGraphAsset;

                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            else if (match.StateGraphAsset != null)
            {
                var asset = match.StateGraphAsset;

                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            else if (match.ScriptMachine != null)
            {
                var machine = match.ScriptMachine.gameObject;

                EditorGUIUtility.PingObject(machine);
                Selection.activeObject = machine;
            }


            var target = OpenReferencePath(match.Reference);
            GraphWindow.OpenActive(target);

            var context = target.Context();
            if (context == null)
                return;
            context.BeginEdit();
            if (match.group != null)
            {
                context.canvas?.ViewElements(((IGraphElement)match.group).Yield());
            }
#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
            else if (match.stickyNote != null)
            {
                context.canvas?.ViewElements(((IGraphElement)match.stickyNote).Yield());
            }
#endif
            else if (match.Unit != null)
            {
                context.canvas?.ViewElements(((IGraphElement)match.Unit).Yield());
            }
            context.EndEdit();
        }

        GraphReference OpenReferencePath(GraphReference graphReference)
        {
            var path = GraphTraversal.GetReferencePath(graphReference);
            GraphReference targetReference = graphReference.root.GetReference().AsReference();
            foreach (var item in path)
            {
                if (item.Item2 != null)
                {
                    targetReference = targetReference.ChildReference(item.Item2, false);
                }
                else if (item.Item1.isRoot)
                {
                    targetReference = item.Item1;
                }
            }
            return targetReference;
        }

        public enum MatchType
        {
            Unit,
            Comment,
            Group,
#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
            StickyNote,
#endif
            Error
        }

        public class MatchObject
        {
            public List<MatchType> Matches { get; set; } = new List<MatchType>();
            public ScriptGraphAsset ScriptGraphAsset { get; set; }
            public ScriptMachine ScriptMachine { get; set; }
            public StateMachine StateMachine { get; set; }
            public StateGraphAsset StateGraphAsset { get; set; }
            public ClassAsset ClassAsset { get; set; }
            public StructAsset StructAsset { get; set; }
            public GraphReference Reference { get; set; }
            public string FullTypeName { get; set; }
            public IUnit Unit { get; set; }
            public GraphGroup group { get; set; }
#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
            public StickyNote stickyNote { get; set; }
#endif
            public CommentNode comment { get; set; }
        }

        private Texture GetGroupIcon(Object key)
        {
            switch (key)
            {
                case ScriptGraphAsset:
                    return EditorGUIUtility.ObjectContent(key, typeof(ScriptGraphAsset)).image;
                case StateGraphAsset:
                    return EditorGUIUtility.ObjectContent(key, typeof(StateGraphAsset)).image;
                case ScriptMachine:
                case StateMachine:
                    return EditorGUIUtility.IconContent("GameObject Icon").image;
                case ClassAsset classAsset:
                    return classAsset.icon != null ? classAsset.icon : typeof(ClassAsset).Icon()[1];
                case StructAsset structAsset:
                    return structAsset.icon != null ? structAsset.icon : typeof(StructAsset).Icon()[1];
                default:
                    return null;
            }
        }

        private string GetGroupLabel(Object key)
        {
            switch (key)
            {
                case ScriptMachine scriptMachine:
                    return $"{scriptMachine.name} (ScriptMachine)";
                case StateMachine stateMachine:
                    return $"{stateMachine.name} (StateMachine)";
                default:
                    return key.name;
            }
        }
        private string HighlightQuery(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return text;

            if (_searchMode == SearchMode.StartsWith)
            {
                if (text.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return $"<b>{text[..pattern.Length]}</b>{text[pattern.Length..]}";
                }
                else
                {
                    return text;
                }
            }
            else
            {
                return SearchUtility.HighlightQuery(text, pattern);
            }
        }
        private void DrawMatchItem(MatchObject match, ref bool isShowingErrors, bool errorsOnly = false)
        {
            var pathNames = GraphTraversal.GetElementPath(match.Reference);
            var isError = match.Matches.Contains(MatchType.Error) && _matchError;

            if (isError)
            {
                isShowingErrors = true;
            }

            var label = isError
                ? $"      {pathNames} <color=#FF6800>{HighlightQuery(match.FullTypeName, _pattern)}</color>"
                : $"      {pathNames} {HighlightQuery(match.FullTypeName, _pattern)}";

            var pathStyle = new GUIStyle(LudiqStyles.paddedButton)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            if (match.Matches.Contains(MatchType.Error) && !_matchError) return;

            IGraphElement element = match.Unit;
#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
            if (match.stickyNote != null) element = match.stickyNote;
#endif
            if (match.group != null) element = match.group;
            if (match.comment != null) element = match.comment;

            EditorGUILayout.BeginHorizontal();
            bool buttonClicked = GUILayout.Button(new GUIContent(label, GetUnitIcon(element)), pathStyle);
            EditorGUILayout.EndHorizontal();

            if (buttonClicked)
            {
                FocusMatchObject(match);
            }
        }

        public static bool SearchMatches(string query, string haystack, SearchMode searchMode)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(haystack)) return false;

            if (searchMode == SearchMode.StartsWith)
            {
                return haystack.StartsWith(query, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return SearchUtility.Matches(query, haystack);
            }
        }
    }

    public class ScriptMachineProvider : NodeFinderWindow.BaseGraphProvider
    {
        public override string Name => "ScriptMachines";
        public ScriptMachineProvider(NodeFinderWindow nodeFinderWindow) : base(nodeFinderWindow)
        {
        }

        public override IEnumerable<(GraphReference, IGraphElement)> GetElements()
        {
            foreach (var machine in FindObjectsOfTypeIncludingInactive<ScriptMachine>().Where(_asset => _asset.nest.source == GraphSource.Embed))
            {
                if (machine?.GetReference().graph is not FlowGraph) continue;

                var baseRef = machine.GetReference().AsReference();
                foreach (var element in GraphTraversal.TraverseFlowGraph(baseRef))
                {
                    yield return element;
                }
            }
        }

        private static IEnumerable<T> FindObjectsOfTypeIncludingInactive<T>()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (scene.isLoaded)
                {
                    foreach (var rootGameObject in scene.GetRootGameObjects())
                    {
                        foreach (var result in rootGameObject.GetComponents<T>())
                        {
                            yield return result;
                        }
                        foreach (var result in rootGameObject.GetComponentsInChildren<T>(true))
                        {
                            yield return result;
                        }
                    }
                }
            }
        }

        public override void HandleMatch(NodeFinderWindow.MatchObject match)
        {
            var machine = match.ScriptMachine;
            if (machine != null)
            {
                AddMatch(match, machine);
            }
        }

        protected override string GetSortKey(Object key)
        {
            return (key as ScriptMachine).graph.title ?? base.GetSortKey(key);
        }
    }


    public class ScriptGraphProvider : NodeFinderWindow.BaseGraphProvider
    {
        public override string Name => "ScriptGraphAssets";

        public ScriptGraphProvider(NodeFinderWindow window) : base(window) { }

        public override IEnumerable<(GraphReference, IGraphElement)> GetElements()
        {
            if (!IsEnabled) yield break;

            var guids = AssetDatabase.FindAssets("t:ScriptGraphAsset", null);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
                if (asset?.GetReference().graph is not FlowGraph) continue;

                var baseRef = asset.GetReference().AsReference();
                foreach (var element in GraphTraversal.TraverseFlowGraph(baseRef))
                {
                    yield return element;
                }
            }
        }

        public override void HandleMatch(NodeFinderWindow.MatchObject match)
        {
            var asset = match.ScriptGraphAsset;
            if (asset != null)
            {
                AddMatch(match, asset);
            }
        }

        protected override string GetSortKey(Object key)
        {
            return (key as ScriptGraphAsset).graph.title ?? base.GetSortKey(key);
        }
    }

    public class StateGraphProvider : NodeFinderWindow.BaseGraphProvider
    {
        public override string Name => "StateGraphAssets";

        public StateGraphProvider(NodeFinderWindow window) : base(window) { }

        public override IEnumerable<(GraphReference, IGraphElement)> GetElements()
        {
            if (!IsEnabled) yield break;

            var guids = AssetDatabase.FindAssets("t:StateGraphAsset", null);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
                if (asset?.GetReference().graph is not StateGraph) continue;

                var baseRef = asset.GetReference().AsReference();
                foreach (var element in GraphTraversal.TraverseStateGraph(baseRef))
                {
                    yield return element;
                }
            }
        }

        public override void HandleMatch(NodeFinderWindow.MatchObject match)
        {
            var asset = match.StateGraphAsset;
            if (asset != null)
            {
                AddMatch(match, asset);
            }
        }

        protected override string GetSortKey(Object key)
        {
            return (key as StateGraphAsset).graph.title ?? base.GetSortKey(key);
        }
    }

    public class StateMachineProvider : NodeFinderWindow.BaseGraphProvider
    {
        public override string Name => "StateMachines";

        public StateMachineProvider(NodeFinderWindow window) : base(window) { }

        public override IEnumerable<(GraphReference, IGraphElement)> GetElements()
        {
            if (!IsEnabled) yield break;

            foreach (var machine in UnityObjectUtility.FindObjectsOfTypeIncludingInactive<StateMachine>().Where(_asset => _asset.nest.source == GraphSource.Embed))
            {
                if (machine?.GetReference().graph is not FlowGraph) continue;

                var baseRef = machine.GetReference().AsReference();
                foreach (var element in GraphTraversal.TraverseFlowGraph(baseRef))
                {
                    yield return element;
                }
            }
        }

        public override void HandleMatch(NodeFinderWindow.MatchObject match)
        {
            var asset = match.StateGraphAsset;
            if (asset != null)
            {
                AddMatch(match, asset);
            }
        }

        protected override string GetSortKey(Object key)
        {
            return (key as StateMachine).graph.title ?? base.GetSortKey(key);
        }
    }

    public class ClassAssetProvider : NodeFinderWindow.BaseGraphProvider
    {
        public override string Name => "ClassAssets";

        public ClassAssetProvider(NodeFinderWindow window) : base(window) { }

        public override IEnumerable<(GraphReference, IGraphElement)> GetElements()
        {
            if (!IsEnabled) yield break;

            var guids = AssetDatabase.FindAssets("t:ClassAsset", null);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ClassAsset>(assetPath);
                if (asset == null) continue;

                foreach (var reference in _window.GetReferences(asset))
                {
                    foreach (var element in GraphTraversal.TraverseFlowGraph(reference))
                    {
                        yield return element;
                    }
                }
            }
        }

        public override void HandleMatch(NodeFinderWindow.MatchObject match)
        {
            var asset = match.ClassAsset;
            if (asset != null)
            {
                AddMatch(match, asset);
            }
        }
    }

    public class StructAssetProvider : NodeFinderWindow.BaseGraphProvider
    {
        public override string Name => "StructAssets";

        public StructAssetProvider(NodeFinderWindow window) : base(window) { }

        public override IEnumerable<(GraphReference, IGraphElement)> GetElements()
        {
            if (!IsEnabled) yield break;

            var guids = AssetDatabase.FindAssets("t:StructAsset", null);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<StructAsset>(assetPath);
                if (asset == null) continue;

                foreach (var reference in _window.GetReferences(asset))
                {
                    foreach (var element in GraphTraversal.TraverseFlowGraph(reference))
                    {
                        yield return element;
                    }
                }
            }
        }

        public override void HandleMatch(NodeFinderWindow.MatchObject match)
        {
            var asset = match.StructAsset;
            if (asset != null)
            {
                AddMatch(match, asset);
            }
        }
    }

    public class UnitMatchHandler : IMatchHandler
    {
        public bool CanHandle(IGraphElement element)
        {
            return element is Unit && element is not CommentNode;
        }

        public NodeFinderWindow.MatchObject HandleMatch(IGraphElement element, string pattern, NodeFinderWindow.SearchMode searchMode)
        {
            if (element is Unit unit)
            {
                var matchRecord = new NodeFinderWindow.MatchObject
                {
                    Matches = new List<NodeFinderWindow.MatchType>(),
                    Unit = unit,
                    FullTypeName = GetUnitName(unit)
                };

                if (NodeFinderWindow.SearchMatches(pattern, matchRecord.FullTypeName, searchMode))
                {
                    matchRecord.Matches.Add(NodeFinderWindow.MatchType.Unit);
                }

                if (matchRecord.Matches.Count > 0)
                {
                    return matchRecord;
                }
            }
            return null;
        }

        private string GetUnitName(IGraphElement element)
        {
            if (element is Unit unit)
            {
                if (unit is GraphOutput or GraphInput)
                {
                    return unit.GetType().HumanName();
                }
                if (unit is SubgraphUnit subgraphUnit)
                {
                    if (subgraphUnit.nest.source == GraphSource.Embed)
                    {
                        return !string.IsNullOrEmpty(subgraphUnit.nest.graph.title) ? subgraphUnit.nest.graph.title : "Unnamed Subgraph";
                    }
                    else
                    {
                        return !string.IsNullOrEmpty(subgraphUnit.nest.graph.title) ? subgraphUnit.nest.graph.title : !string.IsNullOrEmpty(subgraphUnit.nest.macro.name) ? subgraphUnit.nest.macro.name : "Unnamed Subgraph";
                    }
                }
                if (unit is Literal literal)
                {
                    return literal.value != null ? literal.value.ToString() : "Literal";
                }
                return element.Descriptor().description.title ?? BoltFlowNameUtility.UnitTitle(unit.GetType(), true, false);
            }
            else if (element is not null) return element.Descriptor().description.title;
            return "Invalid Element";
        }
    }

    public class GroupMatchHandler : IMatchHandler
    {
        public bool CanHandle(IGraphElement element)
        {
            return element is GraphGroup;
        }

        public NodeFinderWindow.MatchObject HandleMatch(IGraphElement element, string pattern, NodeFinderWindow.SearchMode searchMode)
        {
            if (element is GraphGroup group)
            {
                var matchRecord = new NodeFinderWindow.MatchObject
                {
                    Matches = new List<NodeFinderWindow.MatchType>(),
                    group = group,
                    FullTypeName = GetGroupFullName(group)
                };

                if (NodeFinderWindow.SearchMatches(pattern, matchRecord.FullTypeName, searchMode))
                {
                    matchRecord.Matches.Add(NodeFinderWindow.MatchType.Group);
                    return matchRecord;
                }
            }
            return null;
        }

        private string GetGroupFullName(GraphGroup group)
        {
            if (!string.IsNullOrEmpty(group.label) && !string.IsNullOrEmpty(group.comment))
            {
                return group.label + "." + group.comment;
            }
            else if (!string.IsNullOrEmpty(group.label))
            {
                return group.label;
            }
            else if (!string.IsNullOrEmpty(group.comment))
            {
                return group.comment;
            }
            return "Unnamed Graph Group";
        }
    }

#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
    public class StickyNoteMatchHandler : IMatchHandler
    {
        public bool CanHandle(IGraphElement element)
        {
            return element is StickyNote;
        }

        public NodeFinderWindow.MatchObject HandleMatch(IGraphElement element, string pattern, NodeFinderWindow.SearchMode searchMode)
        {
            if (element is StickyNote note)
            {
                var matchRecord = new NodeFinderWindow.MatchObject
                {
                    Matches = new List<NodeFinderWindow.MatchType>(),
                    stickyNote = note,
                    FullTypeName = GetStickyNoteFullName(note)
                };

                if (NodeFinderWindow.SearchMatches(pattern, matchRecord.FullTypeName, searchMode))
                {
                    matchRecord.Matches.Add(NodeFinderWindow.MatchType.StickyNote);
                    return matchRecord;
                }
            }
            return null;
        }

        private string GetStickyNoteFullName(StickyNote note)
        {
            if (!string.IsNullOrEmpty(note.title) && !string.IsNullOrEmpty(note.body))
            {
                return note.title + "." + note.body;
            }
            else if (!string.IsNullOrEmpty(note.title))
            {
                return note.title;
            }
            else if (!string.IsNullOrEmpty(note.body))
            {
                return note.body;
            }
            return "Empty StickyNote";
        }

    }
#endif

    public class CommentsMatchHandler : IMatchHandler
    {
        public bool CanHandle(IGraphElement element)
        {
            return element is CommentNode;
        }

        public NodeFinderWindow.MatchObject HandleMatch(IGraphElement element, string pattern, NodeFinderWindow.SearchMode searchMode)
        {
            if (element is CommentNode comment)
            {
                var matchRecord = new NodeFinderWindow.MatchObject
                {
                    Matches = new List<NodeFinderWindow.MatchType>(),
                    comment = comment,
                    FullTypeName = GetCommentFullName(comment)
                };

                if (NodeFinderWindow.SearchMatches(pattern, matchRecord.FullTypeName, searchMode))
                {
                    matchRecord.Matches.Add(NodeFinderWindow.MatchType.Comment);
                    return matchRecord;
                }
            }
            return null;
        }

        private string GetCommentFullName(CommentNode note)
        {
            if (!string.IsNullOrEmpty(note.title) && !string.IsNullOrEmpty(note.comment))
            {
                return note.title + "." + note.comment;
            }
            else if (!string.IsNullOrEmpty(note.title))
            {
                return note.title;
            }
            else if (!string.IsNullOrEmpty(note.comment))
            {
                return note.comment;
            }
            return "Empty Comment";
        }
    }

    public class ErrorMatchHandler : IMatchHandler
    {
        public GraphPointer graphPointer;
        public bool CanHandle(IGraphElement element)
        {
            return graphPointer != null && element is Unit unit && IsErrorUnit(unit);
        }
        private bool IsErrorUnit(Unit unit)
        {
            if (unit.GetException(graphPointer) != null)
                return true;
#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
            if (unit is MissingType)
                return true;
#endif
            return false;
        }
        public NodeFinderWindow.MatchObject HandleMatch(IGraphElement element, string pattern, NodeFinderWindow.SearchMode searchMode)
        {
            if (element is Unit unit)
            {
                var matchRecord = new NodeFinderWindow.MatchObject
                {
                    Matches = new List<NodeFinderWindow.MatchType>(),
                    Unit = unit,
                    FullTypeName = GetUnitFullName(unit)
                };

                if (IsErrorUnit(unit))
                {
                    matchRecord.Matches.Add(NodeFinderWindow.MatchType.Error);
                    return matchRecord;
                }
            }
            return null;
        }

        private string GetUnitFullName(Unit unit)
        {
            var typeName = GetUnitName(unit);
#if VISUAL_SCRIPTING_1_8_0_OR_GREATER
            if (unit is MissingType missingType)
            {
                typeName = string.IsNullOrEmpty(missingType.formerType) ? "Missing Type" : "Missing Type : " + missingType.formerType;
            }
#endif
            if (unit.GetException(graphPointer) != null)
            {
                typeName += " " + unit.GetException(graphPointer).Message;
            }
            return typeName;
        }

        private string GetUnitName(IGraphElement element)
        {
            if (element is Unit unit)
            {
                if (unit is GraphOutput or GraphInput)
                {
                    return unit.GetType().HumanName();
                }
                if (unit is SubgraphUnit subgraphUnit)
                {
                    if (subgraphUnit.nest.source == GraphSource.Embed)
                    {
                        return !string.IsNullOrEmpty(subgraphUnit.nest.graph.title) ? subgraphUnit.nest.graph.title : "Unnamed Subgraph";
                    }
                    else
                    {
                        return !string.IsNullOrEmpty(subgraphUnit.nest.graph.title) ? subgraphUnit.nest.graph.title : !string.IsNullOrEmpty(subgraphUnit.nest.macro.name) ? subgraphUnit.nest.macro.name : "Unnamed Subgraph";
                    }
                }
                return element.Descriptor().description.title ?? BoltFlowNameUtility.UnitTitle(unit.GetType(), true, false);
            }

            return "Invalid Element";
        }
    }
}
