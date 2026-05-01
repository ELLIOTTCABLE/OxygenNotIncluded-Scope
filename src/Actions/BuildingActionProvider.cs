using System.Collections.Generic;

namespace ScopeMod {
    internal sealed class BuildingActionProvider : IActionProvider {
        public IEnumerable<IQuickAction> Enumerate() {
            var ps = PlanScreen.Instance;
            if (ps == null) yield break;

            // Hard-exclude gate: identical to PlanScreen.BuildButtonList / CacheSearchCaches.
            // Anything that fails here is never added to the vanilla button list at all —
            // deprecated buildings, ShowInBuildMenu=false internal defs, and wrong-DLC content.
            //
            // Past the gate, RequirementsState is read from PlanScreen's rolling cache
            // (_buildableStatesByID), updated 10 defs/frame by ScreenUpdate →
            // RefreshBuildableStates while the game runs.
            var seen = new HashSet<string>();
            foreach (var planInfo in TUNING.BUILDINGS.PLANORDER) {
                foreach (var pair in planInfo.buildingAndSubcategoryData) {
                    var def = Assets.GetBuildingDef(pair.Key);
                    if (def == null) continue;
                    if (!def.IsAvailable()) continue;
                    if (!def.ShouldShowInBuildMenu()) continue;
                    if (!Game.IsCorrectDlcActiveForCurrentSave(def)) continue;
                    if (!seen.Add(def.PrefabID)) continue;

                    var state = ps.GetBuildableState(def);

                    var subcategoryKey = string.IsNullOrEmpty(pair.Value) ? "default" : pair.Value;
                    var subcategoryTitle = ResolveSubcategoryTitle(subcategoryKey);
                    yield return new BuildingSelectAction(def, subcategoryKey, subcategoryTitle, state);
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
