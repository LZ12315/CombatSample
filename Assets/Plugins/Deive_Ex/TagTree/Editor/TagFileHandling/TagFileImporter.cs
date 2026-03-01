using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DeiveEx.TagTree.Editor
{
    //Here we define which file extension this importer takes care of
    [ScriptedImporter(version: 1, ext: "tags")]
    public class TagFileImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            //Convert file to a TextAsset so we can use it as a TextAsset, like loading from using Resources
            TextAsset asset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("Text", asset);
            ctx.SetMainObject(asset);
        }
    }
}
