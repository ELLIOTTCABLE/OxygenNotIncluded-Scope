using System;
using System.Collections.Generic;
using ScopeMod.UI;

namespace ScopeMod;

internal sealed class PanelActionProvider : IActionProvider
{
   // companion to the always-available "Supply-closet panel" browse action.
   private static readonly CallbackAction UnlockBlueprintsAction = new CallbackAction(
      displayName: "Unlock new blueprints",
      subcategoryKey: "panels",
      subcategoryTitle: "panels",
      mruKey: "panel:unlock-blueprints",
      invoke: () => KleiItemDropScreen.Instance?.Show(),
      spriteResolver: () => OniUiTokens.KleiItemDropOnSprite,
      sortTier: SortTier.Pinned,
      aliases: new[] { "blueprints", "claim", "drops", "klei", "items" },
      rowBgResolver: () => OniUiTokens.KleiItemDropOnBg,
      rowBgHoverResolver: () => OniUiTokens.KleiItemDropOnBgHover
   );

   private List<CallbackAction> cached;
   private ManagementMenu cachedFor;

   public TimeSpan? PollInterval => null;

   public void OnActivate(IProviderContext ctx)
   {
      // Research/skills unlock with research; Starmap unlocks with telescope-built
      // (`ActiveWorldChanged`) or sandbox/instant-build.
      ctx.Subscribe((int)GameHashes.ResearchComplete, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.ActiveWorldChanged, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.ToggleSandbox, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.DebugInsantBuildModeChanged, _ => ctx.MarkDirty());

      // Klei-store inventory updates ride a separate non-Game-hash bus;
      // this is the same notification stream `KleiItemsStatusRefresher`
      // consumes for the top-left button's lit/greyed state.
      //
      // NOTE: local assignment avoids addtl. delegate instances; prevents
      //       remove from no-op'ing
      KleiItems.InventoryRefreshCallback onBlueprintArrival = ctx.MarkDirty;
      KleiItems.AddInventoryRefreshCallback(onBlueprintArrival);
      ctx.Defer(() => KleiItems.RemoveInventoryRefreshCallback(onBlueprintArrival));
   }

   public void OnDeactivate()
   {
      cached = null;
      cachedFor = null;
   }

   public void OnPoll() { }

   public IEnumerable<IQuickAction> Enumerate()
   {
      var mm = ManagementMenu.Instance;
      if (mm == null)
         yield break;

      if (cached == null || !ReferenceEquals(cachedFor, mm))
      {
         cached = BuildCache(mm);
         cachedFor = mm;
      }

      for (int i = 0; i < cached.Count; i++)
         yield return cached[i];

      // Pinned new-arrival action; only present when claimable (i.e.
      // basegame-UI-button isn't greyed out)
      if (KleiItemDropScreen.HasItemsToShow())
         yield return UnlockBlueprintsAction;
   }

   private static List<CallbackAction> BuildCache(ManagementMenu mm)
   {
      var list = new List<CallbackAction>(10);
      AddPanel(list, mm, "Research panel", "panel:research", mm.researchInfo, mm.ResearchAvailable);
      AddPanel(list, mm, "Skills panel", "panel:skills", mm.skillsInfo, mm.SkillsAvailable);
      AddPanel(list, mm, "Codex panel", "panel:codex", mm.codexInfo);
      AddPanel(
         list,
         mm,
         "Starmap panel",
         "panel:starmap",
         mm.starmapInfo,
         ManagementMenu.StarmapAvailable
      );
      AddPanel(list, mm, "Cluster map panel", "panel:clustermap", mm.clusterMapInfo);
      AddPanel(list, mm, "Priorities panel", "panel:priorities", mm.jobsInfo);
      AddPanel(list, mm, "Reports panel", "panel:reports", mm.reportsInfo);
      AddPanel(list, mm, "Consumables panel", "panel:consumables", mm.consumablesInfo);
      AddPanel(list, mm, "Schedule panel", "panel:schedule", mm.scheduleInfo);
      AddPanel(list, mm, "Vitals panel", "panel:vitals", mm.vitalsInfo);

      // Addtl. modal panels; not in ManagementMenu's ScreenInfoMatch, so they
      // bypass the AddPanel helper. Per-item providers (jump-to-diagnostic,
      // jump-to-resource) will *not* duplicate these flat ("open panel, no
      // specific target") entries.
      list.Add(
         new CallbackAction(
            displayName: "Diagnostics panel",
            subcategoryKey: "panels",
            subcategoryTitle: "panels",
            mruKey: "panel:diagnostics",
            invoke: () =>
            {
               var s = AllDiagnosticsScreen.Instance;
               if (s != null && !s.IsScreenActive())
                  s.Show(true);
            }
         )
      );

      list.Add(
         new CallbackAction(
            displayName: "Resources panel",
            subcategoryKey: "panels",
            subcategoryTitle: "panels",
            mruKey: "panel:resources",
            invoke: () =>
            {
               var s = AllResourcesScreen.Instance;
               if (s != null && !s.IsScreenActive())
                  s.Show(true);
            }
         )
      );

      // Supply closet is Klei's always-available blueprint-browser (full
      // inventory, dupes, outfits). Companion to the "Unlock new blueprints"
      // pinned entry (corresponding to `KleiItemDropScreen`).
      list.Add(
         new CallbackAction(
            displayName: "Supply-closet panel",
            subcategoryKey: "panels",
            subcategoryTitle: "panels",
            mruKey: "panel:blueprints",
            invoke: () => LockerMenuScreen.Instance?.Show(),
            aliases: new[]
            {
               "blueprints",
               "drops",
               "klei",
               "items",
               "wardrobe",
               "outfits",
               "duplicants",
            }
         )
      );

      list.Add(
         new CallbackAction(
            displayName: "Colony summary panel",
            subcategoryKey: "panels",
            subcategoryTitle: "panels",
            mruKey: "panel:colony-summary",
            invoke: () =>
            {
               var pauseScreen = PauseScreen.Instance;
               if (pauseScreen == null)
                  return;
               var data = RetireColonyUtility.GetCurrentColonyRetiredColonyData();
               MainMenu.ActivateRetiredColoniesScreenFromData(
                  pauseScreen.transform.parent.gameObject,
                  data
               );
            },
            isAvailable: () => PauseScreen.Instance != null,
            aliases: new[] { "retired", "achievements" }
         )
      );

      return list;
   }

   // Skips panels not registered in this build (DLC-gated etc.) so we don't
   // surface phantom entries the user could never invoke.
   private static void AddPanel(
      List<CallbackAction> list,
      ManagementMenu mm,
      string title,
      string mruKey,
      ManagementMenu.ManagementMenuToggleInfo info,
      Func<bool> isAvailable = null
   )
   {
      if (info == null || !mm.ScreenInfoMatch.TryGetValue(info, out var screenData))
         return;

      list.Add(
         new CallbackAction(
            displayName: title,
            subcategoryKey: "panels",
            subcategoryTitle: "panels",
            mruKey: mruKey,
            invoke: () =>
            {
               // idempotent activation; user handles exit w/ `esc`
               if (mm.activeScreen != screenData)
                  mm.ToggleScreen(screenData);
            },
            isAvailable: isAvailable
         )
      );
   }
}
