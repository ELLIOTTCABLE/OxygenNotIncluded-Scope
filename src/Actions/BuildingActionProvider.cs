using System.Collections.Generic;

namespace ScopeMod {
    internal sealed class BuildingActionProvider : IActionProvider {
        public IEnumerable<IQuickAction> Enumerate() {
            // TODO: Properly mirror the build-menu, ideally extracting at runtime so it's guaranteed to match any newly-introduced limitations/heureistics
            if (PlanScreen.Instance == null) yield break;
            var seen = new HashSet<string>();
            foreach (var planInfo in TUNING.BUILDINGS.PLANORDER) {
                foreach (var pair in planInfo.buildingAndSubcategoryData) {
                    var def = Assets.GetBuildingDef(pair.Key);
                    if (def == null) continue;
                    if (!def.IsAvailable()) continue;
                    if (!def.ShouldShowInBuildMenu()) continue;
                    if (!Game.IsCorrectDlcActiveForCurrentSave(def)) continue;
                    if (!seen.Add(def.PrefabID)) continue;

                    var subcategoryKey = string.IsNullOrEmpty(pair.Value) ? "default" : pair.Value;
                    var subcategoryTitle = ResolveSubcategoryTitle(subcategoryKey);
                    yield return new BuildingSelectAction(def, subcategoryKey, subcategoryTitle);
                }
            }
        }

        private static string ResolveSubcategoryTitle(string subcategoryKey) {
            if (string.IsNullOrEmpty(subcategoryKey) || subcategoryKey == "default") return "default";

            if (Strings.TryGet(
                    "STRINGS.UI.NEWBUILDCATEGORIES." + subcategoryKey.ToUpper() + ".BUILDMENUTITLE",
                    out var title)) {
                return title;
            }

            return subcategoryKey;
        }
    }
}
