using System.Collections.Generic;
using UnityEngine;

//This class shows how to use TagQueries to check if a container matches specific conditions
namespace DeiveEx.TagTree.Examples 
{
	public class GettingStarted_TagQueries : MonoBehaviour
	{
        private void Start()
        {
            QueryBasics();
            QueryAdvanced();
            QueryAdvanced2();
        }

        private void QueryBasics()
        {
            //While you should definitely use "HasTag", "HasAny" and "HasAll" simple checks of Tags inside TagContainers, when
            //we start to mix and match dozens of tags, it can become quite hard to check which container matches a specific
            //combination of Tags.
            //TagQueries are a way to solve this problem by providing a simplified way to check for matches that you can cache
            //and reuse multiple times.
            
            //You can create a tagQuery by simply calling new and passing the matchType
            TagQuery myQuery = new TagQuery(ConditionMatchType.AnyConditionMatches);
            
            //Then you can create new conditions and add them to the Query
            Tag tagA = Tag.GetTagFromFullName("a");
            
            var hasAnyTagCondition = new QueryMatchesAny()
            {
                TagsToMatch = new List<Tag>() { tagA }
            };
            
            myQuery.AddCondition(hasAnyTagCondition);
            
            //Then you simply pass the container you want to compare with the query
            TagContainer testContainer = new TagContainer(tagA);
            
            bool containerMatchesQuery = myQuery.Match(testContainer);
            Debug.Log($"Does {nameof(testContainer)} matches Query? {containerMatchesQuery}");
            
            //IMPORTANT: TagQueries can only check for exact matches. That said, remember that when adding Tag "a.b" to a
            //container, we're also adding Tag "a" implicitly, so even if you've only added Tag "a.b", a TagQuery checking
            //for Tag "a" will still return "true". 
        }

        private void QueryAdvanced()
        {
            //The true power of Queries comes from the fact that you can combine multiple conditions. In this case, we want
            //ALL conditions to match, so if one of them doesn't match, the query fails and returns false
            TagQuery myQuery = new TagQuery(ConditionMatchType.AllConditionsMatches);
            
            Tag tag1 = Tag.GetTagFromFullName("1");
            Tag tag2 = Tag.GetTagFromFullName("2");
            Tag tag3 = Tag.GetTagFromFullName("3");
            Tag tag4 = Tag.GetTagFromFullName("4");
            Tag tag5 = Tag.GetTagFromFullName("5");
            
            //For example, let's check if our container has either Tags 1 or 2, but also needs to have both tags 3 and 4 and cannot have Tag 5.
            myQuery.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = new List<Tag>() { tag1, tag2 }
            });
            
            myQuery.AddCondition(new QueryMatchesAll()
            {
                TagsToMatch = new List<Tag>() { tag3, tag4 }
            });
            
            myQuery.AddCondition(new QueryMatchesNone()
            {
                TagsToMatch = new List<Tag>() { tag5 }
            });
            
            //And let's create some containers to test
            TagContainer containerA = new TagContainer(tag1);
            TagContainer containerB = new TagContainer(tag1, tag3, tag4); //This one matches the Query
            TagContainer containerC = new TagContainer(tag2, tag3, tag4); //This one matches the Query
            TagContainer containerD = new TagContainer(tag1, tag3, tag4, tag5);
            TagContainer containerE = new TagContainer(tag1, tag3, tag5);

            Debug.Log($"Does {nameof(containerA)} matches query? {myQuery.Match(containerA)}");
            Debug.Log($"Does {nameof(containerB)} matches query? {myQuery.Match(containerB)}");
            Debug.Log($"Does {nameof(containerC)} matches query? {myQuery.Match(containerC)}");
            Debug.Log($"Does {nameof(containerD)} matches query? {myQuery.Match(containerD)}");
            Debug.Log($"Does {nameof(containerE)} matches query? {myQuery.Match(containerE)}");
        }

        private void QueryAdvanced2()
        {
            //Another powerful feature of TagQueries is that you can nest queries into other queries, so you can create
            //truly complex queries.
            var tag1 = Tag.GetTagFromFullName("1");
            var tag2 = Tag.GetTagFromFullName("2");
            var tag3 = Tag.GetTagFromFullName("3");
            var tag4 = Tag.GetTagFromFullName("4"); //This tag should have no effect in this query

            //In this example, we want to query if the container has either tag 1 or 3, but not both, and NOT have tag 2
            var mainQuery = new TagQuery(ConditionMatchType.AllConditionsMatches);

            mainQuery.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = new () { tag1, tag3 }
            });
            
            //Create a different query that will be nested inside the first one
            var subQuery = new TagQuery(ConditionMatchType.NoConditionsMatch);

            subQuery.AddCondition(new QueryMatchesAll()
            {
                TagsToMatch = new () { tag1, tag3 }
            });

            subQuery.AddCondition(new QueryMatchesAny()
            {
                TagsToMatch = new () { tag2 }
            });
                
            mainQuery.AddCondition(subQuery);
            
            //Create some containers to test
            TagContainer containerA = new TagContainer(tag1); // This one matches the Query
            TagContainer containerB = new TagContainer(tag3); // This one matches the Query
            TagContainer containerC = new TagContainer(tag1, tag2);
            TagContainer containerD = new TagContainer(tag3, tag2);
            TagContainer containerE = new TagContainer(tag1, tag3);
            TagContainer containerF = new TagContainer(tag1, tag3);
            TagContainer containerG = new TagContainer(tag1, tag4); //This one matches the Query
            TagContainer containerH = new TagContainer(tag3, tag4); //This one matches the Query
            TagContainer containerI = new TagContainer(tag1, tag2, tag4);
            TagContainer containerJ = new TagContainer(tag1, tag2, tag3, tag4);
            
            Debug.Log($"Does {nameof(containerA)} matches query? {mainQuery.Match(containerA)}");
            Debug.Log($"Does {nameof(containerB)} matches query? {mainQuery.Match(containerB)}");
            Debug.Log($"Does {nameof(containerC)} matches query? {mainQuery.Match(containerC)}");
            Debug.Log($"Does {nameof(containerD)} matches query? {mainQuery.Match(containerD)}");
            Debug.Log($"Does {nameof(containerE)} matches query? {mainQuery.Match(containerE)}");
            Debug.Log($"Does {nameof(containerF)} matches query? {mainQuery.Match(containerF)}");
            Debug.Log($"Does {nameof(containerG)} matches query? {mainQuery.Match(containerG)}");
            Debug.Log($"Does {nameof(containerH)} matches query? {mainQuery.Match(containerH)}");
            Debug.Log($"Does {nameof(containerI)} matches query? {mainQuery.Match(containerI)}");
            Debug.Log($"Does {nameof(containerJ)} matches query? {mainQuery.Match(containerJ)}");
        }
    }
}
