using System;
using System.Collections.Generic;
using SysAction = System.Action;

namespace ScopeMod;

internal sealed class BuildingActionProvider : IActionProvider
{
   private List<BuildingSelectAction> cached;
   private PlanScreen cachedFor;

   // Slow-poll for material-mass thresholds
   public TimeSpan? PollInterval => TimeSpan.FromSeconds(1.5);

   private SysAction markDirty;

   public void OnActivate(IProviderContext ctx)
   {
      markDirty = ctx.MarkDirty;

      // Action-list deltas: DLC content gating, world set mutation
      ctx.Subscribe((int)GameHashes.ActiveWorldChanged, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.WorldAdded, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.WorldRemoved, _ => ctx.MarkDirty());

      // Per-action requirements transitions observable as point events
      ctx.Subscribe((int)GameHashes.ResearchComplete, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.ElementNoLongerAvailable, _ => ctx.MarkDirty());

      if (DiscoveredResources.Instance is { } disc)
      {
         Action<Tag, Tag> onDiscover = (_, _) => ctx.MarkDirty();
         disc.OnDiscover += onDiscover;
         ctx.Defer(() =>
         {
            if (DiscoveredResources.Instance is { } live)
               live.OnDiscover -= onDiscover;
         });
      }
   }

   public void OnDeactivate()
   {
      markDirty = null;
      // Drop session-local refs; OnActivate against a fresh PlanScreen
      // (post-save-reload) rebuilds the cache from scratch
      cached = null;
      cachedFor = null;
   }

   // Mirror of Klei's transition-detecting `RefreshBuildableStates`:
   //
   //     _buildableStatesByID == newState ? continue : update
   //
   // no transitions, then no dirty, and no re-rank
   public void OnPoll()
   {
      var ps = PlanScreen.Instance;
      if (ps == null || cached == null)
         return;

      bool anyChanged = false;
      for (int i = 0; i < cached.Count; i++)
      {
         var prev = cached[i].RequirementsState;
         cached[i].RefreshState(ps);
         if (cached[i].RequirementsState != prev)
            anyChanged = true;
      }
      if (anyChanged)
         markDirty?.Invoke();
   }

   public IEnumerable<IQuickAction> Enumerate()
   {
      var ps = PlanScreen.Instance;
      if (ps == null)
         yield break;

      if (cached == null || !ReferenceEquals(cachedFor, ps))
      {
         cached = BuildCache(ps);
         cachedFor = ps;
      }

      for (int i = 0; i < cached.Count; i++)
      {
         cached[i].RefreshState(ps);
         yield return cached[i];
      }
   }

   // Hard-exclude gate: identical to PlanScreen.BuildButtonList / CacheSearchCaches.
   // Anything that fails here is never added to the vanilla button list at all —
   // deprecated buildings, ShowInBuildMenu=false internal defs, and wrong-DLC content.
   //
   // Past the gate, RequirementsState is read from PlanScreen's rolling cache
   // (_buildableStatesByID), updated 10 defs/frame by ScreenUpdate →
   // RefreshBuildableStates while the game runs.
   private static List<BuildingSelectAction> BuildCache(PlanScreen ps)
   {
      var list = new List<BuildingSelectAction>(256);
      var seen = new HashSet<string>();
      foreach (var planInfo in TUNING.BUILDINGS.PLANORDER)
      {
         foreach (var pair in planInfo.buildingAndSubcategoryData)
         {
            var def = Assets.GetBuildingDef(pair.Key);
            if (def == null)
               continue;
            if (!def.IsAvailable())
               continue;
            if (!def.ShouldShowInBuildMenu())
               continue;
            if (!Game.IsCorrectDlcActiveForCurrentSave(def))
               continue;
            if (!seen.Add(def.PrefabID))
               continue;

            var state = ps.GetBuildableState(def);

            var subcategoryKey = string.IsNullOrEmpty(pair.Value) ? "default" : pair.Value;
            var subcategoryTitle = ResolveSubcategoryTitle(subcategoryKey);
            list.Add(new BuildingSelectAction(def, subcategoryKey, subcategoryTitle, state));
         }
      }
      return list;
   }

   private static string ResolveSubcategoryTitle(string subcategoryKey)
   {
      if (string.IsNullOrEmpty(subcategoryKey) || subcategoryKey == "default")
         return "default";

      if (
         Strings.TryGet(
            "STRINGS.UI.NEWBUILDCATEGORIES." + subcategoryKey.ToUpper() + ".BUILDMENUTITLE",
            out var title
         )
      )
      {
         return title;
      }

      return subcategoryKey;
   }
}
