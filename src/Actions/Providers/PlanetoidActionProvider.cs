using System;
using System.Collections.Generic;
using System.Linq;

namespace ScopeMod;

internal sealed class PlanetoidActionProvider : IActionProvider
{
   private readonly Dictionary<WorldContainer, JumpToPlanetoidAction> cached = new();

   public TimeSpan? PollInterval => null;

   public void OnActivate(IProviderContext ctx)
   {
      ctx.Subscribe((int)GameHashes.ActiveWorldChanged, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.DiscoveredWorldsChanged, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.WorldAdded, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.WorldRemoved, _ => ctx.MarkDirty());
   }

   public void OnDeactivate() => cached.Clear();

   public void OnPoll() { }

   public IEnumerable<IQuickAction> Enumerate()
   {
      var cm = ClusterManager.Instance;
      if (cm == null || cm.WorldContainers == null)
         yield break;

      // `WorldContainers` includes undiscovered planetoids, added at world-gen.
      // Spoiler-conservative default: emit only discovered.
      var worlds = cm.WorldContainers;
      if (worlds.Count(w => w != null && w.IsDiscovered) <= 1)
         yield break;

      for (int i = 0; i < worlds.Count; i++)
      {
         var world = worlds[i];
         if (world == null || !world.IsDiscovered)
            continue;
         if (!cached.TryGetValue(world, out var action))
         {
            action = new JumpToPlanetoidAction(world);
            cached[world] = action;
         }
         yield return action;
      }
   }
}
