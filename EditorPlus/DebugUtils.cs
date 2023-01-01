using System.Diagnostics;
using UnityModManagerNet;

namespace EditorPlus; 

public class DebugUtils {
    [Conditional("DEBUG")]
    public static void Log(object log) {
        #if DEBUG
        UnityModManager.Logger.Log($"{log}", "[DEBUG] ");
        #endif
    }
}