using ADOFAI;
using HarmonyLib;

namespace EditorPlus {
    [HarmonyPatch(typeof(RDColorPickerPopup), "UsesAlpha", MethodType.Getter)]
    public static class UsesAlphaPatch {
        public static bool Prefix(RDColorPickerPopup __instance, out bool __result) {
            __result = true;
            return false;
        }
    }
}