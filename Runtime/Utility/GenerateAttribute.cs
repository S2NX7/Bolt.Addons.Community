using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Indicates that code generators should attempt to include this property when producing literal values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class GeneratePropertyAttribute : Attribute
    {
    }
}