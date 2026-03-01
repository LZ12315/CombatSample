using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DeiveEx.TagTree.EditorTests
{
    [Category("TagTree")]
    public class TagTests
    {
        private TagContainer _container;
        
        [SetUp]
        public void Setup()
        {
            _container = new();
        }
        
        [TestCase("a")]
        [TestCase("a", "b")]
        [TestCase("a", "b", "c")]
        public void Is_Tag_Created_Correctly(params string[] tagHierarchy)
        {
            string fullTagName = string.Join(".", tagHierarchy);
            var tag = TagTreeUtils.CreateSingleTagFromName(fullTagName);

            var tagId = TagTreeUtils.GenerateIdFromFullName(fullTagName);
            Assert.AreEqual(tagId, tag.Id);
        }
        
        [TestCase("a", "a", ExpectedResult = true)]
        [TestCase("a.b", "a", ExpectedResult = true)]
        [TestCase("a.b.c", "a", ExpectedResult = true)]
        [TestCase("a.b.c", "a.b", ExpectedResult = true)]
        [TestCase("a.b.c", "a.b.c", ExpectedResult = true)]
        [TestCase( "a", "b", ExpectedResult = false)]
        [TestCase( "a.b.c", "a.b.x", ExpectedResult = false)]
        [TestCase( "a", "a.b.c", ExpectedResult = false)]
        public bool Tag_Matches(string a, string b)
        {
            TagManager.LoadTagsFromNames(a, b);
            
            var tagA = Tag.GetTagFromFullName(a);
            var tagB = Tag.GetTagFromFullName(b);

            return tagA.Matches(tagB);
        }
        
        [TestCase( "a", "a", ExpectedResult = true)]
        [TestCase( "a.b", "a.b", ExpectedResult = true)]
        [TestCase( "x.y.z", "x.y.z", ExpectedResult = true)]
        [TestCase( "a.b", "a", ExpectedResult = false)]
        [TestCase( "a.b.c", "a", ExpectedResult = false)]
        [TestCase( "a.b.c", "a.b", ExpectedResult = false)]
        [TestCase( "a", "b", ExpectedResult = false)]
        [TestCase( "a.b.c", "a.b.x", ExpectedResult = false)]
        [TestCase( "a", "a.b.c", ExpectedResult = false)]
        public bool Tag_Matches_Exact(string a, string b)
        {
            TagManager.LoadTagsFromNames(a, b);
            
            var tagA = Tag.GetTagFromFullName(a);
            var tagB = Tag.GetTagFromFullName(b);
            
            return tagA.MatchesExact(tagB);
        }
        
        [TestCase("a", new [] { "a" }, ExpectedResult = true)]
        [TestCase("a", new [] { "a", "x" }, ExpectedResult = true)]
        [TestCase("a", new [] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a" }, ExpectedResult = true)]
        [TestCase("a", new [] { "a.b" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase("a", new [] { "x" }, ExpectedResult = false)]
        [TestCase("a.b", new [] { "x" }, ExpectedResult = false)]
        [TestCase("a", new string [] {}, ExpectedResult = false)]
        public bool Tag_Matches_Any(string sourceTag, string[] tagsToCompare)
        {
            TagManager.LoadTagsFromNames(tagsToCompare.Concat(new [] { sourceTag }).ToArray());
            
            var tagId = TagTreeUtils.GenerateIdFromFullName(sourceTag);
            var gameplayTag = Tag.GetTagFromId(tagId);

            foreach (var tagName in tagsToCompare)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            Debug.Log($"SourceTag: {sourceTag}");
            TagContainerTests.LogTagsInContainer(_container);
            
            return gameplayTag.MatchesAny(_container);
        }
        
        [TestCase("a", new [] { "a" }, ExpectedResult = true)]
        [TestCase("a", new [] { "a", "x" }, ExpectedResult = true)]
        [TestCase("a", new [] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a" }, ExpectedResult = false)]
        [TestCase("a", new [] { "a.b" }, ExpectedResult = true)] //Remember that adding "a.b" is the same as adding "a" and "a.b"
        [TestCase("a.b", new [] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase("a", new [] { "x" }, ExpectedResult = false)]
        [TestCase("a.b", new [] { "x" }, ExpectedResult = false)]
        [TestCase("a.b", new [] { "a", "x" }, ExpectedResult = false)]
        [TestCase("a", new string [] {}, ExpectedResult = false)]
        public bool Tag_Matches_Any_Exact(string sourceTag, string[] containerTags)
        {
            TagManager.LoadTagsFromNames(containerTags.Concat(new [] { sourceTag }).ToArray());
            
            var tagId = TagTreeUtils.GenerateIdFromFullName(sourceTag);
            var gameplayTag = Tag.GetTagFromId(tagId);

            foreach (var tagName in containerTags)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            Debug.Log($"SourceTag: {sourceTag}");
            TagContainerTests.LogTagsInContainer(_container);
            
            return gameplayTag.MatchesAnyExact(_container);
        }
        
        [TestCase("a", new [] { "a" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a.b" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase("a", new string [] {}, ExpectedResult = true)]
        [TestCase("a", new [] { "a", "x" }, ExpectedResult = false)]
        [TestCase("a", new [] { "a", "a.b" }, ExpectedResult = false)]
        [TestCase("a", new [] { "a.b" }, ExpectedResult = false)]
        [TestCase("a", new [] { "x" }, ExpectedResult = false)]
        [TestCase("a.b", new [] { "x" }, ExpectedResult = false)]
        [TestCase("a.b", new [] { "a.b", "x" }, ExpectedResult = false)]
        public bool Tag_Matches_All(string sourceTag, string[] tagsToCompare)
        {
            TagManager.LoadTagsFromNames(tagsToCompare.Concat(new [] { sourceTag }).ToArray());
            
            var tagId = TagTreeUtils.GenerateIdFromFullName(sourceTag);
            var gameplayTag = Tag.GetTagFromId(tagId);
            
            foreach (var tagName in tagsToCompare)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            Debug.Log($"SourceTag: {sourceTag}");
            TagContainerTests.LogTagsInContainer(_container);
            
            return gameplayTag.MatchesAll(_container);
        }
        
        [TestCase("a", new [] { "a" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a" }, ExpectedResult = false)]
        [TestCase("a.b", new [] { "a.b" }, ExpectedResult = true)]
        [TestCase("a.b", new [] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase("a", new string [] {}, ExpectedResult = true)]
        [TestCase("a", new [] { "a", "x" }, ExpectedResult = false)]
        [TestCase("a", new [] { "a", "a.b" }, ExpectedResult = false)]
        [TestCase("a", new [] { "a.b" }, ExpectedResult = false)]
        [TestCase("a", new [] { "x" }, ExpectedResult = false)]
        [TestCase("a.b", new [] { "x" }, ExpectedResult = false)]
        public bool Tag_Matches_All_Exact(string sourceTag, string[] tagsToCompare)
        {
            TagManager.LoadTagsFromNames(tagsToCompare.Concat(new [] { sourceTag }).ToArray());
            
            var tagId = TagTreeUtils.GenerateIdFromFullName(sourceTag);
            var gameplayTag = Tag.GetTagFromId(tagId);
            
            foreach (var tagName in tagsToCompare)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            Debug.Log($"SourceTag: {sourceTag}");
            Debug.Log("=== Tags to Compare");
            Debug.Log(string.Join("\n", tagsToCompare));
            TagContainerTests.LogTagsInContainer(_container);
            
            return gameplayTag.MatchesAllExact(_container);
        }

        [Test]
        public void Tag_Matches_Null_Container()
        {
            string tagName = "a";
            
            TagManager.LoadTagsFromNames(tagName);
            var tag = Tag.GetTagFromFullName(tagName);

            Assert.Throws<NullReferenceException>(() => tag.MatchesAny(null));
            Assert.Throws<NullReferenceException>(() => tag.MatchesAll(null));
        }
    }
}
