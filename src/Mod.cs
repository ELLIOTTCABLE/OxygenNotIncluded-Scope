using HarmonyLib;
using KMod;
using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;

namespace ScopeMod {
    public sealed class Mod : UserMod2 {
        internal static PAction OpenAction { get; private set; }

        public override void OnLoad(Harmony harmony) {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
            new PPatchManager(harmony).RegisterPatchClass(typeof(Mod));
            OpenAction = new PActionManager().CreateAction(
                ScopeStrings.ActionKey,
                ScopeStrings.ActionTitle,
                new PKeyBinding(ScopeStrings.DefaultKey, ScopeStrings.DefaultModifier));
        }

        // RunAt.AfterLayerableLoad is the canonical mounting point for global IInputHandlers;
        // input manager isn't ready earlier
        [PLibMethod(RunAt.AfterLayerableLoad)]
        internal static void AfterLayerableLoad() {
            KInputHandler.Add(
                Global.GetInputManager().GetDefaultController(),
                new ScopeInputHandler(),
                priority: 1024);
        }
    }
}
