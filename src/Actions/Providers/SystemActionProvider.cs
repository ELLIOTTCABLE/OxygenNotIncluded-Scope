using System;
using System.Collections.Generic;
using System.IO;

namespace ScopeMod;

internal sealed class SystemActionProvider : IActionProvider
{
   public TimeSpan? PollInterval => null;

   public void OnActivate(IProviderContext ctx)
   {
      // `ActiveWorldChanged` covers save-load resets; `ToggleSandbox` /
      // `Entered`/`ExitedRedAlert` handle mid-session flips so the divided-
      // action pairs swap in-place.
      ctx.Subscribe((int)GameHashes.ActiveWorldChanged, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.ToggleSandbox, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.EnteredRedAlert, _ => ctx.MarkDirty());
      ctx.Subscribe((int)GameHashes.ExitedRedAlert, _ => ctx.MarkDirty());
   }

   public void OnDeactivate() { }

   public void OnPoll() { }

   public IEnumerable<IQuickAction> Enumerate()
   {
      yield return SaveAction;

      // sandbox, debug: divided pairs, yield the one that'd flip state.
      var game = Game.Instance;
      if (game != null)
         yield return game.SandboxModeActive ? DeactivateSandboxAction : ActivateSandboxAction;

      // (no Klei event/hash exists for debug-toggle, but invoking-closes-Scope
      // invariant means a reopen re-enumerates the correct pair?)
      yield return DebugHandler.enabled ? DeactivateDebugAction : ActivateDebugAction;

      var world = ClusterManager.Instance?.activeWorld;
      var alert = world?.AlertManager;
      if (alert != null)
         yield return alert.IsRedAlertToggledOn() ? DisableRedAlertAction : EnableRedAlertAction;
   }

   private static readonly CallbackAction SaveAction = new CallbackAction(
      displayName: "Save game",
      subcategoryKey: "system",
      subcategoryTitle: "system",
      mruKey: "system:save",
      invoke: DoDirectSave,
      isAvailable: () => SaveLoader.Instance != null && Game.Instance != null
   );

   private static readonly CallbackAction ActivateSandboxAction = new CallbackAction(
      displayName: "Activate sandbox mode",
      subcategoryKey: "system",
      subcategoryTitle: "system",
      mruKey: "system:activate-sandbox",
      invoke: () => SetSandbox(true),
      // Sandbox must be unlocked in the save's options before activation is possible.
      isAvailable: () =>
         Game.Instance != null && SaveGame.Instance != null && SaveGame.Instance.sandboxEnabled
   );

   private static readonly CallbackAction DeactivateSandboxAction = new CallbackAction(
      displayName: "Deactivate sandbox mode",
      subcategoryKey: "system",
      subcategoryTitle: "system",
      mruKey: "system:deactivate-sandbox",
      invoke: () => SetSandbox(false),
      isAvailable: () => Game.Instance != null
   );

   private static readonly CallbackAction ActivateDebugAction = new CallbackAction(
      displayName: "Activate debug mode",
      subcategoryKey: "system",
      subcategoryTitle: "system",
      mruKey: "system:activate-debug",
      invoke: () => DebugHandler.SetDebugEnabled(true)
   );

   private static readonly CallbackAction DeactivateDebugAction = new CallbackAction(
      displayName: "Deactivate debug mode",
      subcategoryKey: "system",
      subcategoryTitle: "system",
      mruKey: "system:deactivate-debug",
      invoke: () => DebugHandler.SetDebugEnabled(false)
   );

   private static readonly CallbackAction EnableRedAlertAction = new CallbackAction(
      displayName: "Enable Red Alert",
      subcategoryKey: "system",
      subcategoryTitle: "system",
      mruKey: "system:enable-red-alert",
      invoke: () => SetRedAlert(true)
   );

   private static readonly CallbackAction DisableRedAlertAction = new CallbackAction(
      displayName: "Disable Red Alert",
      subcategoryKey: "system",
      subcategoryTitle: "system",
      mruKey: "system:disable-red-alert",
      invoke: () => SetRedAlert(false)
   );

   private static void SetSandbox(bool on)
   {
      var game = Game.Instance;
      if (game == null)
         return;
      // triggers GameHashes.ToggleSandbox, refreshes PlanScreen/BuildMenu
      game.SandboxModeActive = on;
      KMonoBehaviour.PlaySound(
         GlobalAssets.GetSound(on ? "SandboxTool_Toggle_On" : "SandboxTool_Toggle_Off")
      );
   }

   private static void SetRedAlert(bool on)
   {
      var mgr = ClusterManager.Instance?.activeWorld?.AlertManager;
      if (mgr == null)
         return;
      mgr.ToggleRedAlert(on);
      KMonoBehaviour.PlaySound(GlobalAssets.GetSound(on ? "HUD_Click_Open" : "HUD_Click_Close"));
   }

   private static void DoDirectSave()
   {
      var path = SaveLoader.GetActiveSaveFilePath();
      if (string.IsNullOrEmpty(path))
      {
         PauseScreen.Instance?.Show();
         return;
      }
      try
      {
         SaveLoader.Instance.Save(path);
         Log.Info($"Saved to {Path.GetFileName(path)}");
      }
      catch (IOException ex)
      {
         Log.Error($"Save failed: {ex.Message}");
      }
   }
}
