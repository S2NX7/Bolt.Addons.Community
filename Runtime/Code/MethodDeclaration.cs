﻿using Unity.VisualScripting;
using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Utility;
using Unity.VisualScripting.Community.Libraries.CSharp;

namespace Unity.VisualScripting.Community
{
    [Serializable]
    [Inspectable]
    [TypeIcon(typeof(Method))]
    [RenamedFrom("Bolt.Addons.Community.Code.MethodDeclaration")]
    public abstract class MethodDeclaration : Macro<FlowGraph>
    {
        [Inspectable]
        public string methodName;

        [Inspectable]
        [TypeFilter(Abstract = true, Classes = true, Enums = true, Generic = false, Interfaces = true,
            Nested = true, NonPublic = false, NonSerializable = true, Object = true, Obsolete = false, OpenConstructedGeneric = false,
            Primitives = true, Public = true, Reference = true, Sealed = true, Static = false, Structs = true, Value = true)]
        public Type returnType = typeof(Libraries.CSharp.Void);

        [SerializeField]
        [HideInInspector]
        private string qualifiedReturnTypeName;

        /// <summary>
        /// Left this to not overwrite current methodParameters
        /// and instead get the parameters from this and move it to the serializtion variable
        /// </summary>
        [SerializeField]
        [HideInInspector]
        private string serializedParams;

        [SerializeField]
        [HideInInspector]
        private SerializationData serialization;

        [Inspectable]
        public ClassAsset classAsset;

        [Inspectable]
        public StructAsset structAsset;

        [Serialize]
        [InspectorWide]
        public List<TypeParam> parameters = new List<TypeParam>();

        [Serialize]
        public List<AttributeDeclaration> attributes = new List<AttributeDeclaration>();

        public AccessModifier scope = AccessModifier.Public;

        public MethodModifier modifier = MethodModifier.None;

#if UNITY_EDITOR
        public bool opened;
        public bool parametersOpened;
        public bool attributesOpened;
#endif

        public override FlowGraph DefaultGraph()
        {
            return new FlowGraph();
        }

        protected override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            if (!(string.IsNullOrWhiteSpace(qualifiedReturnTypeName) || string.IsNullOrEmpty(qualifiedReturnTypeName)))
            {
                returnType = Type.GetType(qualifiedReturnTypeName);
            }

            foreach (var param in parameters)
            {
                param.OnAfterDeserialize();
            }
            // if (!string.IsNullOrEmpty(serializedParams))
            // {
            //     parameters = (List<TypeParam>)new SerializationData(serializedParams).Deserialize();
            //     serializedParams = null;
            // }
            // else if (!string.IsNullOrEmpty(serialization.json))
            // {
            //     parameters = (List<TypeParam>)serialization.Deserialize();
            // }
        }

        protected override void OnBeforeSerialize()
        {
            base.OnBeforeSerialize();

            if (returnType == null)
            {
                qualifiedReturnTypeName = string.Empty;
                return;
            }

            qualifiedReturnTypeName = returnType.AssemblyQualifiedName;
            foreach (var param in parameters)
            {
                param.OnBeforeSerialize();
            }
            // if (parameters == null)
            // {
            //     serializedParams = null;
            //     serialization = new SerializationData();
            //     return;
            // }

            // serialization = parameters.Serialize();
        }
    }
    /// <summary>
    /// This is a empty class used for the typeIcon
    /// </summary>
    public class Method
    {
    }
}