using System;
using System.Collections.Generic;

namespace ScopeMod;

internal sealed class PanelActionProvider : IActionProvider
{
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
      AddPanel(list, mm, "Cluster Map panel", "panel:clustermap", mm.clusterMapInfo);
      AddPanel(list, mm, "Priorities panel", "panel:priorities", mm.jobsInfo);
      AddPanel(list, mm, "Reports panel", "panel:reports", mm.reportsInfo);
      AddPanel(list, mm, "Consumables panel", "panel:consumables", mm.consumablesInfo);
      AddPanel(list, mm, "Schedule panel", "panel:schedule", mm.scheduleInfo);
      AddPanel(list, mm, "Vitals panel", "panel:vitals", mm.vitalsInfo);
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
