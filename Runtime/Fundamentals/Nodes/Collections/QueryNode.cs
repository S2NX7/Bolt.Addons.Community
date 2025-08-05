using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Unity.VisualScripting.Community
{
    /// <summary>
    /// A unit for using Linq querying operations in a Bolt graph.
    /// </summary>
    [UnitTitle("Query")]
    [UnitCategory("Community/Collections")]
    [TypeIcon(typeof(IEnumerable))]
    [RenamedFrom("Lasm.BoltExtensions.QueryUnit")]
    [RenamedFrom("Lasm.UAlive.QueryUnit")]
    [RenamedFrom("Bolt.Addons.Community.Fundamentals.Units.Collections.QueryUnit")]
    public class QueryNode : Unit
    {
        [UnitHeaderInspectable(null)]
        [InspectorWide]
        [Inspectable]
        [Serialize]
        public QueryOperation operation;

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput enter;

        [DoNotSerialize]
        public ControlOutput body;

        [DoNotSerialize]
        public ControlOutput exit;

        [DoNotSerialize]
        public ValueInput collection;

        [DoNotSerialize]
        public ValueInput condition;

        [DoNotSerialize]
        public ValueInput key;

        [DoNotSerialize]
        public ValueInput value;

        [DoNotSerialize]
        public ValueOutput item;

        [DoNotSerialize]
        public ValueOutput result;

        private object current;
        private IEnumerable<object> output;
        private object single;
        private bool outCondition;
        [Obsolete]
        private string serializedOperation;

        protected override void Definition()
        {
            enter = ControlInput("enter", (flow) =>
            {
                PerformOperation(flow);
                return exit;
            });


            var showItem = true;
            var showBody = true;

            collection = ValueInput(operation == QueryOperation.Sum ? typeof(IEnumerable<float>) : typeof(IEnumerable<object>), "collection");
            switch (operation)
            {
                case QueryOperation.Any:
                case QueryOperation.Count:
                case QueryOperation.Sum:
                    showItem = false;
                    showBody = false;
                    break;
                case QueryOperation.AnyWithCondition:
                case QueryOperation.First:
                case QueryOperation.FirstOrDefault:
                case QueryOperation.Single:
                case QueryOperation.Last:
                case QueryOperation.LastOrDefault:
                case QueryOperation.Where:
                    condition = ValueInput<bool>("condition");
                    break;
                case QueryOperation.OrderBy:
                case QueryOperation.OrderByDescending:
                    key = ValueInput<object>("key");
                    break;
                case QueryOperation.Select:
                    value = ValueInput<object>("value");
                    break;
                case QueryOperation.Skip:
                case QueryOperation.Take:
                    showItem = false;
                    showBody = false;
                    value = ValueInput<int>("value", default);
                    break;
            }

            exit = ControlOutput("exit");
            if (showBody) body = ControlOutput("body");
            if (showItem) item = ValueOutput<object>("item", (flow) => { return current; });

            switch (operation)
            {
                case QueryOperation.Any:
                case QueryOperation.AnyWithCondition:
                case QueryOperation.Count:
                    result = ValueOutput<bool>("result", (flow) => { return outCondition; });
                    break;
                case QueryOperation.First:
                case QueryOperation.FirstOrDefault:
                case QueryOperation.Single:
                case QueryOperation.Last:
                case QueryOperation.LastOrDefault:
                    result = ValueOutput<object>("result", (flow) => { return single; });
                    break;
                case QueryOperation.OrderBy:
                case QueryOperation.OrderByDescending:
                case QueryOperation.Where:
                case QueryOperation.Select:
                case QueryOperation.Skip:
                case QueryOperation.Take:
                    result = ValueOutput("result", (flow) => { return output; });
                    break;
                case QueryOperation.Sum:
                    result = ValueOutput("result", (flow) => { return output.Cast<float>().Sum(); });
                    break;
            }

            Succession(enter, exit);

            if (showBody) Succession(enter, body);

            if (showItem) Assignment(enter, item);

            Requirement(collection, enter);

            switch (operation)
            {
                case QueryOperation.Any:
                case QueryOperation.Count:
                    break;
                case QueryOperation.AnyWithCondition:
                case QueryOperation.First:
                case QueryOperation.FirstOrDefault:
                case QueryOperation.Single:
                case QueryOperation.Last:
                case QueryOperation.LastOrDefault:
                case QueryOperation.Where:
                    Requirement(condition, enter);
                    break;
                case QueryOperation.OrderBy:
                case QueryOperation.OrderByDescending:
                    Requirement(key, enter);
                    break;
                case QueryOperation.Select:
                    Requirement(value, enter);
                    break;
                case QueryOperation.Skip:
                case QueryOperation.Take:
                    Requirement(value, enter);
                    break;
            }
        }

        private void PerformOperation(Flow flow)
        {
            Flow _flow = null;
            switch (operation)
            {
                case QueryOperation.Any:
                    outCondition = Cast(flow.GetValue<IEnumerable>(collection)).Any();
                    break;

                case QueryOperation.AnyWithCondition:
                    outCondition = Cast(flow.GetValue<IEnumerable>(collection)).Any((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<bool>(condition);
                    });
                    break;

                case QueryOperation.First:
                    single = Cast(flow.GetValue<IEnumerable>(collection)).First((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<bool>(condition);
                    });
                    break;

                case QueryOperation.FirstOrDefault:
                    single = Cast(flow.GetValue<IEnumerable>(collection)).FirstOrDefault((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<bool>(condition);
                    });
                    break;

                case QueryOperation.OrderBy:
                    output = Cast(flow.GetValue<IEnumerable>(collection)).OrderBy((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<object>(key);
                    });
                    break;

                case QueryOperation.OrderByDescending:
                    output = Cast(flow.GetValue<IEnumerable>(collection)).OrderByDescending((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<object>(key);
                    });
                    break;

                case QueryOperation.Single:
                    single = Cast(flow.GetValue<IEnumerable>(collection)).Single((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<bool>(condition);
                    });
                    break;

                case QueryOperation.Where:
                    output = Cast(flow.GetValue<IEnumerable>(collection)).Where((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<bool>(condition);
                    });
                    break;

                case QueryOperation.Last:
                    single = Cast(flow.GetValue<IEnumerable>(collection)).Last((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<bool>(condition);
                    });
                    break;

                case QueryOperation.LastOrDefault:
                    single = Cast(flow.GetValue<IEnumerable>(collection)).LastOrDefault((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<bool>(condition);
                    });
                    break;

                case QueryOperation.Select:
                    output = Cast(flow.GetValue<IEnumerable>(collection)).Select((item) =>
                    {
                        current = item;
                        _flow = Flow.New(flow.stack.AsReference());
                        _flow.Invoke(body);
                        return _flow.GetValue<object>(value);
                    });
                    break;

                case QueryOperation.Skip:
                    output = Cast(flow.GetValue<IEnumerable>(collection)).Skip(flow.GetValue<int>(value));
                    break;

                case QueryOperation.Take:
                    output = Cast(flow.GetValue<IEnumerable>(collection)).Take(flow.GetValue<int>(value));
                    break;

                case QueryOperation.Count:
                    outCondition = Cast(flow.GetValue<IEnumerable>(collection)).Count() > 0;
                    break;

                case QueryOperation.Sum:
                    output = Cast(flow.GetValue<IEnumerable>(collection));
                    break;
            }
        }

        IEnumerable<object> Cast(IEnumerable source)
        {
            foreach (var item in source)
                yield return item;
        }
    }
}
