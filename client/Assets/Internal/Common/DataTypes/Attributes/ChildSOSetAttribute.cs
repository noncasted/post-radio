#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using Sirenix.OdinInspector.Editor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;
 
namespace Internal
{
    [IncludeMyAttributes]
#if UNITY_EDITOR
    [ListDrawerSettings(
        CustomRemoveElementFunction =
            "@$property.GetAttribute<NestedScriptableObjectSetAttribute>().RemoveObject($removeElement, $property)",
        Expanded = true)]
    [ValueDropdown("@$property.GetAttribute<NestedScriptableObjectSetAttribute>().GetAllObjectsOfType()",
        FlattenTreeView = true)]
    [OnCollectionChanged("@$property.GetAttribute<NestedScriptableObjectSetAttribute>().OnCollectionChange($info)")]
#endif
    public class ChildSOSetAttribute : Attribute
    {
        private readonly List<ScriptableObject> _create = new();
        private readonly List<Object> _remove = new();

        public Type Type;
        public Object Object;

        public IReadOnlyList<ScriptableObject> ObjectsToCreate => _create;
        public IReadOnlyList<Object> ObjectsToRemove => _remove;

        public void OnCreated(ScriptableObject created)
        {
            _create.Remove(created);
        }

        public void OnRemoved(Object removed)
        {
            _remove.Remove(removed);
        }

#if UNITY_EDITOR
        protected void RemoveObject(Object objectToRemove, InspectorProperty property)
        {
            _remove.Add(objectToRemove);
        }

        protected IEnumerable GetAllObjectsOfType()
        {
            var items =
                AssetDatabase.FindAssets("t:Monoscript", new[] { "Assets/Features" })
                    .Select(x => AssetDatabase.GUIDToAssetPath(x))
                    .Where(x => IsCorrectType(AssetDatabase.LoadAssetAtPath<MonoScript>(x)))
                    .Select(x => new ValueDropdownItem(Path.GetFileName(x),
                        ScriptableObject.CreateInstance(AssetDatabase.LoadAssetAtPath<MonoScript>(x).GetClass())));

            var allObjectsOfType = items.ToList();

            var path = AssetDatabase.GetAssetPath(Object);
            var allNestedObjects = AssetDatabase.LoadAllAssetsAtPath(path);

            var nestedObjectsTypes = new List<Type>();

            foreach (var nestedObject in allNestedObjects)
                nestedObjectsTypes.Add(nestedObject.GetType());

            var objectsToRemove = new List<ValueDropdownItem>();

            foreach (var type in allObjectsOfType)
            {
                var dropdownType = type.Value.GetType();

                if (nestedObjectsTypes.Contains(dropdownType) == false)
                    continue;

                objectsToRemove.Add(type);
            }

            foreach (var objectToRemove in objectsToRemove)
                allObjectsOfType.Remove(objectToRemove);

            return allObjectsOfType;
        }

        protected bool IsCorrectType(MonoScript script)
        {
            if (script != null)
            {
                var scriptType = script.GetClass();
                if (scriptType != null && (scriptType.Equals(Type) || scriptType.IsSubclassOf(Type)) &&
                    !scriptType.IsAbstract) return true;
            }

            return false;
        }

        protected void OnCollectionChange(CollectionChangeInfo info)
        {
            if (info.ChangeType != CollectionChangeType.Add)
                return;

            if (info.Value is not ScriptableObject scriptableObject)
                return;


            if (scriptableObject.name.Contains("EmptyEntry"))
            {
                _create.Add(null);
                return;
            }

            _create.Add(scriptableObject);
        }
#endif
    }
}