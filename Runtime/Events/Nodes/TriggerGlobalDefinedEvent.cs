using System;
using System.Collections.Generic;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// Triggers a Global Event to all Global Defined Events listening for this type.
    /// </summary>
    [UnitCategory("Events/Community")]
    [UnitTitle("Trigger Global Defined Event")]
    [TypeIcon(typeof(BoltUnityEvent))]
    [RenamedFrom("Bolt.Addons.Community.DefinedEvents.Units.TriggerGlobalDefinedEvent")]
    public class TriggerGlobalDefinedEvent : Unit
    {

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
        public Type NeweventType
        {
            get { return New_eventType.type; }
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
        public List<ValueInput> inputPorts { get; } = new List<ValueInput>();

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput enter { get; private set; }

        /// <summary>
        /// The action to do after the event has been triggered.
        /// </summary>
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput exit { get; private set; }

        [DoNotSerialize]
        private ReflectedInfo Info;

        protected override void Definition()
        {
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

            enter = ControlInput(nameof(enter), Trigger);

            exit = ControlOutput(nameof(exit));


            BuildFromInfo();

            Succession(enter, exit);
        }

        private void BuildFromInfo()
        {
            inputPorts.Clear();
            if (New_eventType == null || New_eventType.type == null || IsNotRestricted)
                return;

            if (IsRestricted)
            {
                Info = ReflectedInfo.For(New_eventType.type);
                foreach (var field in Info.reflectedFields)
                {
                    if (field.Value.FieldType == typeof(bool))
                        inputPorts.Add(ValueInput(field.Value.Name, false));
                    else if (field.Value.FieldType == typeof(int))
                        inputPorts.Add(ValueInput(field.Value.Name, 0));
                    else if (field.Value.FieldType == typeof(float))
                        inputPorts.Add(ValueInput(field.Value.Name, 0.0f));
                    else if (field.Value.FieldType == typeof(string))
                        inputPorts.Add(ValueInput(field.Value.Name, ""));
                    else if (field.Value.FieldType == typeof(GameObject))
                        inputPorts.Add(ValueInput<GameObject>(field.Value.Name, null).NullMeansSelf());
                    else
                        inputPorts.Add(ValueInput(field.Value.FieldType, field.Value.Name));
                }


                foreach (var property in Info.reflectedProperties)
                {
                    if (property.Value.PropertyType == typeof(bool))
                        inputPorts.Add(ValueInput(property.Value.Name, false));
                    else if (property.Value.PropertyType == typeof(int))
                        inputPorts.Add(ValueInput(property.Value.Name, 0));
                    else if (property.Value.PropertyType == typeof(float))
                        inputPorts.Add(ValueInput(property.Value.Name, 0.0f));
                    else if (property.Value.PropertyType == typeof(string))
                        inputPorts.Add(ValueInput(property.Value.Name, ""));
                    else if (property.Value.PropertyType == typeof(GameObject))
                        inputPorts.Add(ValueInput<GameObject>(property.Value.Name, null).NullMeansSelf());
                    else
                        inputPorts.Add(ValueInput(property.Value.PropertyType, property.Value.Name));
                }
            }
            else
            {
                if (NeweventType == typeof(bool))
                    inputPorts.Add(ValueInput(NeweventType.As().CSharpName(false, false, false), false));
                else if (NeweventType == typeof(int))
                    inputPorts.Add(ValueInput(NeweventType.As().CSharpName(false, false, false), 0));
                else if (NeweventType == typeof(float))
                    inputPorts.Add(ValueInput(NeweventType.As().CSharpName(false, false, false), 0.0f));
                else if (NeweventType == typeof(string))
                    inputPorts.Add(ValueInput(NeweventType.As().CSharpName(false, false, false), ""));
                else if (NeweventType == typeof(GameObject))
                    inputPorts.Add(ValueInput<GameObject>(NeweventType.As().CSharpName(false, false, false), null).NullMeansSelf());
                else
                    inputPorts.Add(ValueInput(NeweventType, NeweventType.As().CSharpName(false, false, false)));
            }
        }

        private ControlOutput Trigger(Flow flow)
        {
            if (New_eventType.type == null) return exit;

            if (IsRestricted)
            {
                var eventInstance = System.Activator.CreateInstance(New_eventType.type);

                for (var i = 0; i < inputPorts.Count; i++)
                {
                    var inputPort = inputPorts[i];
                    var key = inputPort.key;
                    var value = flow.GetValue(inputPort);
                    if (Info.reflectedFields.ContainsKey(key))
                    {
                        var reflectedField = Info.reflectedFields[key];
                        reflectedField.SetValue(eventInstance, value);
                    }
                    else if (Info.reflectedProperties.ContainsKey(key))
                    {
                        var reflectedProperty = Info.reflectedProperties[key];
                        reflectedProperty.SetValue(eventInstance, value);
                    }
                }

                GlobalDefinedEventNode.Trigger(eventInstance);
            }
            else
            {
                GlobalDefinedEventNode.Trigger(flow.GetValue(inputPorts.Find(port => port.key == NeweventType.As().CSharpName(false, false, false))));
            }

            return exit;
        }
    }

}