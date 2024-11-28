using System;
using UnityEngine;
using VContainer.Unity;

namespace Internal
{
    [Serializable]
    public class ServiceScopeData
    {
        [SerializeField] private LifetimeScope _scopePrefab;
        [SerializeField] private SceneData _servicesScene;
        [SerializeField] private bool _isMock;

        public ServiceScopeData(LifetimeScope scopePrefab, SceneData servicesScene, bool isMock)
        {
            _scopePrefab = scopePrefab;
            _servicesScene = servicesScene;
            _isMock = isMock;
        }

        public LifetimeScope ScopePrefab => _scopePrefab;

        public SceneData ServicesScene => _servicesScene;

        public bool IsMock => _isMock;
    }
}