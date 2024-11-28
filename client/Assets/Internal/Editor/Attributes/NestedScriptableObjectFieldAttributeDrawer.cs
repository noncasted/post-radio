using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public class NestedScriptableObjectFieldAttributeDrawer<T> :
        OdinAttributeDrawer<ChildSOFieldAttribute, T>
        where T : ScriptableObject
    {
        private string[] assetPaths = new string[0];
        private const int _maxSearchIterations = 5;

        private Object Parent => GetParent();

        private Object GetParent()
        {
            var parent = Property;
            var iterationsCount = 0;

            while (true)
            {
                if (iterationsCount > _maxSearchIterations)
                    throw new ArgumentException("No suitable parent found");

                try
                {
                    parent = parent.Parent;
                    var _ = (Object)parent.ValueEntry.WeakSmartValue;
                }
                catch (Exception e)
                {
                    iterationsCount++;
                    continue;
                }

                return (Object)parent.ValueEntry.WeakSmartValue;
            }
        }
        
        protected override void Initialize()
        {
            Attribute.Type = typeof(T);
            base.Initialize();
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (assetPaths.Length == 0)
                assetPaths = GetAllScriptsOfType();
            
            if (ValueEntry.SmartValue == null && !Application.isPlaying)
            {
                //Display value dropdown
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                var rect = EditorGUILayout.GetControlRect();
                var labelWidth = rect.width * 0.2f;  
                var dropdownWidth = rect.width * 0.4f;

                var rect1 = new Rect(rect.x, rect.y, labelWidth, rect.height);
                var rect2 = new Rect(rect.x + labelWidth, rect.y, dropdownWidth, rect.height);
                
                EditorGUI.PrefixLabel(rect1, label);
                var valueIndex = SirenixEditorFields.Dropdown(rect2.AlignRight(100), 0, GetDropdownList(assetPaths));
                CallNextDrawer(label);
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck() && valueIndex > 0)
                {
                    var newObject = (T)ScriptableObject.CreateInstance(AssetDatabase
                        .LoadAssetAtPath<MonoScript>(assetPaths[valueIndex - 1]).GetClass());
                    CreateAsset(newObject, ValueEntry.Property.Name);
                    ValueEntry.SmartValue = newObject;
                }
            }
            else
            {
                //Display object field with a delete button
                EditorGUILayout.BeginHorizontal();
                CallNextDrawer(label);
                var rect = EditorGUILayout.GetControlRect(GUILayout.Width(20));
                EditorGUI.BeginChangeCheck();
                SirenixEditorGUI.IconButton(rect, EditorIcons.X);
                EditorGUILayout.EndHorizontal();
                
                if (EditorGUI.EndChangeCheck())
                {
                    //If delete button was pressed:
                    AssetDatabase.Refresh();
                    Object.DestroyImmediate(ValueEntry.SmartValue, true);
                    AssetDatabase.ForceReserializeAssets(new[] { AssetDatabase.GetAssetPath(Parent) });
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
        }

        protected virtual string[] GetAllScriptsOfType()
        {
            var items = AssetDatabase.FindAssets("t:Monoscript", new[] { "Assets/" })
                .Select(x => AssetDatabase.GUIDToAssetPath(x))
                .Where(x => IsCorrectType(AssetDatabase.LoadAssetAtPath<MonoScript>(x)))
                .ToArray();
            return items;
        }

        protected bool IsCorrectType(MonoScript script)
        {
            if (script != null)
            {
                var scriptType = script.GetClass();
                if (scriptType != null &&
                    (scriptType.Equals(Attribute.Type) || scriptType.IsSubclassOf(Attribute.Type)) &&
                    !scriptType.IsAbstract) return true;
            }

            return false;
        }

        protected string[] GetDropdownList(string[] paths)
        {
            var names = paths.Select(s => Path.GetFileName(s)).ToList();
            names.Insert(0, "null");
            return names.ToArray();
        }

        protected void CreateAsset(T newObject, string fieldName)
        {
            fieldName = fieldName.Replace("_", "");
            fieldName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);
            newObject.name = $"{Parent.name}_{newObject.GetType().Name}_{fieldName}";
            AssetDatabase.Refresh();
            AssetDatabase.AddObjectToAsset(newObject, Parent);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        protected virtual void RemoveAsset(ScriptableObject objectToRemove)
        {
            Object.DestroyImmediate(objectToRemove, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}