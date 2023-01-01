using HarmonyLib;
using UnityEngine;

namespace EditorPlus.Tweaks; 


#if DEBUG
[Tweak("Debug")]
public class DebugTweak : Tweak<DebugTweak.DebugSettings> {
    public class DebugSettings : TweakSettings {
        [Draw("Unity Editor Mode")]
        public bool unityEditor { get; set; }
        
        [Draw("ADOFAI Debug Mode")]
        public bool debug {
            get => RDC.data.debug;
            set => RDC.data.debug = value;
        }
    }

    [HarmonyPatch(typeof(Application), "get_isEditor")]
    public static class EditorPatch {
        public static bool Prefix(out bool __result) {
            __result = Settings.unityEditor;
            return false;
        }
    }
}
#endif