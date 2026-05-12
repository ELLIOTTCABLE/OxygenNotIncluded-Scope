using PeterHan.PLib.Actions;
using TMPro;
using UEventSystem = UnityEngine.EventSystems.EventSystem;

namespace ScopeMod;

internal sealed class ScopeInputHandler : IInputHandler
{
   public string handlerName => "Scope Input Handler";
   public KInputHandler inputHandler { get; set; }

   private readonly Action openAction;

   internal ScopeInputHandler()
   {
      openAction = Mod.OpenAction?.GetKAction() ?? PAction.MaxAction;
   }

   public void OnKeyDown(KButtonEvent e)
   {
      // don't steal keystrokes from focused TMP inputs
      var selected = UEventSystem.current?.currentSelectedGameObject;
      if (selected != null && selected.TryGetComponent<TMP_InputField>(out _))
         return;

      // pause-menu, options, confirm-dialog open: let the event fall through
      // to KScreenManager
      if (ModalGate.IsBlockingModalOpen())
         return;

      if (e.TryConsume(openAction))
         ScopeOverlay.Open();
   }
}
