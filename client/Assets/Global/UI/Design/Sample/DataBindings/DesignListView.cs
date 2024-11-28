using System;
using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignListView : MonoBehaviour
    {
       // [SerializeField] private ListView _list;

        public void Construct<TValue, TEntry>(
            IReadOnlyLifetime lifetime,
            Action<TEntry> constructListener,
            Action<TEntry> disposeListener)
        {
            // _list.AddDataBinder<TValue, TEntry>(BindView);
            // collection.View(lifetime, OnElementAdd);
            //
            // return;
            //
            // void BindView(Data.OnBind<TValue> data, TEntry target, int index)
            // {
            // }
        }

        private void OnElementAdd<T>(IReadOnlyLifetime lifetime, T value)
        {
            lifetime.Listen(OnElementRemoved);

            return;

            void OnElementRemoved()
            {
            }
        }


        private void OnValidate()
        {
            // if (_list == null)
            //     _list = GetComponent<ListView>();
        }
    }
}