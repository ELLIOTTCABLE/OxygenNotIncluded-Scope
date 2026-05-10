using System;
using System.Collections.Generic;
using SysAction = System.Action;

namespace ScopeMod;

// Instantiated per provider × overlay-open-session. Owns the provider's
// session-scoped subscriptions, dirty bit, and next-poll timestamp.
internal sealed class ProviderSession(IActionProvider provider) : IProviderContext
{
   public IActionProvider Provider { get; } = provider;

   // Last `Enumerate` result; reused by `ScopeOverlay` during list-cons
   public readonly List<IQuickAction> CachedActions = new(32);

   public bool Dirty;

   // unscaled-time at which `OnPoll` should fire next (+Inf -> no polling)
   public float NextPollAt;

   private readonly List<int> hashHandles = new(4);
   private readonly List<SysAction> cleanups = new(4);

   public void BeginSession(float now)
   {
      Dirty = true;
      var interval = Provider.PollInterval;
      NextPollAt = interval.HasValue
         ? now + (float)interval.Value.TotalSeconds
         : float.PositiveInfinity;
      try
      {
         Provider.OnActivate(this);
      }
      catch (Exception ex)
      {
         Log.Error($"Provider {Provider.GetType().Name}.OnActivate threw: {ex}");
      }
   }

   public void EndSession()
   {
      try
      {
         Provider.OnDeactivate();
      }
      catch (Exception ex)
      {
         Log.Error($"Provider {Provider.GetType().Name}.OnDeactivate threw: {ex}");
      }

      // using reverse order to matche `IDisposable` composition convention
      for (int i = hashHandles.Count - 1; i >= 0; i--)
      {
         try
         {
            Game.Instance?.Unsubscribe(hashHandles[i]);
         }
         catch (Exception ex)
         {
            Log.Warn($"Unsubscribe failed for {Provider.GetType().Name}: {ex.Message}");
         }
      }
      hashHandles.Clear();

      for (int i = cleanups.Count - 1; i >= 0; i--)
      {
         try
         {
            cleanups[i]();
         }
         catch (Exception ex)
         {
            Log.Warn($"Deferred cleanup failed for {Provider.GetType().Name}: {ex.Message}");
         }
      }
      cleanups.Clear();
      CachedActions.Clear();
   }

   public bool TickPoll(float now)
   {
      if (now < NextPollAt)
         return false;
      if (Provider.PollInterval is not { } interval)
      {
         NextPollAt = float.PositiveInfinity;
         return false;
      }
      NextPollAt = now + (float)interval.TotalSeconds;
      try
      {
         Provider.OnPoll();
      }
      catch (Exception ex)
      {
         Log.Error($"Provider {Provider.GetType().Name}.OnPoll threw: {ex}");
      }
      return true;
   }

   public void RebuildCache()
   {
      Dirty = false;
      CachedActions.Clear();
      try
      {
         foreach (var a in Provider.Enumerate())
         {
            if (a != null)
               CachedActions.Add(a);
         }
      }
      catch (Exception ex)
      {
         Log.Error($"Provider {Provider.GetType().Name}.Enumerate threw: {ex}");
      }
   }

   void IProviderContext.Subscribe(int gameHash, Action<object> handler)
   {
      var game = Game.Instance;
      if (game == null)
      {
         Log.Warn(
            $"{Provider.GetType().Name}: Subscribe({gameHash}) skipped — Game.Instance is null."
         );
         return;
      }
      hashHandles.Add(game.Subscribe(gameHash, handler));
   }

   void IProviderContext.Defer(SysAction cleanup)
   {
      if (cleanup != null)
         cleanups.Add(cleanup);
   }

   void IProviderContext.MarkDirty() => Dirty = true;
}
