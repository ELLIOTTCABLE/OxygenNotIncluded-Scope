using System.Collections.Generic;
using UnityEngine;

namespace ScopeMod;

internal interface IQuickAction
{
   // Display name as the user reads it. May be rich-text-wrapped (e.g. <link="LADDER">Ladder</link>);
   // canonicalized for matching but rendered as-is by TMP.
   string DisplayName { get; }

   // Optional icon. May be null.
   Sprite Sprite { get; }

   // Build-menu subcategory key from TUNING.BUILDINGS.PLANORDER data.
   string SubcategoryKey { get; }

   // User-facing subcategory title shown in section headers.
   string SubcategoryTitle { get; }

   // Synchronous "would Submit actually do something?" check. False means Submit no-ops:
   // the overlay stays open (matching no-result behavior) instead of dismissing on a dud.
   bool CanInvoke { get; }

   // Called after the overlay has been dismissed and after Input.anyKeyDown has cleared
   // (so Klei's KeyDown event has settled and won't bleed into game hotkeys).
   void Invoke();

   // Generic availability flag for actions that can be shown but temporarily unavailable.
   // Unavailable actions should remain visible but render in the disabled style.
   bool IsCurrentlyAvailable { get; }

   // Provider-level 'sort tier' - the primary axis of comparison, for major
   // things like "if there's a camera-return, it should basically always come
   // first - the primary axis of comparison, for major things like "if there's
   // a camera-return, it should basically always come first."
   SortTier SortTier { get; }

   // Optional suffix for a demoted-tier section title (e.g. "unresearched").
   // Null = no suffix; the UI uses the default-by-tier label.
   string SearchDemotionSuffix { get; }

   // Stable identifier used by the MRU store to bias ordering toward
   // repeated user choices. Null = don't track (one-off / non-repeatable
   // actions like a calc result). Should be stable across game sessions
   // (i.e. survive serialize→deserialize).
   //
   // Convention: "<provider>:<id>" — e.g. "building:LadderConfig". The
   // prefix prevents collisions if a future provider happens to use a
   // bare identifier that overlaps.
   string MruKey { get; }

   // Strings to fuzzy-match against; combination of (string x tier). Provides a
   // somewhat-indirect way for providers to tweak some of their
   // actions'/search-strings results, without having to manually tune/tweak
   // specific integer numbers against other providers; that stays the concern
   // of the core search algo.
   IReadOnlyList<SearchTerm> SearchTerms { get; }

   // Encodes the *mutable & visible* slice of state. Include anything that
   // can flip at runtime AND affects rendering; it's folded into a
   // results-list fingerprint so the overlay can skip rebuilds when nothing
   // on-screen would actually change.
   //
   // Exclude immutable-but-visible (name, sprite - covered by MruKey
   // identity) and mutable-but-invisible (internal bookkeeping).
   int RenderStateHash { get; }
}
