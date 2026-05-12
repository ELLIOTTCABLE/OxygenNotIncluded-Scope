using System.Collections.Generic;
using UnityEngine;

namespace ScopeMod;

internal sealed class JumpToDuplicantAction : IQuickAction
{
   private readonly MinionIdentity minion;
   private readonly string cachedDisplayName;
   private readonly string cachedMruKey;
   private readonly SearchTerm[] searchTerms;

   public JumpToDuplicantAction(MinionIdentity minion)
   {
      this.minion = minion;
      var properName = minion != null ? minion.GetProperName() : "duplicant";
      cachedDisplayName = "Jump to " + properName;
      cachedMruKey = minion != null ? "duplicant:" + properName : null;
      searchTerms = new[]
      {
         new SearchTerm(SearchUtil.Canonicalize(cachedDisplayName), SearchTier.Primary),
         // Bare name as Aux: typing just "Aria" finds the dupe even though the
         // action's display name is "Jump to Aria"; the exact-match here
         // disambiguates similar names like Ari vs Aria.
         new SearchTerm(SearchUtil.Canonicalize(properName), SearchTier.Aux),
      };
   }

   public string DisplayName => cachedDisplayName;
   public Sprite Sprite => null;
   public string SubcategoryKey => "duplicants";
   public string SubcategoryTitle => "duplicants";
   public bool IsCurrentlyAvailable => minion != null && minion.gameObject != null;
   public bool CanInvoke => IsCurrentlyAvailable;
   public SortTier SortTier => IsCurrentlyAvailable ? SortTier.Normal : SortTier.Unavailable;
   public string SearchDemotionSuffix => IsCurrentlyAvailable ? null : "no longer here";
   public string MruKey => cachedMruKey;
   public IReadOnlyList<SearchTerm> SearchTerms => searchTerms;
   public int RenderStateHash => IsCurrentlyAvailable ? 1 : 0;
   public Color? RowBgColorOverride => null;
   public Color? RowBgHoverColorOverride => null;

   public void Invoke()
   {
      if (minion == null || minion.gameObject == null)
         return;
      try
      {
         GameUtil.FocusCamera(minion.transform, select: true);
      }
      catch (System.Exception ex)
      {
         Log.Error($"JumpToDuplicantAction '{cachedDisplayName}' threw: {ex}");
      }
   }
}
