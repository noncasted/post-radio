using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Tools
{
    public class ScriptableObjectCreator : OdinMenuEditorWindow
    {
        static readonly HashSet<Type> _targetsTypes = AssemblyUtilities.GetTypes(AssemblyTypeFlags.CustomTypes)
            .Where(t =>
                t.IsClass &&
                typeof(ScriptableObject).IsAssignableFrom(t) &&
                !typeof(EditorWindow).IsAssignableFrom(t) &&
                !typeof(Editor).IsAssignableFrom(t))
            .ToHashSet();
        
        private ScriptableObject previewObject;
        private string targetFolder;
        private Vector2 scroll;

        private Type SelectedType
        {
            get
            {
                var menuItem = MenuTree.Selection.LastOrDefault();
                return menuItem?.Value as Type;
            }
        }

        [MenuItem("Assets/Create Scriptable Object", priority = -1000)]
        private static void ShowDialog()
        {
            var path = "Assets";
            
            if (Selection.activeObject != null && AssetDatabase.Contains(Selection.activeObject) == true)
            {
                path = AssetDatabase.GetAssetPath(Selection.activeObject);
                Debug.Log(path);
                if (Directory.Exists(path) == false)
                    path = Path.GetDirectoryName(path);
            }

            var window = CreateInstance<ScriptableObjectCreator>();
            window.ShowUtility();
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 500);
            window.titleContent = new GUIContent(path);
            window.targetFolder = path.Trim('/');
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            MenuWidth = 270;
            WindowPadding = Vector4.zero;

            var tree = new OdinMenuTree(false);
            tree.Config.DrawSearchToolbar = true;
            tree.DefaultMenuStyle = OdinMenuStyle.TreeViewStyle;

            var types = _targetsTypes.Where(x => !x.IsAbstract);

            tree.AddRange(types, GetMenuPathForType).AddThumbnailIcons();

            tree.SortMenuItemsByName();
            tree.Selection.SelectionConfirmed += x => CreateAsset();
            
            tree.Selection.SelectionChanged += e =>
            {
                if (previewObject && !AssetDatabase.Contains(previewObject))
                {
                    DestroyImmediate(previewObject);
                }

                if (e != SelectionChangedType.ItemAdded)
                {
                    return;
                }

                var t = SelectedType;
                
                if (t != null && !t.IsAbstract)
                {
                    previewObject = CreateInstance(t);
                }
            };

            return tree;
        }

        private string GetPath()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (Directory.Exists(path) == false)
                path = Path.GetDirectoryName(path);

            return path.Trim('/') + "/";
        }

        private string GetMenuPathForType(Type t)
        {
            var attribute = t.GetCustomAttributes(typeof(CreateAssetMenuAttribute), true)
                .FirstOrDefault() as CreateAssetMenuAttribute;

            if (attribute == null || attribute.menuName.IsNullOrWhitespace())
                return "Other/" + t.Name.Split('`').First().SplitPascalCase();

            return attribute.menuName;
        }

        protected override IEnumerable<object> GetTargets()
        {
            yield return previewObject;
        }

        protected override void DrawEditor(int index)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            base.DrawEditor(index);

            GUILayout.EndScrollView();

            if (previewObject == null)
                return;

            GUILayout.FlexibleSpace();
            SirenixEditorGUI.HorizontalLineSeparator();

            if (GUILayout.Button("Create Asset", GUILayoutOptions.Height(30)))
                CreateAsset();
        }

        private void CreateAsset()
        {
            if (previewObject == null)
                return;

            var destination = GetPath() + GetTypeName() + ".asset";
            destination = AssetDatabase.GenerateUniqueAssetPath(destination);
            AssetDatabase.CreateAsset(previewObject, destination);
            AssetDatabase.Refresh();
            
            Selection.activeObject = previewObject;
            //EditorApplication.delayCall += Close;
        }

        private string GetTypeName()
        {
            foreach (var type in _targetsTypes)
            {
                if (type != SelectedType)
                    continue;
                
                var attribute = type.GetCustomAttributes(typeof(CreateAssetMenuAttribute), true)
                    .FirstOrDefault() as CreateAssetMenuAttribute;

                if (attribute == null || attribute.menuName.IsNullOrWhitespace())
                    break;

                return attribute.fileName;
            }
            
            return MenuTree.Selection.First().Name.ToLower();
        }
    }
}