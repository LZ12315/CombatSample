using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DeiveEx.TagTree
{
    public abstract class LoadTagsStrategy
    {
        internal abstract bool ContainFile(string fileName);
        internal abstract IEnumerable<string> GetFileNames();
        internal abstract IEnumerable<string> GetTagsFromFile(string fileName);
        
#if UNITY_EDITOR
        internal abstract void CreateTagFile(string fileName, string contents);
        internal abstract void SaveTags(string fileName, IEnumerable<string> tags);
#endif
        
        protected IEnumerable<string> GetValidLinesFromText(string text)
        {
            //First, replace new lines in case our file came from a different OS
            text = Regex.Replace(text, @"\r\n?|\n", Environment.NewLine);
            
            //Then split
            return text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('[') && !x.StartsWith('#'));
        }
        
        protected void LogTagsInFile(string file, IEnumerable<string> stringTags)
        {
            if (TagManager.ShowLogs)
            {
                var tagArray = stringTags.ToArray();
                
                TagTreeUtils.LogMessage(@$"Loading tags from: {file}
Detected Tags: {tagArray.Length}
- {string.Join("\n- ", tagArray)}");
            }
        }
        
        protected void WriteTagsToFile(string filePath, string contents)
        {
            var directoryPath = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directoryPath))
                throw new NullReferenceException("Path cannot be null");
            
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(filePath, contents);
            
            TagTreeUtils.LogMessage($"Tags written to file '{Path.GetFileNameWithoutExtension(filePath)}' at '{filePath}'");
        }
    }
}
