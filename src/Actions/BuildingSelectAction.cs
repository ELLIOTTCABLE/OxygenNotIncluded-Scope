using System.Collections.Generic;
using UnityEngine;

namespace ScopeMod {
    internal sealed class BuildingSelectAction : IQuickAction {
        private readonly BuildingDef def;
        private readonly string subcategoryKey;
        private readonly string subcategoryTitle;

        public BuildingSelectAction(BuildingDef def, string subcategoryKey, string subcategoryTitle) {
            this.def = def;
            this.subcategoryKey = subcategoryKey;
            this.subcategoryTitle = subcategoryTitle;
        }

        public string DisplayName => STRINGS.UI.StripLinkFormatting(STRINGS.UI.StripStyleFormatting(def.Name));
        public Sprite Sprite => def.GetUISprite();
        public string SubcategoryKey => subcategoryKey;
        public string SubcategoryTitle => subcategoryTitle;

        public void Invoke() {
            var plan = PlanScreen.Instance;
            if (plan != null) {
                // This uses te canonical "select this building from
                // anywhere" path. Walks PLANORDER, calls open-category,
                // selects the building. Most user-friendly.
                plan.CopyBuildingOrder(def, facadeID: null);
                return;
            }

            // Fallback: drive BuildTool directly (e.g. PlanScreen not yet alive on main menu).
            // Skips category grid + element picker; cursor still gets the building.
            BuildTool.Instance?.Activate(def, new List<Tag>());
        }
    }
}
