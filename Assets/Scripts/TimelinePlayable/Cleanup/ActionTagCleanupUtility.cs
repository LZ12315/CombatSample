using System.Collections.Generic;
using DeiveEx.TagTree;
using UnityEngine;

public static class ActionTagCleanupUtility
{
    public static void Apply(Actor actor, ActionTagCleanupPhaseConfig phase)
    {
        if (actor == null || phase == null || !phase.enabled)
            return;

        var container = actor.GetTagContainer(phase.targetContainer);
        if (container == null)
            return;

        switch (phase.mode)
        {
            case ActionTagCleanupMode.All:
                container.ClearTags();
                break;
            case ActionTagCleanupMode.SpecifiedExact:
                if (phase.specifiedTags == null)
                    return;
                for (int i = 0; i < phase.specifiedTags.Count; i++)
                {
                    var tr = phase.specifiedTags[i];
                    if (tr == null) continue;
                    Tag t = tr.GetTag();
                    if (t != null)
                        actor.RemoveTag(t, phase.targetContainer);
                }
                break;
            case ActionTagCleanupMode.SpecifiedFuzzy:
                if (phase.specifiedTags == null)
                    return;
                for (int i = 0; i < phase.specifiedTags.Count; i++)
                {
                    var tr = phase.specifiedTags[i];
                    if (tr == null) continue;
                    Tag r = tr.GetTag();
                    if (r != null)
                        container.RemoveTagCompletely(r);
                }
                break;
        }
    }
}
