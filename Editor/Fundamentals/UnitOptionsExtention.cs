using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using Unity.VisualScripting.Community;
using Unity.VisualScripting.Community.Libraries.Humility;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    [FuzzyOptionTreeExtension(typeof(UnitOptionTree))]
    public class UnitOptionsExtension : FuzzyOptionTree
    {
        private HashSet<IUnitOption> options;
        private UnitOptionTree unitOptionTree;

        private FuzzyGroup csharpGroup = new FuzzyGroup("CSharp", typeof(ClassAsset).Icon());

        #region Inherited
        private FuzzyGroup inheritedMembersGroup = new FuzzyGroup("Inherited", typeof(ClassAsset).Icon());
        private FuzzyGroup baseMembersGroup = new FuzzyGroup("Base", typeof(ClassAsset).Icon());
        private FuzzyGroup baseMethodsGroup = new FuzzyGroup("Methods", typeof(Method).Icon());
        private FuzzyGroup basePropertiesGroup = new FuzzyGroup("Properties", typeof(Property).Icon());
        private FuzzyGroup basePropertiesGetGroup = new FuzzyGroup("Get", PathUtil.Load("return", CommunityEditorPath.Events));
        private FuzzyGroup basePropertiesSetGroup = new FuzzyGroup("Set", typeof(ActionNode).Icon());
        private FuzzyGroup inheritedFieldMembersGroup = new FuzzyGroup("Fields", typeof(Field).Icon());
        private FuzzyGroup inheritedMethodMembersGroup = new FuzzyGroup("Methods", typeof(Method).Icon());
        #endregion

        #region Asset
        private FuzzyGroup assetMembersGroup = new FuzzyGroup("Asset", typeof(ClassAsset).Icon());
        private FuzzyGroup assetVariableMembersGroup = new FuzzyGroup("Variables", typeof(Field).Icon());
        private FuzzyGroup assetMethodMembersGroup = new FuzzyGroup("Methods", typeof(Method).Icon());
        private FuzzyGroup assetVariableGetGroup = new FuzzyGroup("Get", PathUtil.Load("return", CommunityEditorPath.Events));
        private FuzzyGroup assetVariableSetGroup = new FuzzyGroup("Set", typeof(ActionNode).Icon());
        #endregion

        public UnitOptionsExtension(UnitOptionTree unitOptionTree) : base(new GUIContent(""))
        {
            this.unitOptionTree = unitOptionTree;
        }

        public override IEnumerable<object> Root()
        {
            var optionsField = typeof(UnitOptionTree).GetField("options", BindingFlags.NonPublic | BindingFlags.Instance);
            var optionsValue = optionsField.GetValue(unitOptionTree);
            options = (HashSet<IUnitOption>)optionsValue;
            if (options.Any(option => option is AssetTypeOption assetTypeOption && assetTypeOption.classAsset != null && assetTypeOption.unit.asset != null))
                yield return csharpGroup;
        }

        // This was the only way I could figure out how to add these options correctly since the units have to have [SpecialUnit]
        public override IEnumerable<object> Children(object parent)
        {
            if (parent == csharpGroup)
            {
                yield return GetFirstOptionOfType<AssetType, AssetTypeOption>(o =>
                {
                    inheritedMembersGroup = new FuzzyGroup("Inherited", UnityAPI.Await(() => o.classAsset.inherits.type.Icon()));
                    baseMembersGroup = new FuzzyGroup("Base", UnityAPI.Await(() => o.classAsset.inherits.type.Icon()));
                    return o.unit.asset != null;
                });
                yield return inheritedMembersGroup;

                if (HasOptionsOfType<AssetFieldUnit, AssetFieldUnitOption>(o => o.unit.field != null) ||
                    HasOptionsOfType<AssetMethodCallUnit, AssetMethodCallUnitOption>(o => o.unit.method != null))
                {
                    yield return assetMembersGroup;
                }
            }
            else if (parent == inheritedMembersGroup)
            {
                if (HasOptionsOfType<BaseMethodCall, BaseMethodUnitOption>(o => o.unit.member != null) ||
                    HasOptionsOfType<BasePropertyGetterUnit, BasePropertyGetterUnitOption>(o => o.unit.member != null) ||
                    HasOptionsOfType<BasePropertySetterUnit, BasePropertySetterUnitOption>(o => o.unit.member != null))
                {
                    yield return baseMembersGroup;
                }

                if (HasOptionsOfType<InheritedFieldUnit, InheritedFieldUnitOption>(o => o.unit.member != null))
                    yield return inheritedFieldMembersGroup;

                if (HasOptionsOfType<InheritedMethodCall, InheritedMethodUnitOption>(o => o.unit.member != null))
                    yield return inheritedMethodMembersGroup;
            }
            else if (parent == assetMembersGroup)
            {
                if (HasOptionsOfType<AssetFieldUnit, AssetFieldUnitOption>(o => o.unit.field != null))
                    yield return assetVariableMembersGroup;

                if (HasOptionsOfType<AssetMethodCallUnit, AssetMethodCallUnitOption>(o => o.unit.method != null))
                    yield return assetMethodMembersGroup;
            }
            else if (parent == assetVariableMembersGroup)
            {
                if (HasOptionsOfType<AssetFieldUnit, AssetFieldUnitOption>(o => o.unit.field != null && o.unit.actionDirection == ActionDirection.Get))
                    yield return assetVariableGetGroup;

                if (HasOptionsOfType<AssetFieldUnit, AssetFieldUnitOption>(o => o.unit.field != null && o.unit.actionDirection == ActionDirection.Set))
                    yield return assetVariableSetGroup;
            }
            else if (parent == assetVariableGetGroup)
            {
                foreach (var option in GetOptionsOfType<AssetFieldUnit, AssetFieldUnitOption>(o => o.unit.field != null && o.unit.actionDirection == ActionDirection.Get))
                    yield return option;
            }
            else if (parent == assetVariableSetGroup)
            {
                foreach (var option in GetOptionsOfType<AssetFieldUnit, AssetFieldUnitOption>(o => o.unit.field != null && o.unit.actionDirection == ActionDirection.Set))
                    yield return option;
            }
            else if (parent == assetMethodMembersGroup)
            {
                foreach (var option in GetOptionsOfType<AssetMethodCallUnit, AssetMethodCallUnitOption>(o => o.unit.method != null))
                    yield return option;
            }
            else if (parent == baseMembersGroup)
            {
                if (HasOptionsOfType<BaseMethodCall, BaseMethodUnitOption>(o => o.unit.member != null))
                    yield return baseMethodsGroup;

                if (HasOptionsOfType<BasePropertyGetterUnit, BasePropertyGetterUnitOption>(o => o.unit.member != null) ||
                    HasOptionsOfType<BasePropertySetterUnit, BasePropertySetterUnitOption>(o => o.unit.member != null))
                    yield return basePropertiesGroup;
            }
            else if (parent == baseMethodsGroup)
            {
                foreach (var option in GetOptionsOfType<BaseMethodCall, BaseMethodUnitOption>(o => o.unit.member != null))
                    yield return option;
            }
            else if (parent == basePropertiesGroup)
            {
                if (HasOptionsOfType<BasePropertyGetterUnit, BasePropertyGetterUnitOption>(o => o.unit.member != null))
                    yield return basePropertiesGetGroup;

                if (HasOptionsOfType<BasePropertySetterUnit, BasePropertySetterUnitOption>(o => o.unit.member != null))
                    yield return basePropertiesSetGroup;
            }
            else if (parent == basePropertiesGetGroup)
            {
                foreach (var option in GetOptionsOfType<BasePropertyGetterUnit, BasePropertyGetterUnitOption>(o => o.unit.member != null))
                    yield return option;
            }
            else if (parent == basePropertiesSetGroup)
            {
                foreach (var option in GetOptionsOfType<BasePropertySetterUnit, BasePropertySetterUnitOption>(o => o.unit.member != null))
                    yield return option;
            }
            else if (parent == inheritedFieldMembersGroup)
            {
                foreach (var option in GetOptionsOfType<InheritedFieldUnit, InheritedFieldUnitOption>(o => o.unit.member != null))
                    yield return option;
            }
            else if (parent == inheritedMethodMembersGroup)
            {
                foreach (var option in GetOptionsOfType<InheritedMethodCall, InheritedMethodUnitOption>(o => o.unit.member != null))
                    yield return option;
            }
        }

        private bool HasOptionsOfType<TUnit, TOption>(Func<TOption, bool> predicate)
            where TOption : class, IUnitOption
        {
            return options.OfType<TOption>().Any(option => option.UnitIs<TUnit>() && predicate(option));
        }

        private IEnumerable<IUnitOption> GetOptionsOfType<TUnit, TOption>(Func<TOption, bool> predicate)
            where TOption : class, IUnitOption
        {
            return options.OfType<TOption>().Where(option => option.UnitIs<TUnit>() && predicate(option));
        }

        private IUnitOption GetFirstOptionOfType<TUnit, TOption>(Func<TOption, bool> predicate)
            where TOption : class, IUnitOption
        {
            return options.OfType<TOption>().FirstOrDefault(option => option.UnitIs<TUnit>() && predicate(option));
        }
    }
}