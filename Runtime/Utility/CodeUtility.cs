using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.VisualScripting.Community.Libraries.CSharp;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    public static class CodeUtility
    {
        private static readonly Regex RemoveHighlightsRegex = new(@"<b class='highlight'>(.*?)<\/b>", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly ConcurrentDictionary<string, string> RemoveAllCache = new();

        private static TValue GetOrAddRegex<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory)
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = valueFactory(key);
                dictionary[key] = value;
            }
            return value;
        }

        private static readonly Regex RemoveSelectableTagWrapperRegex = new(@"⟦([^\⟧]+)⟧(.*?)⟧⟧", RegexOptions.Compiled | RegexOptions.Singleline);

        public static string RemoveAllSelectableTags(string code)
        {
            if (RemoveAllCache.TryGetValue(code, out string result))
                return result;

            result = RemoveSelectableTagWrapperRegex.Replace(code, "$2");
            RemoveAllCache[code] = result;
            return result;
        }


        public static string RemovePattern(string input, string startPattern, string endPattern)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(startPattern) || string.IsNullOrEmpty(endPattern))
                return input;

            StringBuilder result = new();
            int index = 0;

            while (index < input.Length)
            {
                int startIndex = input.IndexOf(startPattern, index);
                if (startIndex == -1)
                {
                    // No more patterns, append the rest of the string
                    result.Append(input[index..]);
                    break;
                }

                // Append text before the pattern
                result.Append(input[index..startIndex]);

                // Find the end of the pattern
                int endIndex = input.IndexOf(endPattern, startIndex + startPattern.Length);
                if (endIndex == -1)
                {
                    // If end pattern not found, treat it as invalid and append the rest of the string
                    result.Append(input[startIndex..]);
                    break;
                }

                // Skip the pattern
                index = endIndex + endPattern.Length;
            }

            return result.ToString();
        }
        public static string MakeSelectable(Unit unit, string code)
        {
            return $"⟦{unit}⟧{code}⟧⟧";
        }

        /// <summary>
        /// Used for the csharp preview to generate a tooltip
        /// </summary>
        /// <returns>The string with the ToolTip tags</returns>
        public static string ToolTip(string ToolTip, string notifyString, string code, bool highlight = true)
        {
            return CSharpPreviewSettings.ShouldGenerateTooltips ? $"[CommunityAddonsCodeToolTip({ToolTip})]{(highlight ? $"/* {notifyString} (Hover for more info) */".WarningHighlight() : $"/* {notifyString} (Hover for more info) */")}[CommunityAddonsCodeToolTipEnd] {code}" : code;
        }

        private static readonly Dictionary<string, string> ToolTipCache = new();
        private static readonly Dictionary<string, string> AllToolTipCache = new();
        private static readonly Regex ToolTipRegex = new(@"\[CommunityAddonsCodeToolTip\((.*?)\)\](.*?)\[CommunityAddonsCodeToolTipEnd\]", RegexOptions.Compiled);

        public static string RemoveAllToolTipTags(string code)
        {
            if (ToolTipCache.TryGetValue(code, out string result))
            {
                return result;
            }

            result = ToolTipRegex.Replace(code, "$2");
            ToolTipCache[code] = result;
            return result;
        }

        public static string RemoveAllToolTipTagsEntirely(string code)
        {
            if (AllToolTipCache.TryGetValue(code, out string result))
            {
                return result;
            }

            result = ToolTipRegex.Replace(code, string.Empty);
            AllToolTipCache[code] = result;
            return result;
        }

        public static string ExtractTooltip(string code, out string tooltip)
        {
            var match = ToolTipRegex.Match(code);
            if (match.Success)
            {
                tooltip = match.Groups[1].Value;
                return ToolTipRegex.Replace(code, "$2");
            }
            tooltip = string.Empty;
            return code;
        }

        private static readonly Regex RecommendationRegex = new(@"/\*\(Recommendation\) .*?\*/", RegexOptions.Compiled);

        public static string RemoveRecommendations(string code)
        {
            return RecommendationRegex.Replace(code, string.Empty);
        }

        public static string RemoveCustomHighlights(string highlightedCode)
        {
            return RemoveHighlightsRegex.Replace(highlightedCode, "$1");
        }

        public static string CleanCode(string code)
        {
            return RemoveAllSelectableTags(RemoveAllToolTipTagsEntirely(RemoveRecommendations(RemoveCustomHighlights(code))));
        }

        private static readonly Dictionary<string, List<ClickableRegion>> clickableRegionsCache = new();

        public static List<ClickableRegion> ExtractAndPopulateClickableRegions(string input)
        {
            if (clickableRegionsCache.TryGetValue(input, out var cachedRegions))
                return cachedRegions;

            const string openTag = "⟦";
            const string midTag = "⟧";
            const string closeTag = "⟧⟧";

            var regions = new List<ClickableRegion>();
            ReadOnlySpan<char> span = input;
            var lineBreaks = PrecomputeLineBreaks(span);

            int index = 0;
            while (index < span.Length)
            {
                int openIdx = span.Slice(index).IndexOf(openTag);
                if (openIdx == -1) break;
                openIdx += index;

                int midIdx = span.Slice(openIdx + openTag.Length).IndexOf(midTag);
                if (midIdx == -1) break;
                midIdx += openIdx + openTag.Length;

                var unitIdSpan = span.Slice(openIdx + openTag.Length, midIdx - (openIdx + openTag.Length));
                string unitId = unitIdSpan.ToString();

                int closeIdx = span.Slice(midIdx + midTag.Length).IndexOf(closeTag);
                if (closeIdx == -1) break;
                closeIdx += midIdx + midTag.Length;

                int codeStart = midIdx + midTag.Length;
                int codeLength = closeIdx - codeStart;

                ReadOnlySpan<char> codeSpan = span.Slice(codeStart, codeLength);
                string code = codeSpan.ToString();

                int startLine = GetLineNumber(lineBreaks, openIdx);
                int endLine = GetLineNumber(lineBreaks, closeIdx);

                var newRegion = new ClickableRegion(unitId, code, startLine, endLine);

                if (regions.Count > 0)
                {
                    var last = regions[^1];
                    if (last.unitId == unitId && last.endLine == startLine)
                    {
                        last.code += code;
                        last.endLine = endLine;
                        regions[^1] = last;
                    }
                    else
                    {
                        regions.Add(newRegion);
                    }
                }
                else
                {
                    regions.Add(newRegion);
                }

                index = closeIdx + closeTag.Length;
            }

            clickableRegionsCache[input] = regions;
            return regions;
        }

        public static List<int> PrecomputeLineBreaks(ReadOnlySpan<char> span)
        {
            var lineBreaks = new List<int> { 0 };
            int start = 0;
            int index;
            while ((index = span[start..].IndexOf('\n')) != -1)
            {
                start += index + 1;
                lineBreaks.Add(start);
            }

            return lineBreaks;
        }

        private static int GetLineNumber(List<int> lineBreaks, int charIndex)
        {
            int line = lineBreaks.BinarySearch(charIndex);
            return line >= 0 ? line : ~line - 1;
        }
    }
}