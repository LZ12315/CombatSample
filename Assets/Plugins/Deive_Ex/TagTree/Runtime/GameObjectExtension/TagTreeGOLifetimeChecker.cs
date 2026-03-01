using DeiveEx.TagTree.GameObjects;
using UnityEngine;

namespace DeiveEx.TagTree
{
    internal class TagTreeGOLifetimeChecker : MonoBehaviour
    {
        internal const float CHECK_INTERVAL = 1f;
        
        private float _lastCheckTime;

        private void Update()
        {
            if(Time.unscaledTime - _lastCheckTime < CHECK_INTERVAL)
                return;
            
            TagTreeGOExtensions.CheckForDestroyedGameObject();
            _lastCheckTime = Time.unscaledTime;
        }
    }
}
