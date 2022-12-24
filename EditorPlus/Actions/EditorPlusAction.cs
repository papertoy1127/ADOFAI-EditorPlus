using System;
using ADOFAI.Editor.Actions;

namespace EditorPlus.Actions; 

public abstract class EditorPlusAction<T> : EditorAction where T : Tweak {
    public override EditorTabKey sectionKey => EditorTabKey.None;
}