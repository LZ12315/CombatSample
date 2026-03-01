using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DeiveEx.TagTree.EditorTests
{
    [Category("TagTree")]
    public class TagQueryTests
    {
        private TagContainer _container;
        
        [SetUp]
        public void Setup()
        {
            TagManager.LoadTagsFromNames("a.b.c", "x.y.z", "1.2.3");
            _container = new TagContainer();
        }
        
        [TestCase(new[] { "a" }, new[] { "a" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a.b" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a.b.c" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a", "x" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new string [] { }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "x" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "x.y" }, ExpectedResult = false)]
        public bool TagQuery_Any(string[] containerTags, string[] tagsToCheck)
        {
            foreach (var tagName in containerTags)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            TagContainerTests.LogTagsInContainer(_container);

            var query = new TagQuery(ConditionMatchType.AnyConditionMatches);
            query.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = tagsToCheck.Select(Tag.GetTagFromFullName).ToList()
            });
            
            Debug.Log("=== Any");
            Debug.Log(string.Join("\n", tagsToCheck));

            return query.Match(_container);
        }
        
        [TestCase(new[] { "a" }, new[] { "a" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a.b" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a.b.c" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a", "a.b" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new string [] { }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "a", "x" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "x" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "x.y" }, ExpectedResult = false)]
        public bool TagQuery_All(string[] containerTags, string[] tagsToCheck)
        {
            foreach (var tagName in containerTags)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            TagContainerTests.LogTagsInContainer(_container);

            var query = new TagQuery(ConditionMatchType.AnyConditionMatches);
            query.AddCondition(new QueryMatchesAll()
            {
                TagsToMatch = tagsToCheck.Select(Tag.GetTagFromFullName).ToList()
            });
            
            Debug.Log("=== All");
            Debug.Log(string.Join("\n", tagsToCheck));

            return query.Match(_container);
        }
        
        [TestCase(new[] { "a" }, new[] { "a" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "a" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "a.b" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "a.b.c" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "a", "a.b" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new[] { "a", "x" }, ExpectedResult = false)]
        [TestCase(new[] { "a.b.c" }, new string [] { }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "x" }, ExpectedResult = true)]
        [TestCase(new[] { "a.b.c" }, new[] { "x.y" }, ExpectedResult = true)]
        public bool TagQuery_None(string[] containerTags, string[] tagsToCheck)
        {
            foreach (var tagName in containerTags)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            TagContainerTests.LogTagsInContainer(_container);

            var query = new TagQuery(ConditionMatchType.AnyConditionMatches);
            query.AddCondition(new QueryMatchesNone()
            {
                TagsToMatch = tagsToCheck.Select(Tag.GetTagFromFullName).ToList()
            });
            
            Debug.Log("=== None");
            Debug.Log(string.Join("\n", tagsToCheck));
 
            return query.Match(_container);
        }
        
        [TestCase(new string[] { "a" }, new string[] { "a" }, new string[] { "x" }, new string[] { "1" }, ExpectedResult = false)]
        [TestCase(new string[] { "a", "x" }, new string[] { "a" }, new string[] { "x" }, new string[] { "1" }, ExpectedResult = true)]
        [TestCase(new string[] { "a", "x", "1" }, new string[] { "a" }, new string[] { "x" }, new string[] { "1" }, ExpectedResult = false)]
        [TestCase(new string[] { "a.b.c", "x.y.z", "1.2.3" }, new string[] { "a", "x.y" }, new string[] { "x", "1.2" }, new string[] { }, ExpectedResult = true)]
        [TestCase(new string[] { "a.b.c", "x.y.z", "1.2.3" }, new string[] { }, new string[] { }, new string[] { }, ExpectedResult = false)]
        [TestCase(new string[] { "a.b.c", "x.y.z" }, new string[] { }, new string[] { "a", "x.y.z" }, new string[] { "1" }, ExpectedResult = false)]
        [TestCase(new string[] { "a.b.c", "x.y.z" }, new string[] { "a" }, new string[] { "a", "x.y.z" }, new string[] { "1" }, ExpectedResult = true)]
        public bool TagQuery_Complex(string[] containerTags, string[] checkForAny, string[] checkForAll, string[] checkForNone)
        {
            foreach (var tagName in containerTags)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            TagContainerTests.LogTagsInContainer(_container);

            var query = new TagQuery(ConditionMatchType.AllConditionsMatches);
            
            query.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = checkForAny.Select(Tag.GetTagFromFullName).ToList()
            });
            
            query.AddCondition(new QueryMatchesAll()
            {
                TagsToMatch = checkForAll.Select(Tag.GetTagFromFullName).ToList()
            });
            
            query.AddCondition(new QueryMatchesNone()
            {
                TagsToMatch = checkForNone.Select(Tag.GetTagFromFullName).ToList()
            });
            
            Debug.Log("=== Any");
            Debug.Log(string.Join("\n", checkForAny));
            Debug.Log("=== All");
            Debug.Log(string.Join("\n", checkForAll));
            Debug.Log("=== None");
            Debug.Log(string.Join("\n", checkForNone));

            return query.Match(_container);
        }
        
        [TestCase("a.1", ExpectedResult = true)]
        [TestCase("a.2", ExpectedResult = false)]
        [TestCase("a.3", ExpectedResult = true)]
        [TestCase("a.1", "a.3", ExpectedResult = false)]
        [TestCase("a.1", "a.2", ExpectedResult = false)]
        [TestCase("a.2", "a.3", ExpectedResult = false)]
        [TestCase("a.1", "a.2", "a.3", ExpectedResult = false)]
        [TestCase("a.4", ExpectedResult = false)]
        [TestCase("a.1", "a.4", ExpectedResult = true)]
        [TestCase("a.1", "a.2", "a.4", ExpectedResult = false)]
        public bool TagQuery_Complex_2(params string[] containerTags)
        {
            //In this example, we want to query if the container has either tag a.1 or a.3, but not both, and NOT have the tag a.2
            TagManager.LoadTagsFromNames("a.1", "a.2", "a.3", "a.4");

            var a1 = Tag.GetTagFromFullName("a.1");
            var a2 = Tag.GetTagFromFullName("a.2");
            var a3 = Tag.GetTagFromFullName("a.3");
            var a4 = Tag.GetTagFromFullName("a.4"); //This tag should have no effect in this query

            foreach (var tagName in containerTags)
            {
                _container.AddTag(Tag.GetTagFromFullName(tagName));
            }
            
            TagContainerTests.LogTagsInContainer(_container);
            
            //Create the query
            var query = new TagQuery(ConditionMatchType.AllConditionsMatches);

            query.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = new () { a1, a3 }
            });
            
            //To achieve our desired result, we can nest queries
            var subQuery = new TagQuery(ConditionMatchType.NoConditionsMatch);

            subQuery.AddCondition(new QueryMatchesAll()
            {
                TagsToMatch = new () { a1, a3 }
            });

            subQuery.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = new () { a2 }
            });
            
            query.AddCondition(subQuery);
            return query.Match(_container);
        }
    }
}
