using System;
using System.Collections.Generic;

namespace ScopeMod;

internal sealed class DuplicantActionProvider : IActionProvider
{
   // Per-dupe, identity-stable action cache. Survives across `MarkDirty`;
   // re-`Enumerate`s so downstream identity-keyed caches (e.g.
   // `ScopeSearch.scoreCache`) don't thrash when a single dupe is added or
   // removed. Stale entries (for departed dupes) hang around until
   // `OnDeactivate`.
   private readonly Dictionary<MinionIdentity, JumpToDuplicantAction> cached = new();

   public TimeSpan? PollInterval => null;

   public void OnActivate(IProviderContext ctx)
   {
      if (Components.LiveMinionIdentities is not { } live)
         return;

      Action<MinionIdentity> onChange = _ => ctx.MarkDirty();
      live.OnAdd += onChange;
      live.OnRemove += onChange;
      ctx.Defer(() =>
      {
         if (Components.LiveMinionIdentities is { } l)
         {
            l.OnAdd -= onChange;
            l.OnRemove -= onChange;
         }
      });
   }

   public void OnDeactivate() => cached.Clear();

   public void OnPoll() { }

   public IEnumerable<IQuickAction> Enumerate()
   {
      if (Components.LiveMinionIdentities is not { } list)
         yield break;
      for (int i = 0; i < list.Count; i++)
      {
         var minion = list[i];
         if (minion == null)
            continue;
         if (!cached.TryGetValue(minion, out var action))
         {
            action = new JumpToDuplicantAction(minion);
            cached[minion] = action;
         }
         yield return action;
      }
   }
}
