using System;
using System.Collections.Generic;
using SysAction = System.Action;

namespace ScopeMod;

// (Should these be docblocks? probably, but *jesus* is C# fugly.)
//
// Lifecycle (an overlay-session):
//
//     OnActivate(ctx) ->
//        [event-subs / polls fire];
//        [provider calls ctx.MarkDirty for any state-change] ->
//        Enumerate() (called by the overlay for each dirty provider) ->
//        OnDeactivate() (registered subs auto-tear-down)
//
// (I expect providers to reuse `IQuickAction` instances across calls; they're
// cached downstream by instance-identity.)
internal interface IActionProvider
{
   // NOTE: Every provider is dirty on-activation; an additional `MarkDirty`
   //       isn't necessary on-activate.
   void OnActivate(IProviderContext ctx);

   // NOTE: `ctx`-registered subscriptions tear down regardless
   void OnDeactivate();

   // Opt-in to dirtiness-polling; for state that can't be observed via events.
   // (Obviously, prefer `Subscribe` -> `MarkDirty` if possible.)
   TimeSpan? PollInterval { get; }

   // Write non-evented transition-detection here; call `MarkDirty()` if the
   // visible set might have changed.
   void OnPoll();

   // Called once after OnActivate, then again whenever the provider has
   // dirtied since the last call. Cache internally so this stays cheap.
   IEnumerable<IQuickAction> Enumerate();
}

// Bridge instance, per provider × session. Tracks subscriptions/deferrals to
// that Scope-session, tears down automatically.
internal interface IProviderContext
{
   // Klei game-hash event. `handler` runs on the main thread; handle stashed
   // and `Unsubscribe`d on overlay teardown.
   void Subscribe(int gameHash, Action<object> handler);

   // Use to teardown non-Klei-hash subscriptions
   // (e.g. `DiscoveredResources.Instance.OnDiscover -= ...`)
   void Defer(SysAction cleanup);

   // NOTE: Async and coalesced; multiple calls before the next overlay tick
   //       collapse to one re-enumerate.
   void MarkDirty();
}
