using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeiveEx.TagTree
{
    public class LoadTagsFromStreamingAssetsStrategy : LoadTagsStrategy
    {
        private Dictionary<string, string> _filePaths = new();
    
        public LoadTagsFromStreamingAssetsStrategy()
        {
            var filePaths = GetTagFilePathsRecursive(Application.streamingAssetsPath);

            if (filePaths == null || filePaths.Count == 0)
            {
                TagTreeUtils.LogMessage("No Tag files detected!", LogType.Error);
                return;
            }

            foreach (var path in filePaths)
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                
                if(!_filePaths.TryAdd(fileName, path))
                    TagTreeUtils.LogMessage($"A Tag file with name '{fileName}' already exists. Make sure Tag files have unique names", LogType.Error);

            }
        }

        internal override bool ContainFile(string fileName)
        {
            return _filePaths.ContainsKey(fileName);
        }

        internal override IEnumerable<string> GetFileNames()
        {
            return _filePaths.Keys;
        }

        internal override IEnumerable<string> GetTagsFromFile(string fileName)
        {
            if (!_filePaths.TryGetValue(fileName, out var path))
            {
                TagTreeUtils.LogMessage($"No Tag file named '{fileName}' loaded", LogType.Error);
                return null;
            }
            
            var stringTags = GetValidLinesFromText(File.ReadAllText(path));
            LogTagsInFile($"(StreamingAssets) {fileName}", stringTags);
            return stringTags;
        }

#if UNITY_EDITOR
        internal override void CreateTagFile(string fileName, string contents)
        {
            var filePath = Path.Combine(Application.streamingAssetsPath, "Tags", $"{fileName}.tags");
            WriteTagsToFile(filePath, contents);
        }

        internal override void SaveTags(string fileName, IEnumerable<string> tags)
        {
            var filePath = Path.Combine(Application.streamingAssetsPath, "Tags", $"{fileName}.tags");
            var contents = string.Join(Environment.NewLine, tags);
            WriteTagsToFile(filePath, contents);
        }
#endif
    
        private List<string> GetTagFilePathsRecursive(string startPath)
        {
            List<string> paths = new();
    
            if (Directory.Exists(startPath))
            {
                var files = Directory.GetFiles(startPath);
    
                foreach (var filePath in files)
                {
                    if(Path.GetExtension(filePath) == TagManager.TAGS_ASSET_EXTENSION)
                        paths.Add(filePath);
                }
				    
                var directories = Directory.GetDirectories(startPath);
                    
                foreach (var directoryPath in directories)
                {
                    paths.AddRange(GetTagFilePathsRecursive(directoryPath));
                }
            }
			    
            return paths;
        }
    }
}
