namespace ScopeMod;

// We see the open-hotkey before the screen stack can route it to a topmost
// modal; without this gate, pressing the hotkey while `PauseScreen` et al. is
// open pops Scope underneath, hidden by the modal's dimmed backdrop.
//
// Returning false from `OnKeyDown` (without `TryConsume`) lets the event fall
// through `KScreenManager` -> the topmost modal, which swallows it cleanly via
// `KModalScreen.OnKeyDown`'s unconditional `e.Consumed = true`.
internal static class ModalGate
{
   public static bool IsBlockingModalOpen()
   {
      var manager = KScreenManager.Instance;
      if (manager == null)
         return false;

      var stack = manager.screenStack;
      if (stack == null)
         return false;

      for (int i = 0; i < stack.Count; i++)
      {
         var s = stack[i];
         if (s == null || !s.IsScreenActive())
            continue;
         if (s is ScopeOverlay) // unnecessarily defensive? probably.
            continue;
         if (s.IsModal())
            return true;
      }
      return false;
   }
}
