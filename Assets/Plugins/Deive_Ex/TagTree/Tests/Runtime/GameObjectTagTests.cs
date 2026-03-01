using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using DeiveEx.TagTree.GameObjects;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeiveEx.TagTree.PlayModeTests
{
    public class GameObjectTagTests
    {
        private GameObject _go;
        private TagContainer _goContainer;
        
        [SetUp]
        public void Setup()
        {
            //Since the dictionary is static, we need to manually clear it
            TagTreeGOExtensions.GoTagContainers.Clear();

            if (_go != null)
                Object.Destroy(_go);
            
            _go = new GameObject("TagTestObject");
            _goContainer = _go.GetTagContainer();
        }

        [Test]
        public void No_Tags_Loaded_Error_Is_Logged()
        {
            LogAssert.Expect(LogType.Error, new Regex("No Tags loaded!.*"));
            TagManager.LoadTagsFromNames(null);
        }
        
        [Test]
        public void Is_GameObject_Tag_Container_Created()
        {
            Assert.IsNotNull(_goContainer);
        }
        
        [Test]
        public void Query_GameObjects_With_Tag()
        {
            string tagName = "a";
            TagManager.LoadTagsFromNames(tagName);

            var tag = Tag.GetTagFromFullName(tagName);
            _goContainer.AddTag(tag);

            var results = TagTreeGOQuery.GetAllWithTag(tag).ToArray();
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(_go, results[0]);
        }
        
        [Test]
        public void Query_GameObjects_With_TagQuery()
        {
            string tagName = "a";
            TagManager.LoadTagsFromNames(tagName);

            var tag = Tag.GetTagFromFullName(tagName);
            _goContainer.AddTag(tag);
            
            var query = new TagQuery(ConditionMatchType.AnyConditionMatches);
            query.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = new () { tag }
            });
            
            var results = TagTreeGOQuery.GetAllWithQuery(query).ToArray();

            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(_go, results[0]);
        }
        
        [Test]
        public void Query_GameObjects_With_Custom_Condition()
        {
            string tagName = "a";
            TagManager.LoadTagsFromNames(tagName);

            var tag = Tag.GetTagFromFullName(tagName);
            _goContainer.AddTag(tag);
            
            var results = TagTreeGOQuery.GetAllWithCondition(x => x.Tags.Count > 0).ToArray();

            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(_go, results[0]);
        }
        
        [UnityTest]
        public IEnumerator Destroyed_Objects_Not_Included_In_Query()
        {
            string tagName = "a";
            TagManager.LoadTagsFromNames(tagName);

            var tag = Tag.GetTagFromFullName(tagName);
            _goContainer.AddTag(tag);
            
            Object.Destroy(_go);
            yield return null; //Objects are not destroyed until the end of the frame

            var results = TagTreeGOQuery.GetAllWithTag(tag).ToArray();
            Assert.AreEqual(0, results.Length);
        }
    }
}
