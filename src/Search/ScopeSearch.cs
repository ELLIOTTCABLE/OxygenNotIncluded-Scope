using System;
using System.Collections.Generic;
using Roslyn.Utilities;
using ScopeMod.Mru;

namespace ScopeMod;

internal static class ScopeSearch
{
   // Per-tier calibration vs the raw `FuzzySearch` score. `_` arm catches the
   // `default(SearchTerm).Tier` (`(SearchTier)0`) case.
   private static int TierDelta(SearchTier tier) =>
      tier switch
      {
         SearchTier.Primary => 0,
         SearchTier.Secondary => -5,
         SearchTier.Aux => -10,
         // I'd love to log this but C# is stupid.
         _ => int.MinValue / 2,
      };

   private readonly struct ScoreCache
   {
      public readonly int Score;
      public readonly bool IsExactPrimary;

      public ScoreCache(int score, bool isExactPrimary)
      {
         Score = score;
         IsExactPrimary = isExactPrimary;
      }
   }

   // Tiny memoization for a stable canonicalized query (i.e., if a provider
   // dirties without a keystroke) re-uses the scoring.
   private static string cachedQuery;
   private static readonly Dictionary<IQuickAction, ScoreCache> scoreCache = new(256);

   private static MruStore cachedComparatorMru;
   private static Comparison<RankedResult> cachedComparator;

   private static Comparison<RankedResult> GetComparator(MruStore mru)
   {
      if (!ReferenceEquals(cachedComparatorMru, mru))
      {
         cachedComparatorMru = mru;
         cachedComparator = (a, b) => CompareResults(a, b, mru);
      }
      return cachedComparator;
   }

   private static int CompareResults(RankedResult a, RankedResult b, MruStore mru)
   {
      int tierCmp = ((int)a.Action.SortTier).CompareTo((int)b.Action.SortTier);
      if (tierCmp != 0)
         return tierCmp;
      int scoreCmp = b.Score.CompareTo(a.Score);
      if (scoreCmp != 0)
         return scoreCmp;
      if (mru != null)
      {
         int aMru = mru.IndexOf(a.Action.MruKey ?? "");
         int bMru = mru.IndexOf(b.Action.MruKey ?? "");
         if (aMru != bMru)
         {
            if (aMru == -1)
               return 1;
            if (bMru == -1)
               return -1;
            return aMru - bMru;
         }
      }
      return 0;
   }

   public static List<RankedResult> Rank(
      string query,
      IList<IQuickAction> actions,
      int limit,
      MruStore mru = null
   )
   {
      var results = new List<RankedResult>(limit);
      if (string.IsNullOrWhiteSpace(query))
      {
         // Empty-query: Pinned > MRU > remaining; this bypasses the normal
         // comapartor.
         var emitted = new HashSet<string>(System.StringComparer.Ordinal);

         for (int i = 0; i < actions.Count && results.Count < limit; i++)
         {
            var a = actions[i];
            if (!a.IsCurrentlyAvailable || a.SortTier != SortTier.Pinned)
               continue;
            results.Add(new RankedResult(a, int.MaxValue));
            if (!string.IsNullOrEmpty(a.MruKey))
               emitted.Add(a.MruKey);
         }

         if (mru != null)
         {
            var mruKeys = mru.Keys;
            var byKey = new Dictionary<string, IQuickAction>(
               actions.Count,
               System.StringComparer.Ordinal
            );
            for (int i = 0; i < actions.Count; i++)
            {
               var k = actions[i].MruKey;
               if (
                  !string.IsNullOrEmpty(k)
                  && !byKey.ContainsKey(k)
                  && actions[i].IsCurrentlyAvailable
                  && !emitted.Contains(k)
               )
               {
                  byKey[k] = actions[i];
               }
            }
            for (int i = 0; i < mruKeys.Count && results.Count < limit; i++)
            {
               if (byKey.TryGetValue(mruKeys[i], out var act))
               {
                  results.Add(new RankedResult(act, int.MaxValue - 1 - i));
                  emitted.Add(mruKeys[i]);
               }
            }
         }

         for (int i = 0; i < actions.Count && results.Count < limit; i++)
         {
            var a = actions[i];
            if (!a.IsCurrentlyAvailable || a.SortTier == SortTier.Pinned)
               continue;
            var k = a.MruKey;
            if (k != null && emitted.Contains(k))
               continue;
            results.Add(new RankedResult(a, 0));
         }
         return results;
      }

      var canonQuery = SearchUtil.Canonicalize(query.Trim());

      if (canonQuery != cachedQuery)
      {
         scoreCache.Clear();
         cachedQuery = canonQuery;
      }

      IQuickAction exactAction = null;
      var scored = new List<RankedResult>(actions.Count);
      for (int i = 0; i < actions.Count; i++)
      {
         var action = actions[i];
         if (!scoreCache.TryGetValue(action, out var entry))
         {
            int s = ScoreAction(action, canonQuery, out bool ep);
            entry = new ScoreCache(s, ep);
            scoreCache[action] = entry;
         }
         if (entry.Score != int.MinValue)
            scored.Add(new RankedResult(action, entry.Score));
         if (exactAction == null && entry.IsExactPrimary)
            exactAction = action;
      }

      // Sort policy:
      // 1. `SortTier` (provider-declared; Pinned > Normal > Unavailable > Locked)
      // 2. Base fuzzy score (per-source-tier, folded in by `ScoreAction`)
      // 3. MRU recency (tiebreak)
      //
      // NOTE: MRU is currently a tiebreak (a higher-score non-MRU action still
      //    beats a lower-score MRU action.) If frecency lifts MRU to a stronger
      //    role later, I'll compose MRU INTO stage 2 (additive to the score.)

      scored.Sort(GetComparator(mru));

      if (exactAction != null)
      {
         for (int i = 0; i < scored.Count; i++)
         {
            if (ReferenceEquals(scored[i].Action, exactAction))
            {
               scored.RemoveAt(i);
               break;
            }
         }
         scored.Insert(0, new RankedResult(exactAction, int.MaxValue));
      }

      int take = System.Math.Min(scored.Count, limit);
      for (int i = 0; i < take; i++)
         results.Add(scored[i]);
      return results;
   }

   // Central scorer: max-of-sources with per-tier delta, threshold-filtered
   // on the *adjusted* score (so a low-quality alias match doesn't sneak
   // through as a misleading near-threshold hit). Side-output: did any
   // `Primary` source exact-match the query?
   [PerformanceSensitive("scope-search-hot-path")]
   private static int ScoreAction(IQuickAction action, string canonQuery, out bool isExactPrimary)
   {
      isExactPrimary = false;
      var sources = action.SearchTerms;
      if (sources == null)
         return int.MinValue;
      int best = int.MinValue;
      for (int i = 0; i < sources.Count; i++)
      {
         var src = sources[i];
         if (string.IsNullOrEmpty(src.CanonText))
            continue;
         if (src.Tier == SearchTier.Primary && src.CanonText == canonQuery)
            isExactPrimary = true;
         int raw = FuzzySearch.ScoreCanonicalCandidate(canonQuery, src.CanonText).score;
         int adjusted = raw + TierDelta(src.Tier);
         if (adjusted >= SearchUtil.MATCH_SCORE_THRESHOLD && adjusted > best)
            best = adjusted;
      }
      return best;
   }
}
