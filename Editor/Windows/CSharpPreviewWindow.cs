using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.VisualScripting.Community.Libraries.CSharp;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.VisualScripting.Community
{
    [Serializable]
    public sealed class CSharpPreviewWindow : EditorWindow
    {
        public static CSharpPreviewWindow instance;
        private float zoomFactor = 1.0f;
        public bool showCodeWindow = true;
        private List<(Label, int)> labels = new List<(Label, int)>();

        public static Object asset;

        [MenuItem("Window/Community Addons/C# Preview")]
        public static void Open()
        {
            CSharpPreviewWindow window = GetWindow<CSharpPreviewWindow>();
            window.titleContent = new GUIContent("C# Preview");
            instance = window;
        }

        public void CreateGUI()
        {
            if(instance == null)
            {
                CSharpPreviewWindow window = GetWindow<CSharpPreviewWindow>();
                instance = window;
            }
            Selection.selectionChanged += ChangeSelection;
            var toolbar = new Toolbar();
            toolbar.name = "Toolbar";
            var codeView = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexGrow = 1 }, name = "codeView" };
            var codeContainer = new ScrollView
            {
                name = "codeContainer",
                style = { backgroundColor = new Color(0.15f, 0.15f, 0.15f), flexGrow = 1 }
            };

            var settingsContainer = new ScrollView
            {
                name = "settingsContainer",
                style = { backgroundColor = new Color(0.18f, 0.18f, 0.18f), flexGrow = 1 }
            };
            var lineNumbersContainer = new ScrollView()
            {
                name = "lineNumbersContainer",
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                style = { backgroundColor = new Color(0.15f, 0.15f, 0.15f), flexDirection = FlexDirection.Column }
            };
            codeContainer.verticalScroller.valueChanged += (newValue) =>
            {
                lineNumbersContainer.verticalScroller.value = newValue;
            };
            lineNumbersContainer.verticalScroller.valueChanged += (newValue) =>
            {
                codeContainer.verticalScroller.value = newValue;
            };
            codeView.Add(lineNumbersContainer);
            codeView.Add(codeContainer);
            CreateSettingsUI(settingsContainer);

            // Create the Zoom section
            var zoomContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10
                }
            };
            var zoomTextLabel = new Label("Zoom:") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 5 } };
            var zoomSlider = new Slider(0.5f, 2.0f)
            {
                value = zoomFactor,
                style = { width = Length.Percent(20), alignSelf = Align.Center }
            };
            var zoomLabel = new Label($"{zoomFactor:0.#}x") { style = { marginLeft = 5, alignSelf = Align.Center } };

            // Register a callback for when the slider value changes
            zoomSlider.RegisterValueChangedCallback(evt =>
            {
                zoomFactor = evt.newValue;
                zoomLabel.text = $"{zoomFactor:0.#}x";
                codeContainer.style.fontSize = Mathf.RoundToInt(14 * zoomFactor);
                lineNumbersContainer.style.fontSize = Mathf.RoundToInt(14 * zoomFactor);
            });

            zoomContainer.Add(zoomTextLabel);
            zoomContainer.Add(zoomSlider);
            zoomContainer.Add(zoomLabel);

            toolbar.Add(zoomContainer);

            // Toolbar Buttons with toolbar-like styling
            var compileButton = CreateToolbarButton("Compile", CompileCode);
            toolbar.Add(compileButton);

            var copyButton = CreateToolbarButton("Copy to Clipboard", CopyToClipboard);
            toolbar.Add(copyButton);

            var utilityButton = CreateToolbarButton("Utility Window", OpenUtilityWindow);
            toolbar.Add(utilityButton);

            var refreshButton = CreateToolbarButton("Refresh", UpdateCodeDisplay);
            toolbar.Add(refreshButton);

            var toggleButton = CreateToolbarButton(showCodeWindow ? "Settings" : "Preview", ToggleWindowMode);
            toggleButton.name = "toggleButton";
            toolbar.Add(toggleButton);

            rootVisualElement.Add(toolbar);
            rootVisualElement.Add(codeView);
            rootVisualElement.Add(settingsContainer);
            ChangeSelection();

            codeView.style.display = showCodeWindow ? DisplayStyle.Flex : DisplayStyle.None;
            settingsContainer.style.display = showCodeWindow ? DisplayStyle.None : DisplayStyle.Flex;
        }
        private void CreateSettingsUI(ScrollView settingsContainer)
        {
            var path = "Assets/Unity.VisualScripting.Community.Generated/";
            HUMIO.Ensure(path).Path();
            CSharpPreviewSettings settings = AssetDatabase.LoadAssetAtPath<CSharpPreviewSettings>(path + "CSharpPreviewSettings.asset");

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CSharpPreviewSettings>();
                settings.name = "CSharpPreviewSettings";
                AssetDatabase.CreateAsset(settings, path + "CSharpPreviewSettings.asset");
                settings.Initalize();
            }
            else if (!settings.isInitalized)
            {
                settings.Initalize();
            }
            CodeBuilder.ShowRecommendations = settings.ShowRecommendations;

            var settingsLabel = new Label("C# Preview Settings")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    alignSelf = Align.Center,
                    marginTop = 10
                }
            };
            settingsContainer.Add(settingsLabel);


            #region Generation Settings
            var generationSettingsSection = new VisualElement
            {
                style = { marginLeft = 10, marginTop = 10 }
            };
            var generationSettingsLabel = new Label("Generation Settings")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, marginTop = 10 }
            };
            generationSettingsSection.Add(generationSettingsLabel);

            float labelWidth = 200;

            // --- Subgraph Comment ---
            var subgraphToggleContainer = new VisualElement
            {
                style = { marginTop = 10, flexDirection = FlexDirection.Row }
            };

            var showSubgraphLabel = new Label("Show Subgraph Comment :")
            {
                tooltip = "Generate a comment where the Subgraph and Port are being generated.",
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 10, width = labelWidth }
            };

            var showSubgraphCommentToggle = new Toggle
            {
                style = { marginLeft = 10 }
            };
            showSubgraphCommentToggle.value = settings.ShowSubgraphComment;
            CSharpPreview.ShowSubgraphComment = settings.ShowSubgraphComment;

            showSubgraphCommentToggle.RegisterValueChangedCallback(evt =>
            {
                settings.ShowSubgraphComment = evt.newValue;
                CSharpPreview.ShowSubgraphComment = evt.newValue;
                settings.SaveAndDirty();
            });

            subgraphToggleContainer.Add(showSubgraphLabel);
            subgraphToggleContainer.Add(showSubgraphCommentToggle);
            generationSettingsSection.Add(subgraphToggleContainer);

            // --- Recommendations ---
            var recommendationToggleContainer = new VisualElement
            {
                style = { marginTop = 5, flexDirection = FlexDirection.Row }
            };

            var showRecommendationLabel = new Label("Show Recommendations :")
            {
                tooltip = "Show recommendations if there is a better way of generating the code.",
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 10, width = labelWidth }
            };

            var showRecommendationToggle = new Toggle
            {
                style = { marginLeft = 10 }
            };

            showRecommendationToggle.value = settings.ShowRecommendations;
            CodeBuilder.ShowRecommendations = settings.ShowRecommendations;
            showRecommendationToggle.RegisterValueChangedCallback(evt =>
            {
                settings.ShowRecommendations = evt.newValue;
                CodeBuilder.ShowRecommendations = evt.newValue;
                settings.SaveAndDirty();
            });

            recommendationToggleContainer.Add(showRecommendationLabel);
            recommendationToggleContainer.Add(showRecommendationToggle);
            generationSettingsSection.Add(recommendationToggleContainer);

            // --- Tooltips ---
            var tooltipToggleContainer = new VisualElement
            {
                style = { marginTop = 5, flexDirection = FlexDirection.Row }
            };

            var showTooltipLabel = new Label("Show Tooltips :")
            {
                tooltip = "Show tooltips in places where there is a problem.",
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 10, width = labelWidth }
            };

            var showTooltipToggle = new Toggle
            {
                style = { marginLeft = 10 }
            };

            showTooltipToggle.value = settings.ShowTooltips;
            CodeUtility.GenerateTooltips = settings.ShowTooltips;
            showTooltipToggle.RegisterValueChangedCallback(evt =>
            {
                settings.ShowTooltips = evt.newValue;
                CodeUtility.GenerateTooltips = evt.newValue;
                settings.SaveAndDirty();
            });

            tooltipToggleContainer.Add(showTooltipLabel);
            tooltipToggleContainer.Add(showTooltipToggle);
            generationSettingsSection.Add(tooltipToggleContainer);

            // --- Add generations settings ---
            settingsContainer.Add(generationSettingsSection);
            #endregion

            #region Syntax Highlights
            var syntaxHighlightsSection = new VisualElement
            {
                style = { marginLeft = 10, marginTop = 10 }
            };

            var syntaxHighlightsLabel = new Label("Syntax Highlights")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, marginTop = 10 }
            };
            syntaxHighlightsSection.Add(syntaxHighlightsLabel);

            float labelsWidth = 200; // Fixed width for label alignment

            void AddColorField(Action initialize, VisualElement container, string labelText, string tooltip, Color initialColor, Action<Color> onColorChanged, Action<ColorField> resetToDefault)
            {
                initialize();

                var colorContainer = new VisualElement
                {
                    style = { marginTop = 10, flexDirection = FlexDirection.Row }
                };

                var colorLabel = new Label(labelText)
                {
                    tooltip = tooltip,
                    style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 10, width = labelsWidth }
                };

                var colorField = new ColorField
                {
                    style = { marginLeft = 10, width = 400 },
                    value = initialColor
                };

                colorField.RegisterValueChangedCallback(evt =>
                {
                    onColorChanged(evt.newValue);
                    settings.SaveAndDirty();
                });

                var defaultButton = new Button(() =>
                {
                    resetToDefault(colorField);
                    settings.SaveAndDirty();
                })
                {
                    text = "Default",
                    style = { marginLeft = 10 }
                };

                colorContainer.Add(colorLabel);
                colorContainer.Add(colorField);
                colorContainer.Add(defaultButton);

                container.Add(colorContainer);
            }

            AddColorField(() => CodeBuilder.VariableColor = settings.VariableColor.ToHexString(), syntaxHighlightsSection, "Variable Color :", "The variable color.", settings.VariableColor, color =>
            {
                settings.VariableColor = color;
                CodeBuilder.VariableColor = color.ToHexString();
                settings.SaveAndDirty();
            }, (field) =>
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString("#00FFFF", out var value))
                {
                    settings.VariableColor = value;
                    field.value = value;
                }
                CodeBuilder.VariableColor = "00FFFF";
            });

            AddColorField(() => CodeBuilder.StringColor = settings.StringColor.ToHexString(), syntaxHighlightsSection, "String Color :", "The color for strings.", settings.StringColor, color =>
            {
                settings.StringColor = color;
                CodeBuilder.StringColor = color.ToHexString();
                settings.SaveAndDirty();
            }, (field) =>
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString("#CC8833", out var value))
                {
                    settings.StringColor = value;
                    field.value = value;
                }
                CodeBuilder.StringColor = "CC8833";
            });

            AddColorField(() => CodeBuilder.NumericColor = settings.NumericColor.ToHexString(), syntaxHighlightsSection, "Numeric Color :", "The color for numeric values.", settings.NumericColor, color =>
            {
                settings.NumericColor = color;
                CodeBuilder.NumericColor = color.ToHexString();
                settings.SaveAndDirty();
            }, (field) =>
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString("#DDFFBB", out var value))
                {
                    settings.NumericColor = value;
                    field.value = value;
                }
                CodeBuilder.NumericColor = "DDFFBB";
            });

            AddColorField(() => CodeBuilder.ConstructColor = settings.ConstructColor.ToHexString(), syntaxHighlightsSection, "Construct Color :", "The color for constructs (e.g., loops, conditionals).", settings.ConstructColor, color =>
            {
                settings.ConstructColor = color;
                CodeBuilder.ConstructColor = color.ToHexString();
                settings.SaveAndDirty();
            }, (field) =>
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString("#4488FF", out var value))
                {
                    settings.ConstructColor = value;
                    field.value = value;
                }
                CodeBuilder.ConstructColor = "4488FF";
            });

            AddColorField(() => CodeBuilder.TypeColor = settings.TypeColor.ToHexString(), syntaxHighlightsSection, "Type Color :", "The color for data types.", settings.TypeColor, color =>
            {
                settings.TypeColor = color;
                CodeBuilder.TypeColor = color.ToHexString();
                settings.SaveAndDirty();
            }, (field) =>
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString("#33EEAA", out var value))
                {
                    settings.TypeColor = value;
                    field.value = value;
                }
                CodeBuilder.TypeColor = "33EEAA";
            });

            AddColorField(() => CodeBuilder.EnumColor = settings.EnumColor.ToHexString(), syntaxHighlightsSection, "Enum Color :", "The color for enums.", settings.EnumColor, color =>
            {
                settings.EnumColor = color;
                CodeBuilder.EnumColor = color.ToHexString();
                settings.SaveAndDirty();
            }, (field) =>
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString("#FFFFBB", out var value))
                {
                    settings.EnumColor = value;
                    field.value = value;
                }
                CodeBuilder.EnumColor = "FFFFBB";
            });

            AddColorField(() => CodeBuilder.InterfaceColor = settings.InterfaceColor.ToHexString(), syntaxHighlightsSection, "Interface Color :", "The color for interfaces.", settings.InterfaceColor, color =>
            {
                settings.InterfaceColor = color;
                CodeBuilder.InterfaceColor = color.ToHexString();
                settings.SaveAndDirty();
            }, (field) =>
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString("#DDFFBB", out var value))
                {
                    settings.InterfaceColor = value;
                    field.value = value;
                }
                CodeBuilder.InterfaceColor = "DDFFBB";
            });

            settingsContainer.Add(syntaxHighlightsSection);
            #endregion
        }

        private Button CreateToolbarButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.paddingLeft = 6;
            button.style.paddingRight = 6;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            button.style.backgroundColor = new Color(0, 0, 0, 0);
            button.style.borderTopColor = new Color(0, 0, 0, 0);
            button.style.borderBottomColor = new Color(0, 0, 0, 0);
            button.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            button.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 0;
            button.style.color = Color.white;
            button.style.borderTopLeftRadius = 0;
            button.style.borderBottomLeftRadius = 0;
            button.style.borderTopRightRadius = 0;
            button.style.borderBottomRightRadius = 0;

            // Hover effects
            var defaultBackgroundColor = button.style.backgroundColor.value;
            var hoverBackgroundColor = new Color(0.15f, 0.15f, 0.15f); // Darker color on hover

            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                button.style.backgroundColor = hoverBackgroundColor;
            });

            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.backgroundColor = defaultBackgroundColor;
            });

            return button;
        }

        private void CompileCode()
        {
            if (asset != null)
                AssetCompiler.CompileAsset(asset);
        }

        private void CopyToClipboard()
        {
            string outputToCopy = string.Empty;

            if (selectedLabels.Count > 0)
            {
                // Sort selected labels by line in ascending order
                var sortedLabels = selectedLabels.OrderBy(value => value.Item2).ToList();

                int? previousLine = null;
                foreach (var (label, line) in sortedLabels)
                {
                    // Add new line if this label is on a new line compared to the previous
                    if (previousLine != null && line != previousLine)
                    {
                        outputToCopy += "\n";
                    }

                    // Add the cleaned-up label text
                    outputToCopy += CodeUtility.CleanCode(RemoveColorTags(label.text));
                    previousLine = line;
                }
            }
            else
            {
                // Default behavior when no labels are selected
                outputToCopy = CodeUtility.CleanCode(RemoveColorTags(LoadCode()));
            }

            // Copy the result to the clipboard
            EditorGUIUtility.systemCopyBuffer = outputToCopy;
        }

        private string RemoveColorTags(string input)
        {
            return Regex.Replace(input, @"<color=#[0-9a-fA-F]{6,8}>|</color>", string.Empty, RegexOptions.Compiled);
        }

        private void OpenUtilityWindow()
        {
            UtilityWindow.Open();
        }

        private void ToggleWindowMode()
        {
            showCodeWindow = !showCodeWindow;
            var toggleButton = rootVisualElement.Q<Toolbar>("Toolbar").Q<Button>("toggleButton");
            toggleButton.text = showCodeWindow ? "Settings" : "Preview";

            var codeView = rootVisualElement.Q<VisualElement>("codeView");
            var settingsContainer = rootVisualElement.Q<ScrollView>("settingsContainer");

            codeView.style.display = showCodeWindow ? DisplayStyle.Flex : DisplayStyle.None;
            settingsContainer.style.display = showCodeWindow ? DisplayStyle.None : DisplayStyle.Flex;

            if (showCodeWindow) UpdateCodeDisplay();
        }

        private void ChangeSelection()
        {
            var firstReference = GetFirstReference();
            if (Selection.activeObject is CodeAsset _asset)
            {
                asset = _asset;
                UpdateCodeDisplay();
                if (firstReference != null)
                    GraphWindow.activeReference = firstReference;
            }
            else if (Selection.activeObject is ScriptGraphAsset _graphAsset)
            {
                asset = _graphAsset;
                UpdateCodeDisplay();
            }
        }

        public void UpdateCodeDisplay()
        {
            if (asset != null)
            {
                var loadedCode = LoadCode();
                var code = "";
                if (loadedCode.Length > 0)
                {
                    code = "#pragma warning disable\n".ConstructHighlight().RemoveMarkdown();
                }
                code += loadedCode;
                var scrollView = rootVisualElement.Q<VisualElement>("codeView").Q<ScrollView>("codeContainer");
                var lineNumbersScrollView = rootVisualElement.Q<VisualElement>("codeView").Q<ScrollView>("lineNumbersContainer");
                DisplayCode(lineNumbersScrollView, scrollView, code);
            }
        }
        Dictionary<string, List<(Label, int)>> unitIDRegions = new Dictionary<string, List<(Label, int)>>();
        private void DisplayCode(ScrollView lineNumbersScrolView, ScrollView scrollView, string code)
        {
            scrollView.Clear();
            lineNumbersScrolView.Clear();
            labels.Clear();
            SetupScrollbarMarkers(scrollView);

            var clickableRegions = CodeUtility.ExtractClickableRegions(code);
            var regionsByLine = clickableRegions
                .GroupBy(region => region.startLine)
                .ToDictionary(g => g.Key, g => g.ToList());

            var lines = CodeUtility.RemoveAllSelectableTags(code).Split('\n');
            var width = 20f;
            for (int i = 0; i < lines.Length; i++)
            {
                var lineNumberContainer = new Label($"{i + 1}");
                width = Mathf.Max(width, lineNumberContainer.MeasureTextSize($"{i + 1}", lineNumberContainer.style.width.value.value, VisualElement.MeasureMode.Exactly, lineNumberContainer.style.height.value.value, VisualElement.MeasureMode.Exactly).x);
                lineNumberContainer.style.width = width;
                lineNumberContainer.style.unityTextAlign = TextAnchor.MiddleLeft;
                lineNumberContainer.style.color = Color.gray;
                lineNumberContainer.style.marginLeft = 5;
                lineNumberContainer.style.marginRight = 5;
                lineNumberContainer.style.paddingRight = 0;
                lineNumberContainer.style.paddingLeft = 0;
                lineNumbersScrolView.Add(lineNumberContainer);

                var codeContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 10 } };

                if (regionsByLine.TryGetValue(i, out var regions))
                {
                    AdjustLeadingWhitespacesForFirstRegion(lines[i], regions[0]);
                    foreach (var region in regions)
                    {
                        var label = CreateCodeLabel(region, i);
                        labels.Add((label, i));
                        codeContainer.Add(label);
                        if (!unitIDRegions.ContainsKey(region.unitId))
                        {
                            unitIDRegions[region.unitId] = new List<(Label, int)>() { (label, i) };
                        }
                        else
                        {
                            unitIDRegions[region.unitId].Add((label, i));
                        }
                    }
                }
                else
                {
                    var label = CreateNonClickableLabel(lines[i], i);
                    labels.Add((label, i));
                    codeContainer.Add(label);
                }
                scrollView.Add(codeContainer);
            }
        }

        private VisualElement markerContainer;

        private void SetupScrollbarMarkers(ScrollView scrollView)
        {
            // Create a container for the markers
            markerContainer = new VisualElement
            {
                style =
        {
            position = Position.Absolute,
            width = 4,
            backgroundColor = Color.gray,
            right = 0, // Align to the right side
        }
            };
            scrollView.hierarchy.Add(markerContainer);

            // Ensure markers update when the scroll view changes
            scrollView.RegisterCallback<GeometryChangedEvent>(evt => UpdateScrollBarMarkers(scrollView, selectedLabels));
            scrollView.verticalScroller.valueChanged += _ => UpdateScrollBarMarkers(scrollView, selectedLabels);
        }

        private void UpdateScrollBarMarkers(ScrollView scrollView, List<(Label, int)> selectedLines)
        {
            var verticalScrollBar = scrollView.verticalScroller;
            if (verticalScrollBar == null)
                return;

            foreach (VisualElement child in verticalScrollBar.Children().ToList())
            {
                if (child.name != null && child.name.StartsWith("marker"))
                    verticalScrollBar.Remove(child);
            }

            float totalContentHeight = scrollView.contentContainer.resolvedStyle.height;
            float visibleHeight = scrollView.resolvedStyle.height;
            float scrollbarHeight = verticalScrollBar.resolvedStyle.height;

            if (totalContentHeight <= 0 || visibleHeight <= 0 || scrollbarHeight <= 0)
                return;

            float lowButtonHeight = verticalScrollBar.lowButton.resolvedStyle.height;
            float highButtonHeight = verticalScrollBar.highButton.resolvedStyle.height;

            float usableScrollbarHeight = scrollbarHeight - lowButtonHeight - highButtonHeight;

            float lineHeight = totalContentHeight / scrollView.contentContainer.childCount;

            foreach (var (label, line) in selectedLines)
            {
                float linePosition = line * lineHeight;

                float markerY = linePosition * (usableScrollbarHeight / totalContentHeight);

                markerY += lowButtonHeight;

                if (markerY + 5 > usableScrollbarHeight + lowButtonHeight)
                {
                    markerY = usableScrollbarHeight + lowButtonHeight - 5;
                }

                // Create a new marker
                var marker = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        top = markerY,
                        height = 5,
                        width = verticalScrollBar.resolvedStyle.width / 1.5f,
                        alignSelf = Align.Center,
                        backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.3f),
                    },
                    focusable = false,
                    name = "marker" + line,
                };
                marker.pickingMode = PickingMode.Ignore;
                verticalScrollBar.Add(marker);
            }
        }

        private void AdjustLeadingWhitespacesForFirstRegion(string line, ClickableRegion firstRegion)
        {
            int leadingWhitespaceLength = 0;

            while (leadingWhitespaceLength < line.Length && char.IsWhiteSpace(line[leadingWhitespaceLength]))
            {
                leadingWhitespaceLength++;
            }

            if (!firstRegion.code.StartsWith(line.AsSpan(0, leadingWhitespaceLength).ToString()))
            {
                firstRegion.code = line.Substring(0, leadingWhitespaceLength) + firstRegion.code;
            }
        }

        private void OnCodeRegionClicked(ClickableRegion region, int currentLine)
        {
            HandleClickableRegionClick(region.unitId, currentLine);
        }

        private List<(Label, int)> selectedLabels = new List<(Label, int)>();
        private List<Label> relatedLabels = new List<Label>();
        private Label lastSelectedLabel = null;

        private Label CreateNonClickableLabel(string text, int currentLine)
        {
            string tooltip;
            var codeWithoutTooltip = CodeUtility.ExtractTooltip(text, out tooltip);
            var label = new Label(CodeUtility.RemoveAllSelectableTags(codeWithoutTooltip))
            {
                tooltip = tooltip
            };
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.enableRichText = true;
            label.style.color = Color.white;
            label.style.backgroundColor = new Color(1, 1, 1, 0);
            RemovePaddingAndMargin(label);
            label.RegisterCallback<ClickEvent>(evt => SelectLabel(label, evt, "", currentLine));
            return label;
        }

        private Label CreateCodeLabel(ClickableRegion region, int currentLine)
        {
            string tooltip;
            var codeWithoutTooltip = CodeUtility.ExtractTooltip(region.code, out tooltip);
            var label = new Label(CodeUtility.RemoveAllSelectableTags(codeWithoutTooltip))
            {
                tooltip = tooltip
            };
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.color = Color.white;
            label.enableRichText = true;
            label.style.backgroundColor = new Color(1, 1, 1, 0);
            RemovePaddingAndMargin(label);

            label.RegisterCallback<ClickEvent>(evt =>
            {
                SelectLabel(label, evt, region.unitId, currentLine);
                OnCodeRegionClicked(region, currentLine);
            });

            return label;
        }
        private void RemovePaddingAndMargin(Label label)
        {
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
        }

        private double lastClickTime = 0f;
        private const float doubleClickThreshold = 0.3f;

        private void SelectLabel(Label label, ClickEvent evt, string unitId, int currentLine)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            double timeSinceLastClick = currentTime - lastClickTime;
            if (timeSinceLastClick <= doubleClickThreshold)
            {
                ClearSelection();
                HandleDoubleClick(label, unitId, currentLine);
                lastSelectedLabel = label;
            }
            else
            {
                if (evt.ctrlKey)
                {
                    if (selectedLabels.Contains((label, currentLine)))
                    {
                        DeselectLabel(label, unitId, currentLine);
                    }
                    else
                    {
                        AddLabelToSelection(label, unitId, currentLine);
                    }
                }
                else if (evt.shiftKey && lastSelectedLabel != null)
                {
                    SelectRange(label, unitId, currentLine);
                }
                else
                {
                    ClearSelection();
                    AddLabelToSelection(label, unitId, currentLine);
                }

                lastSelectedLabel = label;
            }

            lastClickTime = currentTime;
        }

        private void HandleDoubleClick(Label label, string unitId, int currentLine)
        {
            if (selectedLabels.Contains((label, currentLine)))
            {
                DeselectAllLabels(label, unitId, currentLine);
            }
            else
            {
                AddAllLabelsToSelection(label, unitId, currentLine);
            }
        }


        private void AddAllLabelsToSelection(Label label, string unitId, int currentLine)
        {
            if (unitIDRegions.ContainsKey(unitId))
            {
                selectedLabels.Add((label, currentLine));
                label.style.backgroundColor = new Color(0.25f, 0.5f, 0.8f, 0.3f); // Highlight color
                foreach (var (targetLabel, line) in unitIDRegions[unitId])
                {
                    if (targetLabel != label && !selectedLabels.Contains((targetLabel, line)))
                    {
                        targetLabel.style.backgroundColor = new Color(0.25f, 0.5f, 0.8f, 0.3f); // Highlight color
                        selectedLabels.Add((targetLabel, line));
                    }
                }
            }
            else
            {
                label.style.backgroundColor = new Color(0.25f, 0.5f, 0.8f, 0.3f); // Highlight color
                selectedLabels.Add((label, currentLine));
            }
        }

        private void DeselectAllLabels(Label label, string unitId, int currentLine)
        {
            if (unitIDRegions.ContainsKey(unitId))
            {
                selectedLabels.Remove((label, currentLine));
                label.style.backgroundColor = new Color(1, 1, 1, 0); // Default background
                foreach (var (targetLabel, line) in unitIDRegions[unitId])
                {
                    if (targetLabel != label && selectedLabels.Contains((targetLabel, line)))
                    {
                        targetLabel.style.backgroundColor = new Color(1, 1, 1, 0); // Default background
                        selectedLabels.Remove((targetLabel, line));
                    }
                }
            }
            else
            {
                label.style.backgroundColor = new Color(1, 1, 1, 0); // Default background
                selectedLabels.Remove((label, currentLine));
            }
        }

        private void AddLabelToSelection(Label label, string unitId, int currentLine)
        {
            if (unitIDRegions.ContainsKey(unitId))
            {
                selectedLabels.Add((label, currentLine));
                label.style.backgroundColor = new Color(0.25f, 0.5f, 0.8f, 0.3f); // Highlight color
                foreach (var (targetLabel, line) in unitIDRegions[unitId])
                {
                    if (targetLabel != label && !selectedLabels.Contains((targetLabel, line)))
                    {
                        targetLabel.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.4f); // Highlight color
                        relatedLabels.Add(targetLabel);
                    }
                }
            }
            else
            {
                label.style.backgroundColor = new Color(0.25f, 0.5f, 0.8f, 0.3f); // Highlight color
                selectedLabels.Add((label, currentLine));
            }
        }

        private void DeselectLabel(Label label, string unitId, int currentLine)
        {
            if (unitIDRegions.ContainsKey(unitId))
            {
                selectedLabels.Remove((label, currentLine));
                if (unitIDRegions[unitId].Contains((label, currentLine)))
                    label.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.4f); // Default background
                else
                    label.style.backgroundColor = new Color(1, 1, 1, 0); // Default background
                foreach (var (targetLabel, line) in unitIDRegions[unitId])
                {
                    if (targetLabel != label && !selectedLabels.Contains((targetLabel, line)) && !selectedLabels.Any(_label => unitIDRegions[unitId].Contains(_label)))
                    {
                        targetLabel.style.backgroundColor = new Color(1, 1, 1, 0); // Default background
                        relatedLabels.Remove(targetLabel);
                    }
                }
            }
            else
            {
                label.style.backgroundColor = new Color(1, 1, 1, 0); // Default background
                selectedLabels.Remove((label, currentLine));
            }
        }

        // Clear all selected labels
        private void ClearSelection()
        {
            foreach (var selectedLabel in selectedLabels)
            {
                selectedLabel.Item1.style.backgroundColor = new Color(1, 1, 1, 0);
            }
            foreach (var selectedLabel in relatedLabels)
            {
                selectedLabel.style.backgroundColor = new Color(1, 1, 1, 0);
            }
            selectedLabels.Clear();
            relatedLabels.Clear();
        }

        private void SelectRange(Label label, string unitID, int currentLine)
        {
            if (lastSelectedLabel == null) return;

            int startIndex = labels.FindIndex(val => val.Item1 == lastSelectedLabel);
            int endIndex = labels.IndexOf((label, currentLine));

            if (startIndex == -1 || endIndex == -1) return;

            if (startIndex > endIndex)
            {
                (startIndex, endIndex) = (endIndex, startIndex);
            }

            for (int i = startIndex; i <= endIndex; i++)
            {
                var (labelInRange, labelLine) = labels[i];
                if (!selectedLabels.Contains((labelInRange, labelLine)))
                {
                    AddLabelToSelection(labelInRange, unitID, labelLine);
                }
            }

        }

        private string LoadCode()
        {
            return CodeGenerator.GetSingleDecorator(asset).Generate(0).RemoveMarkdown();
        }

        private void HandleClickableRegionClick(string unitId, int line)
        {
            var code = CodeGenerator.GetSingleDecorator(asset);
            if (GraphWindow.active?.reference != null && GraphWindow.active.context.graph is FlowGraph)
            {
                var reference = GraphWindow.active.reference.isRoot ? GraphWindow.active.reference : GraphWindow.active.reference.root.GetReference() as GraphReference;
                GraphWindow.active.Focus();

                List<(GraphReference, Unit)> units = new List<(GraphReference, Unit)>();
                if (reference.macro != null && reference.macro is MethodDeclaration or ConstructorDeclaration or FieldDeclaration)
                {
                    if (reference.macro is MethodDeclaration methodDeclaration)
                    {
                        ProcessMethodDeclaration(methodDeclaration, units);
                    }
                    else if (reference.macro is ConstructorDeclaration constructorDeclaration)
                    {
                        ProcessConstructorDeclaration(constructorDeclaration, units);
                    }
                    else if (reference.macro is FieldDeclaration fieldDeclaration)
                    {
                        ProcessFieldDeclaration(fieldDeclaration, units);
                    }
                }
                else
                {
                    units = TraverseFlowGraph(reference).ToList();
                }
                var ordered = units.OrderableSearchFilter(unitId ?? "", (value) => value.Item2.ToString());
                if (ordered.Count() > 0 && ordered.Any(selectable => selectable.result.Item2 != null))
                {
                    ordered = ordered.Where(selectable => selectable.result.Item2 != null);
                }
                if (units.OrderableSearchFilter(unitId ?? "", (value) => value.Item2.ToString()).Count() > 0)
                {
                    if (!GraphWindow.active.reference.isRoot && reference != ordered.First().result.Item1)
                    {
                        GraphWindow.active.reference = GraphWindow.active.reference.root.GetReference() as GraphReference;
                    }
                    var path = GetUnitPath(ordered.First().result.Item1);
                    if (GraphWindow.active.reference != ordered.First().result.Item1)
                    {
                        foreach (var item in path)
                        {
                            if (item.Item2 != null)
                            {
                                GraphWindow.active.reference = GraphWindow.active.reference.ChildReference(item.Item2, false);
                            }
                            else if (item.Item1.isRoot)
                            {
                                GraphWindow.active.reference = item.Item1;
                            }
                        }
                    }
                    if (ordered.First().result.Item2 != null)
                    {
                        var canvas = GraphWindow.active.context.canvas as FlowCanvas;
                        GraphWindow.active.context.BeginEdit();
                        GraphWindow.active.context.canvas.UpdateViewport();
                        canvas.ViewElements(new List<Unit>() { ordered.First().result.Item2 });
                        GraphWindow.active.context.EndEdit();
                    }
                }
            }
            else
            {
                if (code is ClassAssetGenerator classAssetGenerator)
                {
                    if (classAssetGenerator.Data != null)
                    {
                        List<(GraphReference, Unit)> units = new List<(GraphReference, Unit)>();
                        foreach (var constructorDeclaration in classAssetGenerator.Data.constructors)
                        {
                            ProcessConstructorDeclaration(constructorDeclaration, units);
                        }

                        foreach (var fieldDeclaration in classAssetGenerator.Data.variables)
                        {
                            ProcessFieldDeclaration(fieldDeclaration, units);
                        }

                        foreach (var methodDeclaration in classAssetGenerator.Data.methods)
                        {
                            ProcessMethodDeclaration(methodDeclaration, units);
                        }
                        var ordered = units.OrderableSearchFilter(unitId ?? "", (value) => value.Item2.ToString());
                        GraphWindow.OpenActive(ordered.First().result.Item1);
                        HandleClickableRegionClick(unitId, line);
                    }
                }
                else if (code is StructAssetGenerator structAssetGenerator)
                {
                    if (structAssetGenerator.Data != null)
                    {
                        List<(GraphReference, Unit)> units = new List<(GraphReference, Unit)>();
                        foreach (var constructorDeclaration in structAssetGenerator.Data.constructors)
                        {
                            ProcessConstructorDeclaration(constructorDeclaration, units);
                        }

                        foreach (var fieldDeclaration in structAssetGenerator.Data.variables)
                        {
                            ProcessFieldDeclaration(fieldDeclaration, units);
                        }

                        foreach (var methodDeclaration in structAssetGenerator.Data.methods)
                        {
                            ProcessMethodDeclaration(methodDeclaration, units);
                        }
                        var ordered = units.OrderableSearchFilter(unitId ?? "", (value) => value.Item2.ToString());
                        GraphWindow.OpenActive(ordered.First().result.Item1);
                        HandleClickableRegionClick(unitId, line);
                    }
                }
                else if (code is ScriptGraphAssetGenerator graphAssetGenerator)
                {
                    if (graphAssetGenerator.Data != null)
                    {
                        List<(GraphReference, Unit)> units = new List<(GraphReference, Unit)>();
                        units = TraverseFlowGraph(graphAssetGenerator.Data.GetReference() as GraphReference).ToList();
                        var ordered = units.OrderableSearchFilter(unitId ?? "", (value) => value.Item2.ToString());
                        GraphWindow.OpenActive(ordered.First().result.Item1);
                    }
                }
            }
        }

        private void ProcessMethodDeclaration(MethodDeclaration methodDeclaration, List<(GraphReference, Unit)> units)
        {
            if (methodDeclaration.classAsset != null)
            {
                AddUnitsFromClassAsset(methodDeclaration.classAsset, units);
            }
            else if (methodDeclaration.structAsset != null)
            {
                AddUnitsFromStructAsset(methodDeclaration.structAsset, units);
            }
        }

        private void ProcessConstructorDeclaration(ConstructorDeclaration constructorDeclaration, List<(GraphReference, Unit)> units)
        {
            if (constructorDeclaration.classAsset != null)
            {
                AddUnitsFromClassAsset(constructorDeclaration.classAsset, units);
            }
            else if (constructorDeclaration.structAsset != null)
            {
                AddUnitsFromStructAsset(constructorDeclaration.structAsset, units);
            }
        }

        private void ProcessFieldDeclaration(FieldDeclaration fieldDeclaration, List<(GraphReference, Unit)> units)
        {
            if (fieldDeclaration.classAsset != null)
            {
                AddUnitsFromClassAsset(fieldDeclaration.classAsset, units);
            }
            else if (fieldDeclaration.structAsset != null)
            {
                AddUnitsFromStructAsset(fieldDeclaration.structAsset, units);
            }
        }

        private void AddUnitsFromClassAsset(ClassAsset classAsset, List<(GraphReference, Unit)> units)
        {
            foreach (var method in classAsset.methods)
                units.AddRange(TraverseFlowGraph(method.GetReference() as GraphReference));

            foreach (var constructor in classAsset.constructors)
                units.AddRange(TraverseFlowGraph(constructor.GetReference() as GraphReference));

            foreach (var variable in classAsset.variables)
            {
                if (variable.isProperty)
                {
                    if (variable.get)
                        units.AddRange(TraverseFlowGraph(variable.getter.GetReference() as GraphReference));
                    if (variable.set)
                        units.AddRange(TraverseFlowGraph(variable.setter.GetReference() as GraphReference));
                }
            }
        }

        private void AddUnitsFromStructAsset(StructAsset structAsset, List<(GraphReference, Unit)> units)
        {
            foreach (var method in structAsset.methods)
                units.AddRange(TraverseFlowGraph(method.GetReference() as GraphReference));

            foreach (var constructor in structAsset.constructors)
                units.AddRange(TraverseFlowGraph(constructor.GetReference() as GraphReference));

            foreach (var variable in structAsset.variables)
            {
                if (variable.isProperty)
                {
                    if (variable.get)
                        units.AddRange(TraverseFlowGraph(variable.getter.GetReference() as GraphReference));
                    if (variable.set)
                        units.AddRange(TraverseFlowGraph(variable.setter.GetReference() as GraphReference));
                }
            }
        }

        List<(GraphReference, SubgraphUnit)> GetUnitPath(GraphReference reference)
        {
            List<(GraphReference, SubgraphUnit)> nodePath = new List<(GraphReference, SubgraphUnit)>() { (reference, !reference.isRoot ? reference.GetParent<SubgraphUnit>() : null) };
            while (reference.ParentReference(false) != null)
            {
                reference = reference.ParentReference(false);
                nodePath.Add((reference, !reference.isRoot ? reference.GetParent<SubgraphUnit>() : null));
            }
            nodePath.Reverse();
            return nodePath;
        }

        IEnumerable<(GraphReference, Unit)> TraverseFlowGraph(GraphReference graphReference)
        {
            var flowGraph = graphReference.graph as FlowGraph;
            if (flowGraph == null) yield break;
            var units = flowGraph.units;
            foreach (var element in units)
            {
                var unit = element as Unit;
                switch (unit)
                {
                    case SubgraphUnit subgraphUnit:
                        {
                            var subGraph = subgraphUnit.nest.embed ?? subgraphUnit.nest.graph;
                            if (subGraph == null) continue;
                            yield return (graphReference, subgraphUnit);
                            var childReference = graphReference.ChildReference(subgraphUnit, false);
                            foreach (var item in TraverseFlowGraph(childReference))
                            {
                                yield return item;
                            }

                            break;
                        }
                    case StateUnit stateUnit:
                        {
                            var stateGraph = stateUnit.nest.embed ?? stateUnit.nest.graph;
                            if (stateGraph == null) continue;
                            // find state graph.
                            var childReference = graphReference.ChildReference(stateUnit, false);
                            foreach (var item in TraverseStateGraph(childReference))
                            {
                                yield return item;
                            }

                            break;
                        }
                    default:
                        yield return (graphReference, unit);
                        break;
                }
            }
        }

        IEnumerable<(GraphReference, Unit)> TraverseStateGraph(GraphReference graphReference)
        {
            var stateGraph = graphReference.graph as StateGraph;
            if (stateGraph == null) yield break;

            foreach (var state in stateGraph.states)
            {
                switch (state)
                {
                    case FlowState flowState:
                        {
                            var graph = flowState.nest.embed ?? flowState.nest.graph;

                            if (graph == null) continue;
                            var childReference = graphReference.ChildReference(flowState, false);
                            foreach (var item in TraverseFlowGraph(childReference))
                            {
                                yield return item;
                            }

                            break;
                        }
                    case SuperState superState:
                        {
                            var subStateGraph = superState.nest.embed ?? superState.nest.graph;
                            if (subStateGraph == null) continue;
                            var childReference = graphReference.ChildReference(superState, false);
                            foreach (var item in TraverseStateGraph(childReference))
                            {
                                yield return item;
                            }

                            break;
                        }
                    case AnyState:
                        continue;
                }
            }

            foreach (var transition in stateGraph.transitions)
            {
                if (transition is not FlowStateTransition flowStateTransition) continue;
                var graph = flowStateTransition.nest.embed ?? flowStateTransition.nest.graph;
                if (graph == null) continue;
                var childReference = graphReference.ChildReference(flowStateTransition, false);
                foreach (var item in TraverseFlowGraph(childReference))
                {
                    yield return item;
                }
            }
        }

        private GraphReference GetFirstReference()
        {
            if (asset is ClassAsset classAsset)
            {
                var variables = classAsset.variables.Where(variable => variable.isProperty);
                if (classAsset.constructors.Count > 0 && ((classAsset.constructors[0].GetReference() as GraphReference).graph as FlowGraph).units.Count > 1)
                {
                    return classAsset.constructors[0].GetReference() as GraphReference;
                }
                else if (variables.Count() > 0)
                {
                    var first = variables.First();
                    if (first.get && ((first.getter.GetReference() as GraphReference).graph as FlowGraph).units.Count > 1)
                    {
                        return first.getter.GetReference() as GraphReference;
                    }
                    else if (first.set && ((first.setter.GetReference() as GraphReference).graph as FlowGraph).units.Count > 1)
                    {
                        return first.setter.GetReference() as GraphReference;
                    }
                }
                else if (classAsset.methods.Count > 0 && ((classAsset.methods[0].GetReference() as GraphReference).graph as FlowGraph).units.Count > 1)
                {
                    return classAsset.methods[0].GetReference() as GraphReference;
                }
            }
            else if (asset is StructAsset structAsset)
            {
                var variables = structAsset.variables.Where(variable => variable.isProperty);
                if (structAsset.constructors.Count > 0)
                {
                    return structAsset.constructors[0].GetReference() as GraphReference;
                }
                else if (variables.Count() > 0)
                {
                    var first = variables.First();
                    if (first.get)
                    {
                        return first.getter.GetReference() as GraphReference;
                    }
                    else if (first.set)
                    {
                        return first.setter.GetReference() as GraphReference;
                    }
                }
                else if (structAsset.methods.Count > 0)
                {
                    return structAsset.methods[0].GetReference() as GraphReference;
                }
            }
            return null;
        }

    }
}