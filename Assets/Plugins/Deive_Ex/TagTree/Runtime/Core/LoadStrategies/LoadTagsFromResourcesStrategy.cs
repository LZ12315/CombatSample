using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeiveEx.TagTree
{
    public class LoadTagsFromResourcesStrategy : LoadTagsStrategy
    {
        private Dictionary<string, TextAsset> _textAssets = new();

        internal LoadTagsFromResourcesStrategy()
        {
            var files = Resources.LoadAll<TextAsset>("Tags");
            
            if (files.Length == 0)
            {
                TagTreeUtils.LogMessage("No Tag files detected!", LogType.Error);
                return;
            }
            
            foreach (var f in files)
            {
                if(!_textAssets.TryAdd(f.name, f))
                    TagTreeUtils.LogMessage($"A Tag file with name '{f.name}' already exists. Make sure Tag files have unique names", LogType.Error);
            }
        }

        internal override bool ContainFile(string fileName)
        {
            return _textAssets.ContainsKey(fileName);
        }

        internal override IEnumerable<string> GetFileNames()
        {
            return _textAssets.Keys;
        }

        internal override IEnumerable<string> GetTagsFromFile(string fileName)
        {
            if (!_textAssets.TryGetValue(fileName, out var asset))
            {
                TagTreeUtils.LogMessage($"No Tag file named '{fileName}' loaded", LogType.Error);
                return null;
            }
            
            var stringTags = GetValidLinesFromText(asset.text);
            LogTagsInFile($"(Resources) {fileName}", stringTags);
            return stringTags;
        }

#if UNITY_EDITOR
        internal override void CreateTagFile(string fileName, string contents)
        {
            var separator = Path.DirectorySeparatorChar;
            var filePath = Path.Combine(Application.dataPath, $"Resources{separator}Tags", $"{fileName}.tags");
            WriteTagsToFile(filePath, contents);
        }

        internal override void SaveTags(string fileName, IEnumerable<string> tags)
        {
            var textAsset = _textAssets[fileName];
            var path = Path.Combine(Application.dataPath.Replace("/Assets", ""), AssetDatabase.GetAssetPath(textAsset));
            var contents = string.Join(Environment.NewLine, tags);
            
            WriteTagsToFile(path, contents);
        }
#endif
    }
}
