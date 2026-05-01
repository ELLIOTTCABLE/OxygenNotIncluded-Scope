using PeterHan.PLib.Actions;

namespace ScopeMod {
    internal sealed class ScopeInputHandler : IInputHandler {
        public string handlerName => "Scope Input Handler";
        public KInputHandler inputHandler { get; set; }

        private readonly Action openAction;

        internal ScopeInputHandler() {
            openAction = Mod.OpenAction?.GetKAction() ?? PAction.MaxAction;
        }

        public void OnKeyDown(KButtonEvent e) {
            // TESTME: If the open key fires while another
            // TMP_InputField is focused (e.g. existing build-menu
            // search, save dialog), and that turns out to misbehave,
            // gate this:

            //   var es = UnityEngine.EventSystems.EventSystem.current;
            //   if (es?.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>() != null)
            //       return;

            // Empirically the game already handles defocus correctly
            // for bare hotkeys, so skip the check until evidence says
            // otherwise.
            if (e.TryConsume(openAction)) ScopeOverlay.Open();
        }
    }
}
