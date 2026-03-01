using System;
using System.Collections.Generic;

namespace DeiveEx.TagTree
{
    public abstract class TagQueryBase
    {
        public abstract bool Match(TagContainer container);
    }
    
    public abstract class TagQueryCondition : TagQueryBase
    {
        public List<Tag> TagsToMatch;
    }

    /// <summary>
    /// Check if any tag in the container matches the query
    /// </summary>
    public class QueryMatchesAny : TagQueryCondition
    {
        public override bool Match(TagContainer container)
        {
            foreach (var tag in TagsToMatch)
            {
                if (tag.MatchesAnyExact(container))
                    return true;
            }

            return false;
        }
    }
    
    /// <summary>
    /// Check if all tags in the container matches the query
    /// </summary>
    public class QueryMatchesAll : TagQueryCondition
    {
        public override bool Match(TagContainer container)
        {
            foreach (var tag in TagsToMatch)
            {
                if (!tag.MatchesAnyExact(container))
                    return false;
            }

            return true;
        }
    }
    
    /// <summary>
    /// Check if no tags in the container matches the query
    /// </summary>
    public class QueryMatchesNone : TagQueryCondition
    {
        public override bool Match(TagContainer container)
        {
            foreach (var tag in TagsToMatch)
            {
                if (tag.MatchesAnyExact(container))
                    return false;
            }

            return true;
        }
    }

    public enum ConditionMatchType
    {
        /// <summary>
        /// At least one condition must match. If there's no conditions, the query fails.
        /// </summary>
        AnyConditionMatches,
        
        /// <summary>
        /// All conditions must match. If there's no conditions, the query succeeds since technically no condition has failed.
        /// </summary>
        AllConditionsMatches,
        
        /// <summary>
        /// No condition must match. If there's no conditions, the query succeeds since technically no condition has failed.
        /// </summary>
        NoConditionsMatch,
    }
    
    public class TagQuery : TagQueryBase
    {
        private readonly ConditionMatchType _matchType;
        private List<TagQueryBase> _conditions;

        public TagQuery(ConditionMatchType matchType)
        {
            _matchType = matchType;
        }

        public void AddCondition(TagQueryBase condition)
        {
            _conditions ??= new();
            _conditions.Add(condition);
        }
        
        public override bool Match(TagContainer container)
        {
            if (_conditions == null || _conditions.Count == 0)
                return _matchType != ConditionMatchType.AnyConditionMatches;

            switch (_matchType)
            {
                case ConditionMatchType.AnyConditionMatches:
                    return CheckForAny(container);
                    
                case ConditionMatchType.AllConditionsMatches:
                    return CheckForAll(container);
                    
                case ConditionMatchType.NoConditionsMatch:
                    return CheckForNone(container);
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool CheckForAny(TagContainer container)
        {
            foreach (var queryCondition in _conditions)
            {
                if (queryCondition.Match(container))
                    return true;
            }

            return false;
        }
        
        private bool CheckForAll(TagContainer container)
        {
            foreach (var queryCondition in _conditions)
            {
                if (!queryCondition.Match(container))
                    return false;
            }

            return true;
        }
        
        private bool CheckForNone(TagContainer container)
        {
            foreach (var queryCondition in _conditions)
            {
                if (queryCondition.Match(container))
                    return false;
            }

            return true;
        }
    }
}
