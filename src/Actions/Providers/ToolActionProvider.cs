using System;
using System.Collections.Generic;

namespace ScopeMod;

// Tool list is fixed at game-load; per-tool availability flips with sandbox
// mode.
internal sealed class ToolActionProvider : IActionProvider
{
   private List<CallbackAction> cached;
   private ToolMenu cachedFor;

   public TimeSpan? PollInterval => null;

   public void OnActivate(IProviderContext ctx)
   {
      ctx.Subscribe((int)GameHashes.ToggleSandbox, _ => ctx.MarkDirty());
   }

   public void OnDeactivate()
   {
      cached = null;
      cachedFor = null;
   }

   public void OnPoll() { }

   public IEnumerable<IQuickAction> Enumerate()
   {
      var menu = ToolMenu.Instance;
      if (menu == null)
         yield break;

      if (cached == null || !ReferenceEquals(cachedFor, menu))
      {
         cached = BuildCache(menu);
         cachedFor = menu;
      }

      for (int i = 0; i < cached.Count; i++)
         yield return cached[i];
   }

   private static List<CallbackAction> BuildCache(ToolMenu menu)
   {
      var list = new List<CallbackAction>(32);
      foreach (var info in EnumerateTools(menu.basicTools))
         list.Add(MakeTool(info, isSandbox: false));
      foreach (var info in EnumerateTools(menu.sandboxTools))
         list.Add(MakeTool(info, isSandbox: true, isAvailable: SandboxActive));
      return list;
   }

   private static bool SandboxActive() => Game.Instance != null && Game.Instance.SandboxModeActive;

   private static IEnumerable<ToolMenu.ToolInfo> EnumerateTools(
      List<ToolMenu.ToolCollection> collections
   )
   {
      if (collections == null)
         yield break;
      for (int i = 0; i < collections.Count; i++)
      {
         var coll = collections[i];
         if (coll == null || coll.tools == null)
            continue;
         for (int j = 0; j < coll.tools.Count; j++)
         {
            var info = coll.tools[j];
            if (info == null || string.IsNullOrEmpty(info.text))
               continue;
            yield return info;
         }
      }
   }

   private static CallbackAction MakeTool(
      ToolMenu.ToolInfo info,
      bool isSandbox,
      Func<bool> isAvailable = null
   )
   {
      string idPart = !string.IsNullOrEmpty(info.toolName) ? info.toolName : info.text;
      // ChooseTool over PlayerController.ActivateTool: keeps toolbar selection
      // visuals in sync with the keyboard-driven selection.
      return new CallbackAction(
         displayName: info.text,
         subcategoryKey: isSandbox ? "tools-sandbox" : "tools",
         subcategoryTitle: isSandbox ? "sandbox tools" : "tools",
         mruKey: "tool:" + idPart,
         invoke: () =>
         {
            var m = ToolMenu.Instance;
            if (m != null)
               m.ChooseTool(info);
         },
         isAvailable: isAvailable
      );
   }
}
