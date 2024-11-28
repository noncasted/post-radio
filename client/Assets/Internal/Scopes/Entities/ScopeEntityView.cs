using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace Internal
{
    [DisallowMultipleComponent]
    public class ScopeEntityView : MonoBehaviour, IScopeEntityView
    {
        [SerializeField] private LifetimeScope _scope;

        [SerializeField] private Component[] _register;
        [SerializeField] private List<MonoBehaviour> _autoDetected;

        public LifetimeScope Scope => _scope;

        public void CreateViews(IEntityBuilder builder)
        {
            foreach (var component in _register)
                builder.RegisterComponent(component, VContainer.Lifetime.Scoped);

            foreach (var behaviour in _autoDetected)
            {
                if (behaviour is not IEntityComponent component)
                    throw new Exception();

                component.Register(builder);
            }
        }

        [Button("Scan")]
        private void OnValidate()
        {
            if (_scope == null)
                _scope = GetComponent<LifetimeScope>();

            _autoDetected.Clear();
            var components = GetComponentsInChildren<IEntityComponent>(true);

            foreach (var component in components)
            {
                if (component is not MonoBehaviour behaviour)
                    throw new Exception();

                _autoDetected.Add(behaviour);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}