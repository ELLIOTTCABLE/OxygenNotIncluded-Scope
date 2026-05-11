namespace ScopeMod;

// Provider-declared sort tier. Lower enum value sorts first.
//
// Within the same tier, results sort by base fuzzy score (then MRU as
// tiebreak). See `ScopeSearch.Rank`.
internal enum SortTier
{
   // Always-on-top when present (e.g. CameraReturn while vanilla's notification
   // is alive).
   //
   // Note that pinning trumps *all* other logic; it will always be visible, and
   // always be first, when present.
   Pinned = 0,

   // Default for available actions.
   Normal = 100,

   // Currently un-actionable but conceptually present (insufficient material,
   // missing dependency). Sinks to a demoted subsection.
   Unavailable = 200,

   // Locked behind something the user can unlock (for buildings, research).
   //
   // (Not enforced, but this is intended to be something items only 'rise out
   // of' per-session; like material-missing, things that rise into
   // normal/unavailable, should generally stay in normal/unavailable for the
   // rest of the save/playthrough.)
   Locked = 300,
}
