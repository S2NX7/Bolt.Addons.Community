using System;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting.Community.Libraries.CSharp;

namespace Unity.VisualScripting.Community
{
    public class ClickableStringBuilder
    {
        private readonly Unit unit;
        private readonly List<(string value, bool clickable)> segments = new();
        private bool ignoreContextActive = false;

        private ClickableStringBuilder(Unit unit, string value, bool clickable)
        {
            this.unit = unit;
            segments.Add((value, clickable));
        }

        public static ClickableStringBuilder CreateString(Unit unit, string initial, bool clickable)
        {
            return new ClickableStringBuilder(unit, initial, clickable);
        }

        /// <summary>
        /// Will invert all calls to use Ignore instead of Clickable unless explicitly called.
        /// </summary>
        public ClickableStringBuilder IgnoreContext()
        {
            ignoreContextActive = true;
            return this;
        }

        /// <summary>
        /// Returns to the default behavior of Clickable.
        /// </summary>
        public ClickableStringBuilder EndIgnoreContext()
        {
            ignoreContextActive = false;
            return this;
        }

        public ClickableStringBuilder Clickable(string value)
        {
            segments.Add((value, true));
            return this;
        }

        public ClickableStringBuilder Ignore(string value)
        {
            segments.Add((value, false));
            return this;
        }

        private ClickableStringBuilder Add(string value)
        {
            return ignoreContextActive ? Ignore(value) : Clickable(value);
        }

        /// <summary>
        /// Ignores the current context and uses the provided ignore and clickable code.
        /// </summary>
        /// <param name="ignoreCondition">Condition for ignore string</param>
        /// <param name="ignore">Ignore Code</param>
        /// <param name="clickable">Clickable Code</param>
        public ClickableStringBuilder Select(bool ignoreCondition, string ignore, string clickable)
        {
            return ignoreCondition ? Ignore(ignore) : Clickable(clickable);
        }
        #region Parentheses
        public ClickableStringBuilder OpenParentheses() => Add("(");
        public ClickableStringBuilder OpenParentheses(string before) => Add(before + "(");
        public ClickableStringBuilder CloseParentheses() => Add(")");
        public ClickableStringBuilder CloseParentheses(string after) => Add(")" + after);
        public ClickableStringBuilder Parentheses(Action<ClickableStringBuilder> inner)
        {
            Add("(");
            inner(this);
            Add(")");

            return this;
        }

        public ClickableStringBuilder Parentheses(string before, Action<ClickableStringBuilder> inner, string after = "")
        {
            Add(before);
            Add("(");
            inner(this);
            Add(")");
            Add(after);
            return this;
        }
        #endregion

        #region Brace
        public ClickableStringBuilder OpenBrace(int indent = 0)
        {
            Ignore(CodeBuilder.Indent(indent));
            return Add("{");
        }
        public ClickableStringBuilder OpenBrace(string before, int indent = 0)
        {
            Ignore(CodeBuilder.Indent(indent));
            return Add(before + "{");
        }
        public ClickableStringBuilder CloseBrace(int indent = 0)
        {
            Ignore(CodeBuilder.Indent(indent));
            return Add("}");
        }
        public ClickableStringBuilder CloseBrace(string after, int indent = 0)
        {
            Ignore(CodeBuilder.Indent(indent));
            return Add(after + "}");
        }

        /// <summary>
        /// Generate { }
        /// </summary>
        /// <param name="inner">Generate code inside the braces</param>
        /// <param name="newLine">Place { and } on a NewLine </param>
        /// <param name="indent">The indent for { and } </param>
        public ClickableStringBuilder Braces(Action<ClickableStringBuilder> inner, bool newLine, int indent = 0)
        {
            OpenBrace(indent);
            if (newLine) NewLine();
            inner(this);
            if (newLine) NewLine();
            CloseBrace(indent);
            return this;
        }

        /// <summary>
        /// Generate { }
        /// </summary>
        /// <param name="before">String before { </param>
        /// <param name="inner">Generate code inside the braces</param>
        /// <param name="newLine">Place { and } on a NewLine </param>
        /// <param name="after">String after } </param>
        /// <param name="indent">The indent for { and }</param>
        public ClickableStringBuilder Braces(string before, Action<ClickableStringBuilder> inner, bool newLine, string after = "", int indent = 0)
        {
            OpenBrace(before, indent);
            if (newLine) NewLine();
            inner(this);
            if (newLine) NewLine();
            CloseBrace(after, indent);
            return this;
        }
        #endregion
        public ClickableStringBuilder Space() => Add(" ");
        public ClickableStringBuilder Space(int count) => Add(new string(' ', count));
        public ClickableStringBuilder Comma(string after = "") => Add("," + after);
        public ClickableStringBuilder Dot() => Add(".");
        public ClickableStringBuilder End() => Add(CodeBuilder.End());
        public ClickableStringBuilder NewLine() => Ignore("\n");
        public ClickableStringBuilder Indent() => Add(CodeBuilder.Indent(1));
        public ClickableStringBuilder Indent(int indent) => Add(CodeBuilder.Indent(indent));

        public ClickableStringBuilder Cast(Type castType, bool shouldCast, bool convertType = true)
        {
            if (castType == null || !shouldCast) return this;

            var code = Build();
            var builder = CreateString(unit, "", true).Ignore(convertType ? CodeBuilder.CastAs(code, castType, ignoreContextActive ? null : unit) : CodeBuilder.Cast(code, castType, ignoreContextActive ? null : unit));
            builder.ignoreContextActive = ignoreContextActive;
            return builder;
        }

        public string Build()
        {
            var result = new StringBuilder();
            var streak = new StringBuilder();
            bool? currentClickable = null;

            foreach (var (value, clickable) in segments)
            {
                if (currentClickable == null)
                {
                    currentClickable = clickable;
                    streak.Append(value);
                }
                else if (clickable == currentClickable)
                {
                    streak.Append(value);
                }
                else
                {
                    if (currentClickable.Value)
                        result.Append(CodeUtility.MakeClickable(unit, streak.ToString()));
                    else
                        result.Append(streak.ToString());

                    streak.Clear();
                    streak.Append(value);
                    currentClickable = clickable;
                }
            }

            if (streak.Length > 0)
            {
                if (currentClickable == true)
                    result.Append(CodeUtility.MakeClickable(unit, streak.ToString()));
                else
                    result.Append(streak.ToString());
            }

            return result.ToString();
        }
        public override string ToString() => Build();
        public static implicit operator string(ClickableStringBuilder builder) => builder.Build();
    }

    public static class XClickableStringBuilder
    {
        public static ClickableStringBuilder CreateClickableString(this Unit unit, string initial = "")
        {
            return ClickableStringBuilder.CreateString(unit, initial, true);
        }

        /// <summary>
        /// Creates a ClickableStringBuilder with Ignore Context enabled.
        /// </summary>
        public static ClickableStringBuilder CreateIgnoreString(this Unit unit, string initial = "")
        {
            return ClickableStringBuilder.CreateString(unit, initial, false).IgnoreContext();
        }
    }
}