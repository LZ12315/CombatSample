using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DeiveEx.TagTree.EditorTests
{
    [Category("TagTree")]
    public class TagContainerTests
    {
        private TagContainer _container;
        
        [SetUp]
        public void Setup()
        {
            _container = new();
        }
        
        [TestCase("a")]
        [TestCase("a.b")]
        [TestCase("a.b.c")]
        public void Is_Tag_Added_To_Container(string tagToAdd)
        {
            TagManager.LoadTagsFromNames(tagToAdd);
            var tagObj = Tag.GetTagFromFullName(tagToAdd);

            _container.AddTag(tagObj);

            //The entire hierarchy of tags should be added
            Assert.AreEqual(TagManager.Tags.Count, _container.CurrentTags.Count);
            
            foreach (var tag in TagManager.Tags.Values)
            {
                Assert.IsTrue(_container.HasTag(tag));
                Assert.IsTrue(_container.CurrentTags[tag] == 1);
            }
        }
        
        [Test]
        public void Tag_Added_Throws_Exception()
        {
            string tagName = "a";
            TagManager.LoadTagsFromNames(tagName);

            var tag = Tag.GetTagFromFullName("x");
            Assert.Throws<InvalidOperationException>(() => _container.AddTag(tag));
        }
        
        [TestCase("a")]
        [TestCase("a.b")]
        [TestCase("a.b.c")]
        public void Is_Tag_Removed_From_Container(string tagToAdd)
        {
            TagManager.LoadTagsFromNames(tagToAdd);
            var tag = Tag.GetTagFromFullName(tagToAdd);
            _container.AddTag(tag);
            _container.RemoveTag(tag);
            
            Assert.IsFalse(_container.HasTag(tag));
            Assert.IsFalse(_container.CurrentTags.ContainsKey(tag));
        }

        [Test]
        public void Tag_Removed_Returns_True()
        {
            string tagName = "a";
            TagManager.LoadTagsFromNames(tagName);

            var tag = Tag.GetTagFromFullName("a");
            _container.AddTag(tag);
            
            Assert.IsTrue(_container.RemoveTag(tag));
        }
        
        [Test]
        public void Tag_Removed_Returns_False()
        {
            string tagNameA = "a";
            string tagNameB = "b";
            TagManager.LoadTagsFromNames(tagNameA, tagNameB);

            var tag = Tag.GetTagFromFullName("a");
            _container.AddTag(tag);
            
            var differentTag = Tag.GetTagFromFullName("b");
            Assert.IsFalse(_container.RemoveTag(differentTag));
        }
        
        [TestCase(new string [] { "a" }, "a", new string [] { })]
        [TestCase(new string [] { "a", "a" }, "a", new string [] { })]
        [TestCase(new string [] { "a", "x" }, "a", new string [] { "x" })]
        [TestCase(new string [] { "a.b" }, "a", new string [] { })]
        [TestCase(new string [] { "a", "a.b" }, "a.b", new string [] { "a" })]
        [TestCase(new string [] { "a", "a.b", "a.b.c" }, "a", new string [] { })]
        [TestCase(new string [] { "a", "a.b", "a.b.c" }, "a.b.c", new string [] { "a", "a.b" })]
        public void Is_Tag_Removed_From_Container_Completely(string[] tagsToAdd, string tagToRemove, string[] expectedRemainingTags)
        {
            TagManager.LoadTagsFromNames(tagsToAdd);

            foreach (var tagName in tagsToAdd)
            {
                var tag = Tag.GetTagFromFullName(tagName);
                _container.AddTag(tag);
            }

            var removeTag = Tag.GetTagFromFullName(tagToRemove);
            _container.RemoveTagCompletely(removeTag);
            
            Assert.AreEqual(expectedRemainingTags.Length, _container.CurrentTags.Count);

            foreach (var remainingTagName in expectedRemainingTags)
            {
                var remainingTag = Tag.GetTagFromFullName(remainingTagName);
                Assert.IsTrue(_container.CurrentTags.ContainsKey(remainingTag));
            }
        }

        [Test]
        public void Tag_RemovedCompletely_Returns_True()
        {
            string tagName = "a";
            TagManager.LoadTagsFromNames(tagName);

            var tag = Tag.GetTagFromFullName("a");
            _container.AddTag(tag);
            
            Assert.IsTrue(_container.RemoveTagCompletely(tag));
        }
        
        [Test]
        public void Tag_RemovedCompletely_Returns_False()
        {
            string tagNameA = "a";
            string tagNameB = "b";
            TagManager.LoadTagsFromNames(tagNameA, tagNameB);

            var tag = Tag.GetTagFromFullName("a");
            _container.AddTag(tag);
            
            var differentTag = Tag.GetTagFromFullName("b");
            Assert.IsFalse(_container.RemoveTagCompletely(differentTag));
        }

        [TestCase("a.b.c", 1, "a.b.c", 1, "a.b.c", 0)]
        [TestCase("a.b.c", 1, "a.b.c", 0, "a.b.c", 1)]
        [TestCase("a.b.c", 0, "a.b.c", 1, "a.b.c", 0)]
        [TestCase("a.b.c", 5, "a.b.c", 3, "a.b.c", 2)]
        [TestCase("a.b", 1, "a.b.c", 1, "a.b", 1)]
        [TestCase("a.b", 1, "a.b.c", 1, "a.b.c", 0)]
        [TestCase("a.b", 1, "a.b.c", 1, "a.b", 1)]
        [TestCase("a.b.c", 3, "a.b", 1, "a.b.c", 3)]
        public void Is_Tag_Counter_Modified_Correctly(string tagToAdd, int addCount, string tagToRemove, int removeCount, string tagToCheck, int expectedCount)
        {
            TagManager.LoadTagsFromNames(tagToAdd, tagToRemove, tagToCheck);

            var addTag = Tag.GetTagFromFullName(tagToAdd);
            var removeTag = Tag.GetTagFromFullName(tagToRemove);
            var checkTag = Tag.GetTagFromFullName(tagToCheck);
            
            for (int i = 0; i < addCount; i++)
            {
                _container.AddTag(addTag);
            }
            
            for (int i = 0; i < removeCount; i++)
            {
                _container.RemoveTag(removeTag);
            }
            
            Assert.AreEqual(expectedCount > 0, _container.CurrentTags.ContainsKey(checkTag));
            Assert.AreEqual(expectedCount, _container.GetTagCount(checkTag));
        }
        
        [TestCase(new [] { "a" }, new [] { "a" }, "a", 0)]
        [TestCase(new [] { "a", "a" }, new [] { "a" }, "a", 1)]
        [TestCase(new [] { "a", "a" }, new string [0], "a", 2)]
        [TestCase(new [] { "a", "a.b" }, new [] { "a" }, "a", 1)]
        [TestCase(new [] { "a", "a.b" }, new [] { "a.b" }, "a", 1)]
        [TestCase(new [] { "a", "a.b" }, new [] { "a.b" }, "a.b", 0)]
        [TestCase(new [] { "a", "a.b" }, new [] { "a", "a" }, "a", 1)]
        [TestCase(new [] { "a", "a.b", "a.b.c" }, new string [0], "a", 3)]
        [TestCase(new [] { "a", "a.b", "a.b.c" }, new [] { "a.b" }, "a", 2)]
        [TestCase(new [] { "a", "a.b", "a.b.c" }, new [] { "a.b.c" }, "a", 2)]
        public void Is_Tag_Counter_Modified_Correctly_2(string[] tagsToAdd, string[] tagsToRemove, string tagToCheck, int expectedCount)
        {
            var tagsToLoad = new List<string>();
            tagsToLoad.AddRange(tagsToAdd);
            tagsToLoad.AddRange(tagsToRemove);
            tagsToLoad.Add(tagToCheck);
            TagManager.LoadTagsFromNames(tagsToLoad.ToArray());
            
            foreach (var tag in tagsToAdd)
            {
                var addTag = Tag.GetTagFromFullName(tag);
                _container.AddTag(addTag);
            }
            
            foreach (var tag in tagsToRemove)
            {
                var removeTag = Tag.GetTagFromFullName(tag);
                _container.RemoveTag(removeTag);
            }
            
            var checkTag = Tag.GetTagFromFullName(tagToCheck);
            
            Assert.AreEqual(expectedCount > 0, _container.CurrentTags.ContainsKey(checkTag));
            Assert.AreEqual(expectedCount, _container.GetTagCount(checkTag));
        }

        [Test]
        public void Is_TagContainer_Cleared()
        {
            string tagName = "a.b.c";
            TagManager.LoadTagsFromNames(tagName);
            var tag = Tag.GetTagFromFullName(tagName);
            
            _container.AddTag(tag);
            Assert.AreEqual(3, _container.CurrentTags.Count);
            
            _container.ClearTags();
            Assert.AreEqual(0, _container.CurrentTags.Count);
        }

        [TestCase("a", "a", ExpectedResult = true)]
        [TestCase("a.b", "a", ExpectedResult = true)]
        [TestCase("a.b.c", "a", ExpectedResult = true)]
        [TestCase("a.b.c", "a.b", ExpectedResult = true)]
        [TestCase("a.b.c", "a.b.c", ExpectedResult = true)]
        [TestCase("a", "a.b", ExpectedResult = false)]
        [TestCase("a", "a.b.c", ExpectedResult = false)]
        [TestCase("a", "x", ExpectedResult = false)]
        public bool Container_Has_Tag(string tagToAdd, string tagToCheck)
        {
            TagManager.LoadTagsFromNames(tagToAdd, tagToCheck);
            
            var addTag = Tag.GetTagFromFullName(tagToAdd);
            var checkTag = Tag.GetTagFromFullName(tagToCheck);
            
            _container.AddTag(addTag);
            return _container.HasTag(checkTag);
        }

        [TestCase(new [] { "a" }, new [] { "a" }, true)]
        [TestCase(new [] { "a.b.c" }, new [] { "a" }, true)]
        [TestCase(new [] { "a.b.c" }, new [] { "a.b" }, true)]
        [TestCase(new [] { "a.b.c" }, new [] { "a.b.c" }, true)]
        [TestCase(new [] { "a.b.c" }, new [] { "a", "x" }, true)]
        [TestCase(new [] { "a.b.c" }, new [] { "x" }, false)]
        [TestCase(new [] { "a.b.c" }, new [] { "x", "x.y" }, false)]
        [TestCase(new [] { "a" }, new string [] {}, false)]
        [TestCase(new string [] {}, new [] { "a" }, false)]
        public void Container_Has_Any_Tag(string[] containerA, string[] containerB, bool expectedResult)
        {
            TagManager.LoadTagsFromNames(containerA.Concat(containerB).ToArray());

            foreach (var tagName in containerA)
            {
                var tag = Tag.GetTagFromFullName(tagName);
                _container.AddTag(tag);
            }

            LogTagsInContainer(_container, "A");
            
            TagContainer other = new();
                
            foreach (var tagName in containerB)
            {
                var tag = Tag.GetTagFromFullName(tagName);
                other.AddTag(tag);
            }
            
            LogTagsInContainer(other, "B");
            
            Assert.AreEqual(expectedResult, _container.HasAny(other));
        }
        
        [TestCase(new [] { "a" }, new [] { "a" }, true)]
        [TestCase(new [] { "a", "x" }, new [] { "a" }, true)]
        [TestCase(new [] { "a", "x" }, new [] { "x" }, true)]
        [TestCase(new [] { "a", "x" }, new [] { "a", "x" }, true)]
        [TestCase(new [] { "a.b.c" }, new [] { "a", "a.b", "a.b.c" }, true)]
        [TestCase(new [] { "a" }, new string [] {}, true)]
        [TestCase(new string [] {}, new [] { "a" }, false)]
        [TestCase(new [] { "a" }, new [] { "a", "b" }, false)]
        [TestCase(new [] { "a" }, new [] { "x" }, false)]
        [TestCase(new [] { "a.b.c" }, new [] { "x" }, false)]
        public void Container_Has_All_Tags(string[] containerA, string[] containerB, bool expectedResult)
        {
            TagManager.LoadTagsFromNames(containerA.Concat(containerB).ToArray());

            foreach (var tagName in containerA)
            {
                var tag = Tag.GetTagFromFullName(tagName);
                _container.AddTag(tag);
            }

            LogTagsInContainer(_container, "A");
            
            TagContainer other = new();
            
            foreach (var tagName in containerB)
            {
                var tag = Tag.GetTagFromFullName(tagName);
                other.AddTag(tag);
            }
            
            LogTagsInContainer(other, "B");
            
            Assert.AreEqual(expectedResult, _container.HasAll(other));
        }

        [Test]
        public void Container_Has_Null()
        {
            Assert.Throws<NullReferenceException>(() => _container.HasAny(null));
            Assert.Throws<NullReferenceException>(() => _container.HasAll(null));
        }
        
        [Test]
        public void Is_Event_Tag_Added_Raised()
        {
            var tagName = "a";
            TagManager.LoadTagsFromNames(tagName);
            var tag = Tag.GetTagFromFullName(tagName);
            bool eventRaised = false;

            _container.TagChanged += (sender, e) =>
            {
                if (e.Tag == tag && e.Event == TagChangedEvent.TagAdded)
                    eventRaised = true;
            };
            
            _container.AddTag(tag);
            
            Assert.IsTrue(eventRaised);
        }
        
        [Test]
        public void Is_Event_Tag_Removed_Raised()
        {
            var tagName = "a";
            TagManager.LoadTagsFromNames(tagName);
            var tag = Tag.GetTagFromFullName(tagName);
            bool eventRaised = false;

            _container.TagChanged += (sender, e) =>
            {
                if (e.Tag == tag && e.Event == TagChangedEvent.TagRemoved)
                    eventRaised = true;
            };
            
            _container.AddTag(tag);
            _container.RemoveTag(tag);
            
            Assert.IsTrue(eventRaised);
        }
        
        [TestCase(1, ExpectedResult = true)]
        [TestCase(5, ExpectedResult = true)]
        [TestCase(0, ExpectedResult = false)]
        public bool Is_Event_Tag_Removed_Raised_2(int addAmount)
        {
            var tagName = "a";
            TagManager.LoadTagsFromNames(tagName);
            var tag = Tag.GetTagFromFullName(tagName);
            bool eventRaised = false;

            _container.TagChanged += (sender, e) =>
            {
                if (e.Tag == tag && e.Event == TagChangedEvent.TagRemoved)
                    eventRaised = true;
            };
            
            for (int i = 0; i < addAmount; i++)
            {
                _container.AddTag(tag);
            }
            
            _container.RemoveTagCompletely(tag);

            return eventRaised;
        }
        
        [TestCase(1, 1, ExpectedResult = 0)]
        [TestCase(2, 1, ExpectedResult = 1)]
        [TestCase(5, 1, ExpectedResult = 4)]
        [TestCase(0, 1, ExpectedResult = 0)]
        [TestCase(0, 5, ExpectedResult = 0)]
        public int Is_Event_Tag_Counter_Increased_Raised(int addAmount, int removeAmount)
        {
            var tagName = "a";
            TagManager.LoadTagsFromNames(tagName);
            var tag = Tag.GetTagFromFullName(tagName);
            int eventRaisedCounter = 0;

            _container.TagChanged += (sender, e) =>
            {
                if (e.Tag == tag && e.Event == TagChangedEvent.TagCounterIncreased)
                    eventRaisedCounter++;
            };

            for (int i = 0; i < addAmount; i++)
            {
                _container.AddTag(tag);
            }
            
            for (int i = 0; i < removeAmount; i++)
            {
                _container.RemoveTag(tag);
            }

            return eventRaisedCounter;
        }
        
        [TestCase(1, 1, ExpectedResult = 0)]
        [TestCase(2, 2, ExpectedResult = 1)]
        [TestCase(5, 2, ExpectedResult = 2)]
        [TestCase(0, 1, ExpectedResult = 0)]
        [TestCase(0, 5, ExpectedResult = 0)]
        public int Is_Event_Tag_Counter_Decreased_Raised(int addAmount, int removeAmount)
        {
            var tagName = "a";
            TagManager.LoadTagsFromNames(tagName);
            var tag = Tag.GetTagFromFullName(tagName);
            int eventRaisedCounter = 0;

            _container.TagChanged += (sender, e) =>
            {
                if (e.Tag == tag && e.Event == TagChangedEvent.TagCounterDecreased)
                    eventRaisedCounter++;
            };

            for (int i = 0; i < addAmount; i++)
            {
                _container.AddTag(tag);
            }
            
            for (int i = 0; i < removeAmount; i++)
            {
                _container.RemoveTag(tag);
            }

            return eventRaisedCounter;
        }

        #region Utility

        internal static void LogTagsInContainer(TagContainer container, string containerName = null)
        {
            Debug.Log($"=== Container {containerName}");
            Debug.Log(container);
        }

        #endregion
    }
}
