using System.Collections.Generic;
using ScopeMod.Mru;

namespace ScopeMod
{
   internal static class ScopeSearch
   {
      // SearchUtil.IsPassingScore threshold
      private const int MIN_SCORE = 79;

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
            // Empty-query path: MRU items lead, in recency order. Then fill
            // from `actions` skipping anything we already emitted.
            var emitted = new HashSet<string>(System.StringComparer.Ordinal);
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
                  )
                  {
                     byKey[k] = actions[i];
                  }
               }
               for (int i = 0; i < mruKeys.Count && results.Count < limit; i++)
               {
                  if (byKey.TryGetValue(mruKeys[i], out var act))
                  {
                     results.Add(new RankedResult(act, int.MaxValue - i));
                     emitted.Add(mruKeys[i]);
                  }
               }
            }

            for (int i = 0; i < actions.Count && results.Count < limit; i++)
            {
               if (!actions[i].IsCurrentlyAvailable)
                  continue;
               var k = actions[i].MruKey;
               if (k != null && emitted.Contains(k))
                  continue;
               results.Add(new RankedResult(actions[i], 0));
            }
            return results;
         }

         var canonQuery = SearchUtil.Canonicalize(query.Trim());

         int exactIdx = -1;
         for (int i = 0; i < actions.Count; i++)
         {
            if (SearchUtil.Canonicalize(actions[i].DisplayName) == canonQuery)
            {
               exactIdx = i;
               break;
            }
         }

         var scored = new List<RankedResult>(actions.Count);
         for (int i = 0; i < actions.Count; i++)
         {
            var canonName = SearchUtil.Canonicalize(actions[i].DisplayName);
            var match = FuzzySearch.ScoreCanonicalCandidate(canonQuery, canonName);
            if (match.score >= MIN_SCORE)
               scored.Add(new RankedResult(actions[i], match.score));
         }
         // MRU as score-tiebreak: among same tier + same fuzzy score, the
         // more-recently-used item wins. Doesn't perturb fuzzy primary order
         // — typing "lad" still puts "Ladder" ahead of less-matching items
         // even if the user used some other building more recently.
         scored.Sort(
            (a, b) =>
            {
               int tierCmp = a.Action.SearchDemotionTier.CompareTo(b.Action.SearchDemotionTier);
               if (tierCmp != 0)
                  return tierCmp;
               int scoreCmp = b.Score.CompareTo(a.Score);
               if (scoreCmp != 0)
                  return scoreCmp;
               if (mru != null)
               {
                  int aMru = mru.IndexOf(a.Action.MruKey ?? "");
                  int bMru = mru.IndexOf(b.Action.MruKey ?? "");
                  // Lower MRU index = more recent; -1 means "not in MRU at all".
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
         );

         if (exactIdx >= 0)
         {
            var exact = new RankedResult(actions[exactIdx], int.MaxValue);
            scored.RemoveAll(r => ReferenceEquals(r.Action, exact.Action));
            scored.Insert(0, exact);
         }

         int take = System.Math.Min(scored.Count, limit);
         for (int i = 0; i < take; i++)
            results.Add(scored[i]);
         return results;
      }
   }
}
