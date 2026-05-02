using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ScopeMod {
    internal sealed class BuildingSelectAction : IQuickAction {
        // MaterialSelectionPanel keeps its selector list private; we need it to
        // reach into individual MaterialSelectors that vanilla AutoSelect left empty.
        private static readonly FieldInfo materialSelectorsField =
            typeof(MaterialSelectionPanel).GetField(
                "materialSelectors",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly BuildingDef def;
        private readonly string subcategoryKey;
        private readonly string subcategoryTitle;
        private readonly PlanScreen.RequirementsState requirementsState;

        public BuildingSelectAction(BuildingDef def, string subcategoryKey, string subcategoryTitle, PlanScreen.RequirementsState requirementsState) {
            this.def = def;
            this.subcategoryKey = subcategoryKey;
            this.subcategoryTitle = subcategoryTitle;
            this.requirementsState = requirementsState;
        }

        // TODO: surface this as a config option (and pair it with an "open research tree
        // and jump to" follow-up action). For now we match vanilla: unresearched is blocked.
        private const bool AllowUnresearchedSelection = false;

        public string DisplayName => STRINGS.UI.StripLinkFormatting(STRINGS.UI.StripStyleFormatting(def.Name));
        public Sprite Sprite => def.GetUISprite();
        public string SubcategoryKey => subcategoryKey;
        public string SubcategoryTitle => subcategoryTitle;
        public bool IsCurrentlyAvailable => requirementsState == PlanScreen.RequirementsState.Complete;
        public bool CanInvoke =>
            AllowUnresearchedSelection || requirementsState != PlanScreen.RequirementsState.Tech;
        public int SearchDemotionTier => IsCurrentlyAvailable ? 0 : 1;
        public string SearchDemotionSuffix => requirementsState == PlanScreen.RequirementsState.Tech ? "unresearched" : "unavailable";
        public PlanScreen.RequirementsState RequirementsState => requirementsState;

        public void Invoke() {
            var plan = PlanScreen.Instance;
            if (plan != null) {
                // Canonical "select this building from anywhere" vanilla func:
                // walks PLANORDER, opens the category, selects the building,
                // runs ProductInfoScreen.ConfigureScreen.
                plan.CopyBuildingOrder(def, facadeID: null);
                EnsureCursorHoldsBuilding(plan);
                return;
            }

            // Fallback: drive BuildTool directly (e.g. PlanScreen not yet alive on main menu).
            BuildTool.Instance?.Activate(def, def.DefaultElements());
        }

        // Vanilla's AutoSelectAvailableMaterial only picks tags whose mass >= activeMass,
        // so when every material is empty no MaterialSelector ends up with a CurrentSelectedElement.
        // ActivateAppropriateTool then either drops to PrebuildTool (no insufficient-build mod) or
        // leaves whatever tool was active (with Cairath's PlanBuildingsWithoutMaterials, since
        // AllSelectorsSelected stays false and the BuildTool branch is skipped). Either way the
        // cursor isn't actually carrying the building. We force a fallback pick per selector and
        // then force the correct build tool active, which is a no-op in the happy path.
        private void EnsureCursorHoldsBuilding(PlanScreen plan) {
            var pis = plan.ProductInfoScreen;
            var msp = pis?.materialSelectionPanel;
            if (msp == null) {
                ActivateBuildTool(def.DefaultElements(), facadeID: null);
                return;
            }

            ForceFallbackSelections(msp);

            var elements = ResolveElements(msp);
            var facadeID = pis.FacadeSelectionPanel?.SelectedFacade;

            if (!IsCorrectToolActive(def)) ActivateBuildTool(elements, facadeID);
        }

        private static void ForceFallbackSelections(MaterialSelectionPanel msp) {
            var selectors = materialSelectorsField?.GetValue(msp) as List<MaterialSelector>;
            if (selectors == null) return;

            foreach (var sel in selectors) {
                if (!sel.gameObject.activeSelf) continue;
                if (sel.CurrentSelectedElement != null) continue;

                var fallback = PickFallbackElement(sel);
                if (!fallback.IsValid) continue;

                // recipe = null skips writing the per-slot persistent default,
                // so a 0kg pick doesn't overwrite the user's saved preference.
                sel.OnSelectMaterial(fallback, null, focusScrollRect: false);
            }
        }

        // Generally, trying to ape vanilla's behaviour: priority is (1) any
        // in-stock material, ranked by vanilla build-menu sort first (Copper
        // before Niobium) then by mass desc within the same sort tier; (2) when
        // nothing is in stock, the lowest-sort material so we get sane defaults
        // instead of 0kg of an exotic; (3) any toggle as last resort.
        // TODO: MRU.
        private static Tag PickFallbackElement(MaterialSelector sel) {
            var inv = ClusterManager.Instance?.activeWorld?.worldInventory;

            Tag best = Tag.Invalid;
            float bestMass = 0f;
            foreach (var kvp in sel.ElementToggles) {
                if (!kvp.Value.gameObject.activeSelf) continue;
                float mass = inv != null ? inv.GetAmount(kvp.Key, includeRelatedWorlds: true) : 0f;
                if (mass <= 0f) continue;
                if (!best.IsValid) { best = kvp.Key; bestMass = mass; continue; }
                int cmp = CompareByElementSortOrder(kvp.Key, best);
                if (cmp < 0 || (cmp == 0 && mass > bestMass)) {
                    best = kvp.Key;
                    bestMass = mass;
                }
            }
            if (best.IsValid) return best;

            foreach (var kvp in sel.ElementToggles) {
                if (!kvp.Value.gameObject.activeSelf) continue;
                if (!best.IsValid || CompareByElementSortOrder(kvp.Key, best) < 0) best = kvp.Key;
            }
            if (best.IsValid) return best;

            foreach (var kvp in sel.ElementToggles) return kvp.Key;
            return Tag.Invalid;
        }

        // Mirrors MaterialSelector.ElementSorter (private). Crib the comparator instead of
        // maintaining a hand-curated list: IHasSortOrder.sortOrder primary, Element.buildMenuSort
        // secondary, Element.idx tiebreak — same fields the build menu's material strip uses.
        private static int CompareByElementSortOrder(Tag at, Tag bt) {
            var prefab_a = Assets.TryGetPrefab(at);
            var ord_a = prefab_a != null ? prefab_a.GetComponent<IHasSortOrder>() : null;
            var prefab_b = Assets.TryGetPrefab(bt);
            var ord_b = prefab_b != null ? prefab_b.GetComponent<IHasSortOrder>() : null;
            if (ord_a == null || ord_b == null) return 0;
            var el_a = ElementLoader.GetElement(at);
            var el_b = ElementLoader.GetElement(bt);
            if (el_a != null && el_b != null && el_a.buildMenuSort == el_b.buildMenuSort) {
                return el_a.idx.CompareTo(el_b.idx);
            }
            return ord_a.sortOrder.CompareTo(ord_b.sortOrder);
        }

        // GetSelectedElementAsList may surface Tag.Invalid entries if a selector still has no
        // discoverable toggle (degenerate case). BuildTool.TryPlace then crashes on selected[0].
        // Substitute def.DefaultElements when the panel can't produce a usable list.
        private IList<Tag> ResolveElements(MaterialSelectionPanel msp) {
            var elements = msp.GetSelectedElementAsList;
            if (elements == null || elements.Count == 0) return def.DefaultElements();
            for (int i = 0; i < elements.Count; i++) {
                if (!elements[i].IsValid) return def.DefaultElements();
            }
            return elements;
        }

        private static bool IsCorrectToolActive(BuildingDef def) {
            var active = PlayerController.Instance?.ActiveTool;
            if (active == null) return false;
            if (def.isKAnimTile && def.isUtility) {
                return def.BuildingComplete.GetComponent<Wire>() != null
                    ? active == WireBuildTool.Instance
                    : active == UtilityBuildTool.Instance;
            }
            return active == BuildTool.Instance;
        }

        // Mirrors PlanScreen.OnRecipeElementsFullySelected's tool dispatch.
        private void ActivateBuildTool(IList<Tag> elements, string facadeID) {
            if (def.isKAnimTile && def.isUtility) {
                var tool = def.BuildingComplete.GetComponent<Wire>() != null
                    ? (BaseUtilityBuildTool)WireBuildTool.Instance
                    : (BaseUtilityBuildTool)UtilityBuildTool.Instance;
                tool?.Activate(def, elements, facadeID);
                return;
            }
            BuildTool.Instance?.Activate(def, elements, facadeID);
        }
    }
}
