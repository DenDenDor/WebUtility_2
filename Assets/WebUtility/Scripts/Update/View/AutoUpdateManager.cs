using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WebUtility
{
    public class AutoUpdateManager : MonoBehaviour
    {
        public event Action Updated;
        public event Action FixedUpdated;
        public event Action LateUpdated;

        private void Update()
        {
           Updated?.Invoke();
        }

        private void FixedUpdate()
        {
            FixedUpdated?.Invoke();
        }

        private void LateUpdate()
        {
            LateUpdated?.Invoke();
        }
    }
    
    public interface IUpdatable
    {
        void Update();
    }

    public interface IFixedUpdatable
    {
        void FixedUpdate();
    }

    public interface ILateUpdatable
    {
        void LateUpdate();
    }
}