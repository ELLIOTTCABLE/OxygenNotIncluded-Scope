using System.Collections.Generic;
using UnityEngine;
using SysAction = System.Action;

namespace ScopeMod;

// Somewhat generic "do-a-thing" action. Used by command-style providers (tools,
// panels, overlays ...) where the action is fully described by a display name +
// one invoke callback.
internal sealed class CallbackAction : IQuickAction
{
   private readonly SysAction invoke;

   private readonly System.Func<bool> isAvailable;
   private readonly SortTier sortTier;
   private readonly SearchTerm[] searchTerms;

   // Resolvers let static-field actions defer sprite/color lookup until
   // live Klei UI is up; eager `sprite` arg wins if set.
   private readonly System.Func<Sprite> spriteResolver;
   private readonly Sprite eagerSprite;
   private readonly System.Func<Color?> rowBgResolver;
   private readonly System.Func<Color?> rowBgHoverResolver;

   public string DisplayName { get; }
   public string SubcategoryKey { get; }
   public string SubcategoryTitle { get; }
   public bool IsCurrentlyAvailable => isAvailable == null || isAvailable();
   public bool CanInvoke => IsCurrentlyAvailable && invoke != null;
   public SortTier SortTier => IsCurrentlyAvailable ? sortTier : SortTier.Unavailable;
   public string SearchDemotionSuffix => IsCurrentlyAvailable ? null : "unavailable";
   public string MruKey { get; }
   public IReadOnlyList<SearchTerm> SearchTerms => searchTerms;
   public int RenderStateHash => IsCurrentlyAvailable ? 1 : 0;

   public Sprite Sprite => eagerSprite ?? spriteResolver?.Invoke();
   public Color? RowBgColorOverride => rowBgResolver?.Invoke();
   public Color? RowBgHoverColorOverride => rowBgHoverResolver?.Invoke();

   public CallbackAction(
      string displayName,
      string subcategoryKey,
      string subcategoryTitle,
      string mruKey,
      SysAction invoke,
      System.Func<bool> isAvailable = null,
      SortTier sortTier = SortTier.Normal,
      IReadOnlyList<string> aliases = null,
      Sprite sprite = null,
      System.Func<Sprite> spriteResolver = null,
      System.Func<Color?> rowBgResolver = null,
      System.Func<Color?> rowBgHoverResolver = null
   )
   {
      DisplayName = displayName;
      SubcategoryKey = subcategoryKey;
      SubcategoryTitle = subcategoryTitle;
      MruKey = mruKey;
      this.invoke = invoke;
      this.isAvailable = isAvailable;
      this.sortTier = sortTier;
      this.searchTerms = BuildSearchTerms(displayName, subcategoryTitle, aliases);
      eagerSprite = sprite;
      this.spriteResolver = spriteResolver;
      this.rowBgResolver = rowBgResolver;
      this.rowBgHoverResolver = rowBgHoverResolver;
   }

   private static SearchTerm[] BuildSearchTerms(
      string displayName,
      string subcategoryTitle,
      IReadOnlyList<string> aliases
   )
   {
      bool hasSubcat = !string.IsNullOrEmpty(subcategoryTitle);
      int aliasCount = aliases?.Count ?? 0;
      var sources = new SearchTerm[1 + (hasSubcat ? 1 : 0) + aliasCount];
      int i = 0;
      sources[i++] = new SearchTerm(SearchUtil.Canonicalize(displayName), SearchTier.Primary);
      if (hasSubcat)
         sources[i++] = new SearchTerm(
            SearchUtil.Canonicalize(subcategoryTitle),
            SearchTier.Secondary
         );
      for (int j = 0; j < aliasCount; j++)
      {
         var a = aliases[j];
         if (!string.IsNullOrEmpty(a))
            sources[i++] = new SearchTerm(SearchUtil.Canonicalize(a), SearchTier.Aux);
      }
      if (i < sources.Length)
         System.Array.Resize(ref sources, i);
      return sources;
   }

   public void Invoke()
   {
      try
      {
         invoke?.Invoke();
      }
      catch (System.Exception ex)
      {
         Log.Error($"CallbackAction '{DisplayName}' threw: {ex}");
      }
   }
}
