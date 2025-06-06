﻿
using Unity.VisualScripting.Community.Utility;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Listens for an event by type, rather than by name.  In other respects, it acts similar to the built-in Custom Unit
    /// </summary>
    [UnitCategory("Events/Community")]
    [UnitTitle("Defined Event")]
    [RenamedFrom("Bolt.Addons.Community.DefinedEvents.Units.DefinedEvent")]
    [RenamedFrom("Bolt.Addons.Community.DefinedEvents.Units.DefinedEventUnit")]
    public class DefinedEventNode : GameObjectEventUnit<DefinedEventArgs>, IDefinedEventNode
    {
        const string EventName = "OnDefinedEvent";

        #region Previous Event Type Handling (for backward compatibility)
        [SerializeAs(nameof(eventType))]
        private System.Type _eventType;


        [DoNotSerialize]
        //[InspectableIf(nameof(IsNotRestricted))]
        public System.Type eventType
        {
            get
            {
                return _eventType;
            }
            set
            {
                _eventType = value;
            }
        }

        [DoNotSerialize]
        //[UnitHeaderInspectable]
        //[InspectableIf(nameof(IsRestricted))]
        public System.Type restrictedEventType
        {
            get
            {
                return _eventType;
            }
            set
            {
                _eventType = value;
            }
        }

        #endregion

        #region New Event Type Handling
        [SerializeAs(nameof(NeweventType))]
        private DefinedEventType New_eventType;

        [DoNotSerialize]
        [InspectableIf(nameof(IsNotRestricted))]
        [InspectorLabel("EventType")]
        public DefinedEventType NeweventType
        {
            get { return New_eventType; }
            set { New_eventType = value; }
        }

        [DoNotSerialize]
        [UnitHeaderInspectable]
        [InspectableIf(nameof(IsRestricted))]
        [InspectorLabel("EventType")]
        public DefinedEventType NewrestrictedEventType
        {
            get { return New_eventType; }
            set { New_eventType = value; }
        }

        public bool IsRestricted
        {
            get { return CommunityOptionFetcher.DefinedEvent_RestrictEventTypes; }
        }

        public bool IsNotRestricted
        {
            get { return !IsRestricted; }
        }
        #endregion

        [DoNotSerialize]
        public List<ValueOutput> outputPorts { get; } = new List<ValueOutput>();

        [DoNotSerialize]
        private ReflectedInfo Info;

        public override Type MessageListenerType => null;

        protected override string hookName => EventName;

        protected override bool ShouldTrigger(Flow flow, DefinedEventArgs args)
        {
            return args.eventData.GetType() == NeweventType.type;
        }

        protected override void Definition()
        {
            base.Definition();

            // For backward compatibility, convert the Type to IDefinedEventType
            if (restrictedEventType != null)
            {
                NewrestrictedEventType = new DefinedEventType(restrictedEventType);
                restrictedEventType = null;
            }

            if (NewrestrictedEventType == null)
            {
                NewrestrictedEventType = new DefinedEventType();
            }

            BuildFromInfo();
        }

        private void BuildFromInfo()
        {
            outputPorts.Clear();
            if (NeweventType.type == null)
                return;

            Info = ReflectedInfo.For(NeweventType.type);
            foreach (var field in Info.reflectedFields)
            {
                outputPorts.Add(ValueOutput(field.Value.FieldType, field.Value.Name));
            }


            foreach (var property in Info.reflectedProperties)
            {
                outputPorts.Add(ValueOutput(property.Value.PropertyType, property.Value.Name));
            }
        }

        protected override void AssignArguments(Flow flow, DefinedEventArgs args)
        {
            for (var i = 0; i < outputPorts.Count; i++)
            {
                var outputPort = outputPorts[i];
                var key = outputPort.key;
                if (Info.reflectedFields.ContainsKey(key))
                {
                    var reflectedField = Info.reflectedFields[key];
                    flow.SetValue(outputPort, reflectedField.GetValue(args.eventData));
                }
                else if (Info.reflectedProperties.ContainsKey(key))
                {
                    var reflectedProperty = Info.reflectedProperties[key];
                    flow.SetValue(outputPort, reflectedProperty.GetValue(args.eventData));
                }
            }
        }
        public override EventHook GetHook(GraphReference reference)
        {
            var refData = reference.GetElementData<Data>(this);
            return ConstructHook(refData.target, New_eventType.type);
        }

        private static EventHook ConstructHook(GameObject target, Type eventType)
        {
            EventHook hook;
            if (DefinedEventSupport.IsOptimized())
                hook = new EventHook(EventName, target, eventType.GetTypeInfo().FullName);
            else
                hook = new EventHook(EventName, target);
            return hook;
        }


        public static void Trigger(GameObject target, object eventData)
        {
            var eventHook = ConstructHook(target, eventData.GetType());
            EventBus.Trigger(eventHook, new DefinedEventArgs(eventData));
        }



        public static IDisposable RegisterListener<T>(GameObject target, Action<T> onEvent)
        {
            var eventHook = ConstructHook(target, typeof(T));
            Action<DefinedEventArgs> action = (x) =>
            {
                if (x.eventData.GetType() == typeof(T))
                    onEvent((T)x.eventData);
            };
            EventBus.Register<DefinedEventArgs>(eventHook, action);

            return Disposable.Create(() => { EventBus.Unregister(eventHook, action); });
        }
    }
}
