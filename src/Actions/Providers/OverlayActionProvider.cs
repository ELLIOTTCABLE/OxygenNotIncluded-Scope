using System;
using System.Collections.Generic;

namespace ScopeMod;

internal sealed class OverlayActionProvider : IActionProvider
{
   private List<CallbackAction> cached;
   private OverlayMenu cachedFor;

   public TimeSpan? PollInterval => null;

   public void OnActivate(IProviderContext ctx)
   {
      // these are the three states `OverlayToggleInfo.IsUnlocked` combines
      ctx.Subscribe((int)GameHashes.ResearchComplete, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.ToggleSandbox, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.DebugInsantBuildModeChanged, _ => ctx.MarkDirty()); // typo sic, lol
   }

   public void OnDeactivate()
   {
      cached = null;
      cachedFor = null;
   }

   public void OnPoll() { }

   public IEnumerable<IQuickAction> Enumerate()
   {
      var menu = OverlayMenu.Instance;
      if (menu == null || menu.overlayToggleInfos == null)
         yield break;

      if (cached == null || !ReferenceEquals(cachedFor, menu))
      {
         cached = BuildCache(menu);
         cachedFor = menu;
      }

      for (int i = 0; i < cached.Count; i++)
         yield return cached[i];
   }

   private static List<CallbackAction> BuildCache(OverlayMenu menu)
   {
      var list = new List<CallbackAction>(16);
      foreach (var raw in menu.overlayToggleInfos)
      {
         if (raw is not OverlayMenu.OverlayToggleInfo info)
            continue;
         if (string.IsNullOrEmpty(info.text))
            continue;

         // HashedString.ToString returns the hex hash; fine for an internal MRU
         // key — no need for a human-readable identifier here.
         var simView = info.simView;
         list.Add(
            new CallbackAction(
               displayName: info.text,
               subcategoryKey: "overlays",
               subcategoryTitle: "overlays",
               mruKey: "overlay:" + simView.ToString(),
               invoke: () =>
               {
                  // handle idempotently; avoid disable/enable churn
                  var s = OverlayScreen.Instance;
                  if (s != null && s.GetMode() != simView)
                     s.ToggleOverlay(simView);
               },
               isAvailable: info.IsUnlocked
            )
         );
      }
      return list;
   }
}
