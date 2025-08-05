using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [UnitShortTitle("Logic")]
    [UnitTitle("Logic (Params)")]
    [UnitCategory("Community\\Logic")]
    [RenamedFrom("Bolt.Addons.Community.Logic.Units.LogicParams")]
    [RenamedFrom("Bolt.Addons.Community.Logic.Units.OrParam")]
    [RenamedFrom("Bolt.Addons.Community.Fundamentals.LogicParams")]
    public sealed class LogicParams : LogicParamNode
    {
        public LogicParams() { }

        [PortLabel("Result")]
        [DoNotSerialize]
        public ValueOutput output { get; private set; }

        protected override void Definition()
        {
            output = ValueOutput<bool>(nameof(output), GetValue);

            base.Definition();
        }

        protected override void BuildRelations(ValueInput arg)
        {
            Requirement(arg, output);
        }

        private bool GetValue(Flow flow)
        {
            switch (BranchingType)
            {
                case BranchType.And:
                    {
                        foreach (var item in arguments)
                            if (!flow.GetValue<bool>(item))
                                return false;
                        return true;
                    }
                case BranchType.Or:
                    {
                        foreach (var item in arguments)
                            if (flow.GetValue<bool>(item))
                                return true;
                        return false;
                    }
                case BranchType.GreaterThan:
                    {
                        float a = flow.GetValue<float>(arguments[0]);
                        float b = flow.GetValue<float>(arguments[1]);
                        return AllowEquals ? a >= b : a > b;
                    }

                case BranchType.LessThan:
                    {
                        float a = flow.GetValue<float>(arguments[0]);
                        float b = flow.GetValue<float>(arguments[1]);
                        return AllowEquals ? a <= b : a < b;
                    }
                case BranchType.Equal:
                    {
                        if (Numeric)
                        {
                            float reference = (float)flow.GetConvertedValue(arguments[0]);

                            for (int i = 1; i < arguments.Count; i++)
                            {
                                float compare = (float)flow.GetConvertedValue(arguments[i]);
                                if (!Mathf.Approximately(reference, compare))
                                    return false;
                            }
                        }
                        else
                        {
                            object reference = flow.GetValue<object>(arguments[0]);

                            for (int i = 1; i < arguments.Count; i++)
                            {
                                object compare = flow.GetValue<object>(arguments[i]);
                                if (!OperatorUtility.Equal(reference, compare))
                                    return false;
                            }
                        }

                        return true;
                    }
            }

            return false;
        }
    }
}