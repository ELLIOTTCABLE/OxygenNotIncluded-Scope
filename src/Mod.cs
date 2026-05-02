using HarmonyLib;
using KMod;
using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;
using UnityEngine;

namespace ScopeMod {
    public sealed class Mod : UserMod2 {
        // Toggle for verbose info logs. Warnings and errors are unconditional.
        internal const bool LogDebug = false;

#pragma warning disable CS0162  // unreachable when LogDebug is const false
        internal static void Log(string msg) { if (LogDebug) Debug.Log(msg); }
#pragma warning restore CS0162

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
