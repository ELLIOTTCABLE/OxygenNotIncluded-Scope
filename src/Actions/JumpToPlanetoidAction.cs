using System.Collections.Generic;
using UnityEngine;

namespace ScopeMod;

internal sealed class JumpToPlanetoidAction : IQuickAction
{
   private readonly WorldContainer world;
   private readonly string cachedDisplayName;
   private readonly SearchTerm[] searchTerms;

   public JumpToPlanetoidAction(WorldContainer world)
   {
      this.world = world;
      string label = "world";
      if (world != null)
         label = world.GetProperName() ?? world.worldName ?? "world";
      cachedDisplayName = "Jump to " + label;
      searchTerms = new[]
      {
         new SearchTerm(SearchUtil.Canonicalize(cachedDisplayName), SearchTier.Primary),
         // bare planetoid name as Aux; typing "Terra" matches at exact-match
         // priority even though the action is "Jump to Terra".
         new SearchTerm(SearchUtil.Canonicalize(label), SearchTier.Aux),
      };
   }

   public string DisplayName => cachedDisplayName;
   public Sprite Sprite => null;
   public string SubcategoryKey => "planetoids";
   public string SubcategoryTitle => "planetoids";
   public bool IsCurrentlyAvailable => world != null && world.IsDiscovered;
   public bool CanInvoke => IsCurrentlyAvailable;
   public SortTier SortTier => IsCurrentlyAvailable ? SortTier.Normal : SortTier.Locked;
   public string SearchDemotionSuffix => IsCurrentlyAvailable ? null : "undiscovered";
   public string MruKey => world != null ? "planetoid:" + world.id : null;
   public IReadOnlyList<SearchTerm> SearchTerms => searchTerms;
   public int RenderStateHash => IsCurrentlyAvailable ? 1 : 0;

   public void Invoke()
   {
      if (world == null)
         return;
      try
      {
         // First-visit fly-over before swap; matches the Klei UX of clicking
         // an undiscovered-but-newly-revealed asteroid.
         if (!world.IsDupeVisited)
            world.LookAtSurface();
         var cm = ClusterManager.Instance;
         if (cm != null)
            cm.SetActiveWorld(world.id);
      }
      catch (System.Exception ex)
      {
         Log.Error($"JumpToPlanetoidAction '{cachedDisplayName}' threw: {ex}");
      }
   }
}
