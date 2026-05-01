using System.Collections.Generic;

namespace ScopeMod {
    internal static class ScopeSearch {
        // SearchUtil.IsPassingScore threshold
        private const int MIN_SCORE = 79;

        public static List<RankedResult> Rank(string query, IList<IQuickAction> actions, int limit) {
            var results = new List<RankedResult>(limit);
            if (string.IsNullOrWhiteSpace(query)) {
                int n = System.Math.Min(actions.Count, limit);
                for (int i = 0; i < n; i++) results.Add(new RankedResult(actions[i], 0));
                return results;
            }

            var canonQuery = SearchUtil.Canonicalize(query.Trim());

            int exactIdx = -1;
            for (int i = 0; i < actions.Count; i++) {
                if (SearchUtil.Canonicalize(actions[i].DisplayName) == canonQuery) {
                    exactIdx = i;
                    break;
                }
            }

            var scored = new List<RankedResult>(actions.Count);
            for (int i = 0; i < actions.Count; i++) {
                var canonName = SearchUtil.Canonicalize(actions[i].DisplayName);
                var match     = FuzzySearch.ScoreCanonicalCandidate(canonQuery, canonName);
                if (match.score >= MIN_SCORE) scored.Add(new RankedResult(actions[i], match.score));
            }
            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            if (exactIdx >= 0) {
                var exact = new RankedResult(actions[exactIdx], int.MaxValue);
                scored.RemoveAll(r => ReferenceEquals(r.Action, exact.Action));
                scored.Insert(0, exact);
            }

            int take = System.Math.Min(scored.Count, limit);
            for (int i = 0; i < take; i++) results.Add(scored[i]);
            return results;
        }
    }
}
