using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeiveEx.TagTree.GameObjects
{
    public static class TagTreeGOExtensions
    {
        #region Fields

        internal static Dictionary<GameObject, TagContainer> GoTagContainers;
        
        private static TagTreeGOLifetimeChecker _treeGOLifetimeChecker;

        #endregion

        #region Unity Events

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GoTagContainers = new();
            
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateHelperObject()
        {
            //Creates a helper GameObject to keep track of when other GameObjects were destroyed
            var go =  new GameObject("TagTree_GO_LifetimeChecker");
            Object.DontDestroyOnLoad(go);
            // go.hideFlags = HideFlags.HideAndDontSave; //TODO Should we hide the object? Probably not... If there's an error, it would be extremely hard for users to find the source
            _treeGOLifetimeChecker = go.AddComponent<TagTreeGOLifetimeChecker>();
        }

        #endregion

        #region Internal Methods

        internal static void CheckForDestroyedGameObject()
        {
            //Check if any of the GameObjects we stored was destroyed, and remove it from the dictionary if it was
            bool hasNullKeys = false;

            foreach (var key in GoTagContainers.Keys)
            {
                //Usually, Dictionaries cannot have null keys, but in this case we can check for null keys because Unity
                //overrides the "==" operator, so the key is not really null
                if (key == null)
                {
                    hasNullKeys = true;
                    break;
                }
            }
            
            if(!hasNullKeys)
                return;

            var keys = GoTagContainers.Keys.ToArray();
            
            foreach (var key in keys)
            {
                //This works for the same reason as above
                if (key == null)
                    GoTagContainers.Remove(key);
            }
        }

        #endregion

        #region Private Methods

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if(state != PlayModeStateChange.ExitingPlayMode)
                return;

            //The GO container should only exist during Play mode
            GoTagContainers = null;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
#endif

        #endregion

        #region Extension Methods

        public static TagContainer GetTagContainer(this GameObject gameObject)
        {
            //TODO while this is easier to do, maybe I should just create wrapper methods for all tagContainer methods
            //so people don't need to store the container? This would also avoid unused containers from staying alive in
            //memory because some other class has stored a reference to it.
            //An extension property, like "myGameObject.Tags.HasTag()" would would mask this nicely, but they're not
            //available in C# 8.0...

            if (GoTagContainers == null)
                return null;
            
            if (GoTagContainers.TryGetValue(gameObject, out var tagContainer))
                return tagContainer;
            
            tagContainer = new();
            GoTagContainers.Add(gameObject, tagContainer);

            return tagContainer;
        }

        #endregion
    }
}
 