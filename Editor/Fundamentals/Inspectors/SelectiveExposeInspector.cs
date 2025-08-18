using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [Inspector(typeof(SelectiveExpose))]
    public sealed class SelectiveExposeInspector : Inspector
    {
        public SelectiveExposeInspector(Metadata metadata) : base(metadata)
        {
        }

        // Cache for foldout state
        private bool foldout = true;

        protected override float GetHeight(float width, GUIContent label)
        {
            var selectiveExpose = (SelectiveExpose)metadata.value;
            if (selectiveExpose.type == null) return EditorGUIUtility.singleLineHeight * 3;

            var members = selectiveExpose.type.GetMembers()
                .Where(m => (m is System.Reflection.FieldInfo || m is System.Reflection.PropertyInfo) && selectiveExpose.Include(m.ToManipulator()))
                .Select(m => m.Name)
                .ToList();

            return (foldout ? (members.Count + 1) * EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight) + EditorGUIUtility.singleLineHeight;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            var selectiveExpose = (SelectiveExpose)metadata.value;

            Rect labelRect = new Rect(position.x, position.y, 40, EditorGUIUtility.singleLineHeight);
            GUI.Label(labelRect, "Type:");

            Rect buttonRect = new Rect(position.x + 45, position.y, position.width - 45, EditorGUIUtility.singleLineHeight);
            BeginBlock(metadata["type"], position);
            Type type = selectiveExpose.type;
            if (GUI.Button(buttonRect, new GUIContent(((Type)metadata["type"].value).DisplayName(), ((Type)metadata["type"].value).Icon()[IconSize.Small])))
            {
                TypeBuilderWindow.ShowWindow(labelRect, metadata["type"], true, new Type[0], () =>
                {
                    selectiveExpose.Define();
                    selectiveExpose.Describe();
                }, _type =>
                {
                    if (type != _type)
                    {
                        selectiveExpose.selectedMembers.Clear();
                    }
                    type = _type;
                    selectiveExpose.type = _type;
                });
            }
            if (EndBlock(metadata["type"]))
            {
                metadata["type"].RecordUndo();
                metadata["type"].value = type;
            }

            position.y += EditorGUIUtility.singleLineHeight;
            position.x += 10;
            foldout = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), foldout, "Select Members", true);
            if (foldout)
            {
                position.y += EditorGUIUtility.singleLineHeight;

                if (selectiveExpose.type != null)
                {
                    var members = selectiveExpose.type.GetMembers()
                        .Where(m => m is System.Reflection.FieldInfo || m is System.Reflection.PropertyInfo)
                        .Select(m => m.ToManipulator());
                    BeginBlock(metadata["selectedMembers"], position);
                    var selectedMembers = selectiveExpose.selectedMembers ?? new List<string>();
                    foreach (var member in members)
                    {
                        var memberName = member.name;
                        if (!selectiveExpose.Include(member)) continue;
                        bool isSelected = selectedMembers.Contains(memberName);
                        bool newSelection = EditorGUI.Toggle(new Rect(position.x + 10, position.y, position.width - 10, EditorGUIUtility.singleLineHeight), memberName, isSelected);

                        if (newSelection && !isSelected)
                        {
                            selectedMembers.Add(memberName);
                        }
                        else if (!newSelection && isSelected)
                        {
                            selectedMembers.Remove(memberName);
                        }

                        position.y += EditorGUIUtility.singleLineHeight;
                    }
                    if (EndBlock(metadata["selectedMembers"]))
                    {
                        metadata["selectedMembers"].RecordUndo();
                        metadata["selectedMembers"].value = selectedMembers;
                    }

                }
                else
                {
                    EditorGUI.LabelField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), "No type selected.");
                }
            }
        }
    }
}