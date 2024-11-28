using System.Collections.Generic;
using System.Linq;
using Internal;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class NavigationStorage : MonoBehaviour, INavigationStorage, ISceneService
    {
        [SerializeField] private NavigationTarget _first;
        [SerializeField] private int _selectAmount;

        public INavigationTarget First => _first;

        [Button]
        private void Scan()
        {
            Recalculate();
        }

        public void Recalculate()
        {
            var targets = FindObjectsByType<NavigationTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var current in targets)
            {
                var entries = new List<NavigationTarget>(targets);
                entries.Remove(current);

                entries = entries.OrderBy(t => Vector2.Distance(t.Position, current.Position)).ToList();
                var xPossible = entries.OrderBy(t => Mathf.Abs(t.Position.x - current.Position.x)).ToList();
                var yPossible = entries.OrderBy(t => Mathf.Abs(t.Position.y - current.Position.y)).ToList();

                var xTargets = new List<NavigationTarget>();
                var yTargets = new List<NavigationTarget>();

                for (var i = 0; i < _selectAmount; i++)
                {
                    if (i < xPossible.Count)
                        xTargets.Add(xPossible[i]);

                    if (i < yPossible.Count)
                        yTargets.Add(yPossible[i]);
                }

                var dictionary = new TargetsDictionary();

                foreach (var target in yTargets)
                {
                    if (dictionary.ContainsKey(Side.Right) == false)
                    {
                        if (current.Position.x < target.Position.x)
                            dictionary.Add(Side.Right, target);
                    }

                    if (dictionary.ContainsKey(Side.Left) == false)
                    {
                        if (current.Position.x > target.Position.y)
                            dictionary.Add(Side.Left, target);
                    }
                }

                foreach (var target in xTargets)
                {
                    if (dictionary.ContainsKey(Side.Up) == false)
                    {
                        if (current.Position.y < target.Position.y)
                            dictionary.Add(Side.Up, target);
                    }

                    if (dictionary.ContainsKey(Side.Down) == false)
                    {
                        if (current.Position.y > target.Position.y)
                            dictionary.Add(Side.Down, target);
                    }
                }

                current.Setup(dictionary);
#if UNITY_EDITOR
                EditorUtility.SetDirty(current);
#endif
            }
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<INavigationStorage>();
        }
    }
}