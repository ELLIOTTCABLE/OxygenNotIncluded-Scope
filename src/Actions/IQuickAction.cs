using UnityEngine;

namespace ScopeMod {
    internal interface IQuickAction {
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

        // Search ordering only: larger values are pushed lower in typed results.
        // This is intentionally separate from IsCurrentlyAvailable so we can support
        // non-interactive-but-visible actions without forcing visual disable styling.
        int SearchDemotionTier { get; }

        // Optional suffix for demoted section title (e.g. "unresearched").
        string SearchDemotionSuffix { get; }
    }
}
