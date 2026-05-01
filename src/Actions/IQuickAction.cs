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

        // Called after the overlay has been dismissed and after Input.anyKeyDown has cleared
        // (so Klei's KeyDown event has settled and won't bleed into game hotkeys).
        void Invoke();
    }
}
