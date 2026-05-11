namespace ScopeMod;

// A canonical-string-to-score-against, tagged with its semantic role. Each role
// earns or pays a fixed delta vs the raw fuzzy score (see
// `ScopeSearch.TierDelta`), so providers can declare *what* to score without
// knowing the calibration.
internal readonly struct SearchTerm(string canonText, SearchTier tier)
{
   // Pre-canonicalized at action construction. `ScopeSearch` feeds this to
   // `FuzzySearch.ScoreCanonicalCandidate` directly.
   public readonly string CanonText = canonText;

   public readonly SearchTier Tier = tier;
}

// Provider-declared role of a `SearchTerm`. The numeric value is purely
// for `tierDelta`-array indexing; consumers should use the enum.
internal enum SearchTier
{
   ZERO = 0, // do not use

   // The action's user-facing name (`DisplayName`). The intent is that this is
   // the value the user is 'implicitly trained' to use; it should both be
   // identifying *and* type-able (i.e. after several uses, the user will learn
   // to reflexively type `<scope>Disp<enter>`, even if they originally thought
   // of it as 'alternative namey thing.')
   Primary = 1,

   // Secondary identifier the user might also recognize: subcategory title,
   // canonical tag, etc. Worth a hit, but loses to a primary match.
   Secondary = 2,

   // Aliases, hidden synonyms, descriptions, recipe ingredients, etc. Trumped
   // by pretty much any other result.
   Aux = 3,
}
