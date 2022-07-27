using System.Collections.Generic;
using SA.GoogleDoc;
using UnityModManagerNet;

namespace EditorPlus {
    public class MainSettings : UnityModManager.ModSettings, IDrawable {
        public override void Save(UnityModManager.ModEntry modEntry) { }

        public void OnChange() { }
    }
}