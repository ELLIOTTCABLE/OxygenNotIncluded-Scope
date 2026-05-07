using TMPro;

namespace ScopeMod.UI;

// Replaces PLib's PTextFieldEvents on Scope's input field.
//
// PLib at sort 99 consumes every KButtonEvent while editing, including
// Action.ZoomIn / Action.ZoomOut; which blackholes the wheel before
// CameraController sees it. Wheel intent is positional, not focus-bound, so
// we let those two actions fall through to ScopeOverlay's position-aware
// gate at sort 60.
internal sealed class ScopeInputFieldEvents : KScreen
{
   [MyCmpReq]
#pragma warning disable CS0649
   private TMP_InputField textEntry;
#pragma warning restore CS0649

   public override float GetSortKey() =>
      textEntry != null && textEntry.isFocused ? 99f : base.GetSortKey();

   // Wheel and Escape pass through to lower sort handlers:
   //   wheel  → CameraController (positional, not focus-bound).
   //   Escape → ScopeOverlay (single owner of "close scope" semantics).
   // Everything else is consumed to prevent letter keys from firing game
   // hotkeys (e.g. 'c' → cancel-tool) while typing.
   public override void OnKeyDown(KButtonEvent e)
   {
      if (textEntry == null || !textEntry.isFocused)
      {
         base.OnKeyDown(e);
         return;
      }
      if (
         e.IsAction(global::Action.ZoomIn)
         || e.IsAction(global::Action.ZoomOut)
         || e.IsAction(global::Action.Escape)
      )
         return;
      e.Consumed = true;
   }

   public override void OnKeyUp(KButtonEvent e)
   {
      if (textEntry == null || !textEntry.isFocused)
      {
         base.OnKeyUp(e);
         return;
      }
      if (
         e.IsAction(global::Action.ZoomIn)
         || e.IsAction(global::Action.ZoomOut)
         || e.IsAction(global::Action.Escape)
      )
         return;
      e.Consumed = true;
   }
}
