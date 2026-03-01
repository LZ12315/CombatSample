using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeiveEx.TagTree.EditorTests
{
    [Category("TagTree")]
    public class TagManagerTests
    {
        private Regex _invalidTagErrorMessageRegex;
        
        [SetUp]
        public void Setup()
        {
            _invalidTagErrorMessageRegex = new Regex("Tag .* is invalid.*");
        }
        
        [TestCase(true, "a")]
        [TestCase(true, "a.b")]
        [TestCase(true, "a.b.c")]
        [TestCase(true, "1.2.3")]
        [TestCase(true, "a-a")]
        [TestCase(true, "a-a.b_b")]
        [TestCase(false, "")]
        [TestCase(false, ".")]
        [TestCase(false, " ")]
        [TestCase(false, " ")]
        [TestCase(false, "a. .b")]
        [TestCase(false, " . ")]
        [TestCase(false, ".a.")]
        [TestCase(false, "a#.b@")]
        [TestCase(false, "a..b")]
        [TestCase(false, "a.b.")]
        [TestCase(false, ".a.b")]
        [TestCase(false, null)]
        public void Is_Formatting_Validation_Working_As_Expected(bool isValid, string tagToValidate)
        {
            Assert.IsTrue(isValid == TagTreeUtils.IsTagFormattingValid(tagToValidate));
        }
        
        [TestCase(1, new [] {"a"})]
        [TestCase(2, new [] {"a.b"})]
        [TestCase(3, new [] {"a.b.c"})]
        [TestCase(2, new [] {"a", "b"})]
        [TestCase(3, new [] {"a", "b.c"})]
        [TestCase(1, new [] {"a", "a"})]
        [TestCase(2, new [] {"a", "a.b"})]
        [TestCase(0, new [] {""})]
        [TestCase(0, new [] {"."})]
        [TestCase(0, new [] {".."})]
        [TestCase(0, new [] {".a."})]
        [TestCase(0, new [] {" "})]
        [TestCase(0, new [] {" . "})]
        [TestCase(0, null)]
        [TestCase(0, new string []{})]
        [TestCase(0, new string [] { null })]
        public void Are_Loaded_Tags_The_Correct_Amount(int expectedAmount, string[] tagsToLoad)
        {
            if (tagsToLoad != null && tagsToLoad.Any(x => !TagTreeUtils.IsTagFormattingValid(x)))
                LogAssert.Expect(LogType.Error, _invalidTagErrorMessageRegex);
            
            TagManager.LoadTagsFromNames(tagsToLoad);
            Assert.AreEqual(expectedAmount, TagManager.Tags.Count);
        }
        
        [TestCase("a", "a")]
        [TestCase("a.b", new [] {"a", "a.b"})]
        [TestCase("a.b.c", new [] {"a", "a.b", "a.b.c"})]
        [TestCase("")]
        [TestCase(null)]
        [TestCase(" ")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase(".a.")]
        [TestCase(" . ")]
        public void Are_Loaded_Tags_Created_Correctly(string tagToLoad, params string[] expectedTags)
        {
            if(!TagTreeUtils.IsTagFormattingValid(tagToLoad))
                LogAssert.Expect(LogType.Error, _invalidTagErrorMessageRegex);
            
            TagManager.LoadTagsFromNames(tagToLoad);

            if (expectedTags == null || expectedTags.Length == 0)
            {
                Assert.IsTrue(TagManager.Tags.Count == 0);
                return;
            }

            foreach (var expectedFullTagName in expectedTags)
            {
                var expectedId = TagTreeUtils.GenerateIdFromFullName(expectedFullTagName);
                var expectedTagName = expectedFullTagName.Split('.')[^1];
                
                Assert.IsTrue(TagManager.Tags.ContainsKey(expectedId));
                Assert.AreEqual(expectedId, TagManager.Tags[expectedId].Id);
                Assert.AreEqual(expectedTagName, TagManager.Tags[expectedId].TagName);
                Assert.AreEqual(expectedFullTagName, TagManager.Tags[expectedId].FullTagName);
            }
        }

        [TestCase("a", "a", null)]
        [TestCase("a.b", "a", null)]
        [TestCase("a.b", "a.b", "a")]
        [TestCase("a.b.c", "a.b.c", "a.b")]
        public void Is_Parent_Tag_Correctly_Set(string tagToLoad, string tagToCheck, string expectedParentTag)
        {
            TagManager.LoadTagsFromNames(tagToLoad);

            var tag = Tag.GetTagFromFullName(tagToCheck);
            var expectedTag = Tag.GetTagFromFullName(expectedParentTag);
            Assert.AreEqual(tag.ParentTag, expectedTag);
        }

        [TestCase(new string[] { "a" }, "a", new string[] {} )]
        [TestCase(new string[] { "a.b" }, "a", new string[] { "a.b" } )]
        [TestCase(new string[] { "a.b.c" }, "a", new string[] { "a.b" } )]
        [TestCase(new string[] { "a.b.c" }, "a.b", new string[] { "a.b.c" } )]
        [TestCase(new string[] { "a", "a.b1", "a.b2" }, "a",new string[] { "a.b1", "a.b2" } )]
        [TestCase(new string[] { "a.b", "a.b.c1", "a.b.c2" }, "a.b",new string[] { "a.b.c1", "a.b.c2" } )]
        public void Are_Children_Tags_Correctly_Set(string[] tagsToLoad, string tagToCheck, string[] expectedDirectChildren)
        {
            TagManager.LoadTagsFromNames(tagsToLoad);
            var tag = Tag.GetTagFromFullName(tagToCheck);
            
            Assert.AreEqual(expectedDirectChildren.Length, tag.ChildrenTags.Count);

            for (int i = 0; i < expectedDirectChildren.Length; i++)
            {
                var childTag = Tag.GetTagFromFullName(expectedDirectChildren[i]);
                Assert.AreEqual(tag.ChildrenTags[i], childTag);
            }
        }
        
        [TestCase(new [] { "a" }, "a", true)]
        [TestCase(new [] { "a", "b" }, "b", true)]
        [TestCase(new [] { "a", "a.b.c" }, "a.b.c", true)]
        [TestCase(new [] { "a" }, "x", false)]
        [TestCase(new [] { "a.b.c" }, "a", false)]
        [TestCase(new [] { "a.b.c" }, "a.b", false)]
        [TestCase(new [] { "a" }, "a.b", false)]
        public void Is_Leaf_Tag_Correctly_Identified(string[] tagsToAdd, string tagToCheck, bool expectedResult)
        {
            TagManager.LoadTagsFromNames(tagsToAdd);
            var tag = Tag.GetTagFromFullName(tagToCheck);
            Assert.AreEqual(expectedResult, TagTreeUtils.IsLeafTagInCollection(tag, TagManager.Tags.Values));
        }
    }
}
