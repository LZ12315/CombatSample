using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;

namespace DeiveEx.TagTree.Editor
{
    internal class TagFilesAssetPostProcessor : AssetPostprocessor
    {
        public static event Action TagFilesChanged;
        
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            //If any tag file was imported, reload the tags
            if (importedAssets.Any(x => Path.GetExtension(x) == TagManager.TAGS_ASSET_EXTENSION) ||
                deletedAssets.Any(x => Path.GetExtension(x) == TagManager.TAGS_ASSET_EXTENSION))
            {
                //Apparently, the files aren't actually loaded when this callback is triggered... The solution? Wait for the editor to update
                EditorApplication.update += OnEditorUpdate;
            }
        }

        private static void OnEditorUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
            TagFilesChanged?.Invoke();
        }
    }
}
