using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Serialization;
using HarmonyLib;
using Mono.WebBrowser;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityModManagerNet.UnityModManager;
using Exception = System.Exception;

namespace EditorPlus {
    public abstract class Tweak {
        public virtual void Init(Tweak parent, TweakAttribute attr) {
            Parent = parent;
            _children = new List<Tweak>();
            Children = new ReadOnlyCollection<Tweak>(_children);
            parent?._children.Add(this);
            Metadata = attr;
            if (Metadata == null) throw new Exception($"Tweak type {GetType()} cannot be initialized because no TweakAttribute is found.");
            Patches = new List<Type>();
            foreach (var t in GetType().GetNestedTypes()) {
                if (t.GetCustomAttribute<HarmonyPatch>() != null) Patches.Add(t);
            }

            foreach (var i in GetType().GetProperties(BindingFlags.Static)) {
                if (i.GetCustomAttribute<SyncTweakAttribute>() != null) i.SetValue(null, this);
            }
            
            foreach (var i in GetType().GetFields(BindingFlags.Static)) {
                if (i.GetCustomAttribute<SyncTweakAttribute>() != null) i.SetValue(null, this);
            }

            if (Metadata.Name != null) Harmony = new Harmony(Metadata.Name);

            if (EnableState == State.Enabled) {
                Enable();
                RunningState = State.Enabled;
            }
        }
        
        public Tweak Parent { get; private set; }
        public ReadOnlyCollection<Tweak> Children { get; private set; }
        private List<Tweak> _children { get; set; }
        
        public Harmony Harmony { get; private set; }
        public TweakAttribute Metadata { get; private set; }
        public List<Type> Patches { get; private set; }

        public State RunningState { get; private set; } = State.Disabled;
        public State EnableState { get; private set; } = State.Enabled;
        public bool IsExpanded { get; set; } = false;
        public virtual bool HasGUI => Children.Count != 0;

        public static int fontSize => (int) (TweakRunner.FontSizeMax - (TweakRunner.FontSizeMax - TweakRunner.FontSizeMin) * TweakRunner.Indent / (TweakRunner.Indent + 40));
        
        public virtual void OnGUI() {
            GUILayout.Space(-10);
            foreach (var child in Children) {
                bool changeState;
                GUILayout.BeginHorizontal();
                switch (child.EnableState) {
                    case State.Enabled:
                        changeState = GUILayout.Button(child.Metadata.CannotDisable ? "" : "<size=36><color=#22DD22>●</color></size>", TweakRunner.Enabl, GUILayout.Width(22));
                        if (changeState) {
                            child.EnableState = State.Disabled;
                            child.Enable();
                        }
                        break;
                    case State.Disabled:
                        changeState = GUILayout.Button(child.Metadata.CannotDisable ? "" : "<size=36><color=#888888>●</color></size>", TweakRunner.Enabl, GUILayout.Width(22));
                        if (changeState) {
                            child.EnableState = State.Enabled;
                            child.Enable();
                        }
                        break;
                    case State.Error:
                        GUILayout.Button("<size=36><color=#DD2222>●</color></size>", TweakRunner.Enabl, GUILayout.Width(50));
                        GUILayout.Space(-45);
                        GUILayout.BeginVertical();
                        GUILayout.Space(4);
                        GUILayout.Button("<b><size=14><color=#FFFFFF>！</color></size></b>", TweakRunner.Enabl, GUILayout.Width(45));
                        GUILayout.Space(-4);
                        GUILayout.EndVertical();
                        GUILayout.Space(-1455);
                        break;
                }

                var ctn = RDString.GetWithCheck($"editorPlus.tweakName.{child.Metadata.Name}", out bool exists);
                if (!exists) ctn = $"editorPlus.tweakName.{child.Metadata.Name}";
                GUILayout.Button($"<size={fontSize}><b>{ctn}</b></size>", TweakRunner.Enabl_Label);
                GUILayout.EndHorizontal();
                GUILayout.Space(-15);
                
                if (child.EnableState == State.Enabled && child.HasGUI) {
                    TweakRunner.BeginIndent();
                    child.OnGUI();
                    TweakRunner.EndIndent();
                }
            }
            GUILayout.Space(20);
        }

        public void Enable() {
            try {
                foreach (var t in Patches) {
                    Harmony.CreateClassProcessor(t).Patch();
                }

                foreach (var child in Children) {
                    if (child.EnableState == State.Enabled && child.RunningState == State.Disabled) {
                        child.Enable();
                    }
                }

                OnEnable();
            } catch (Exception e) {
                Main._mod.Logger.LogException(e);
                EnableState = State.Error;
                RunningState = State.Error;
            }
        }

        public void Disable() {
            try {
                foreach (var child in Children) {
                    if (child.EnableState == State.Enabled) {
                        child.Disable();
                    }
                }

                Harmony.UnpatchAll(Harmony.Id);
                OnDisable();
            } catch (Exception e) {
                Main._mod.Logger.LogException(e);
                EnableState = State.Error;
                RunningState = State.Error;
            }
        }

        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        
        public virtual void OnPatch() { }
        public virtual void OnUnpatch() { }
        public virtual void OnUpdate() { }
        public virtual void OnHideGUI() { }
    }

    [Tweak("Root")]
    public class Root : Tweak {
        public override bool HasGUI => true;
    }

    public interface ISettingTweak {
        public TweakSettings Settings { get; }
    }

    public abstract class Tweak<T> : Tweak, ISettingTweak where T : TweakSettings, new() {
        public override void OnGUI() {
            base.OnGUI();
            GUILayout.Space(-10);
            Settings.Draw();
        }

        public override void Init(Tweak parent, TweakAttribute attr) {
            Settings = ModSettings.Load<T>(Main._mod);
            Settings.Init();
            base.Init(parent, attr);
        }

        public static T Settings { get; private set; }
        TweakSettings ISettingTweak.Settings => Settings;

        public override bool HasGUI => true;
    }

    public abstract class TweakSettings : ModSettings {
        [XmlIgnore] public List<(MemberInfo, DrawAttribute)> Settings { get; private set; }

        public void Init() {
            var type = GetType();
            Settings = new List<(MemberInfo, DrawAttribute)>();
            foreach (var p in type.GetProperties()) {
                var attr = p.GetCustomAttribute<DrawAttribute>();
                if (attr == null) continue;
                Settings.Add((p, attr));
            }
            
            foreach (var p in type.GetFields()) {
                var attr = p.GetCustomAttribute<DrawAttribute>();
                if (attr == null) continue;
                Settings.Add((p, attr));
            }
        }
        public virtual void Draw() {
            GUILayout.Space(-10);
            foreach (var (info, attr) in Settings) {
                GUILayout.BeginHorizontal();
                if (info is PropertyInfo p) {
                    if (p.PropertyType == typeof(bool)) {
                        var value = (bool)p.GetValue(this);
                        var changeState = false;
                        if (value) {
                            changeState = GUILayout.Button("<size=36><color=#22DD22>●</color></size>",
                                TweakRunner.Enabl, GUILayout.Width(22));
                        }
                        else {
                            changeState = GUILayout.Button("<size=36><color=#888888>●</color></size>",
                                TweakRunner.Enabl, GUILayout.Width(22));
                        }

                        var ctn = RDString.GetWithCheck(attr.Label, out bool exists);
                        if (!exists) ctn = attr.Label;
                        GUILayout.Button($"<size={Tweak.fontSize}><b>{ctn}</b></size>", TweakRunner.Enabl_Label);
                        if (changeState) p.SetValue(this, !value);
                    }
                }
                else if (info is FieldInfo f) {
                    if (f.FieldType == typeof(bool)) {
                        var value = (bool)f.GetValue(this);
                        var changeState = false;
                        if (value) {
                            changeState = GUILayout.Button("<size=36><color=#22DD22>●</color></size>",
                                TweakRunner.Enabl, GUILayout.Width(22));
                        }
                        else {
                            changeState = GUILayout.Button("<size=36><color=#888888>●</color></size>",
                                TweakRunner.Enabl, GUILayout.Width(22));
                        }

                        var ctn = RDString.GetWithCheck(attr.Label, out bool exists);
                        if (!exists) ctn = attr.Label;
                        GUILayout.Button($"<size={Tweak.fontSize}><b>{ctn}</b></size>", TweakRunner.Enabl_Label);
                        if (changeState) f.SetValue(this, !value);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(-15);
            }
            GUILayout.Space(20);
        }
        
        public override string GetPath(ModEntry modEntry)
            => Path.Combine(modEntry.Path, GetType().FullName + ".xml");

        public override void Save(ModEntry modEntry) {
            var filepath = GetPath(modEntry);
            try {
                using (var writer = new StreamWriter(filepath)) {
                    var serializer = new XmlSerializer(GetType());
                    serializer.Serialize(writer, this);
                }
            } catch (Exception e) {
                modEntry.Logger.Error($"Can't save {filepath}.");
                modEntry.Logger.LogException(e);
            }
        }
    }

    public enum State {
        Enabled,
        Disabled,
        Error
    }

    internal static class TweakRunner {
        public static Root Root { get; private set; }
        public static List<Tweak> Tweaks = new();

        public static ModEntry Entry;

        public static int Indent;
        public const float FontSizeMax = 18f;
        public const float FontSizeMin = 10f;
        public static GUIStyle Expan;
        public static GUIStyle Enabl;
        public static GUIStyle Enabl_Label;
        public static GUIStyle Descr;
        public static bool StyleInitialized = false;

        public static void Run(ModEntry entry) {
            Entry = entry;
            Root = new Root();
            Entry.Logger.Log("Initializing Root...");
            Root.Init(null, typeof(Root).GetCustomAttribute<TweakAttribute>());
            Entry.Logger.Log("Root initalized.");
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsNested && t.GetCustomAttribute<TweakAttribute>() != null)
                .OrderBy(t => t.GetCustomAttribute<TweakAttribute>().Priority);
            
            Entry.Logger.Log("Initializing Tweaks...");
            foreach (var type in types) {
                if (type == typeof(Root)) continue;
                if (!type.IsSubclassOf(typeof(Tweak))) continue;
                RegisterTweak(type, Root);
            }
            Entry.Logger.Log("All tweaks initialized.");
        }

        private static void RegisterTweak(Type tweakType, Tweak parent) {
            Entry.Logger.Log($"Initializing tweak {tweakType}...");
            var attr = tweakType.GetCustomAttribute<TweakAttribute>();
            if (attr == null) {
                Entry.Logger.Log($"Warning: Tweak type {tweakType} has no attribute TweakAttribute");
                return;
            }

            var tweak = (Tweak) Activator.CreateInstance(tweakType);
            tweak.Init(parent, attr);

            var types = tweakType.GetNestedTypes(AccessTools.all)
                .Where(t => t.IsSubclassOf(typeof(Tweak)) && t.GetCustomAttribute<TweakAttribute>() != null)
                .OrderBy(t => t.GetCustomAttribute<TweakAttribute>().Priority).ToArray();
            foreach (var t in types) {
                if (!t.IsSubclassOf(typeof(Tweak))) continue;
                RegisterTweak(t, tweak);
            }
        }
        
        public static void OnGUI() {
            var font = GUI.skin.font;
            GUI.skin.font = RDC.data.arialFont;
            if (!StyleInitialized) {
                Expan = new GUIStyle() {
                    fixedWidth = 10,
                    normal = new GUIStyleState() { textColor = Color.white },
                    fontSize = 15,
                    margin = new RectOffset(4, 8, 5, 5),
                };
                Enabl_Label = new GUIStyle(GUI.skin.label) {
                    margin = new RectOffset(0, 2, 4, 4),
                    alignment = TextAnchor.MiddleLeft
                };
                Enabl = new GUIStyle(GUI.skin.label) {
                    margin = new RectOffset(0, 2, -8, -8),
                    alignment = TextAnchor.MiddleLeft
                };
                Descr = new GUIStyle(GUI.skin.label) {
                    fontStyle = FontStyle.Italic,
                };
                StyleInitialized = true;
            }
            
            Root.OnGUI();
        }

        public static void BeginIndent() {
            Indent += 40;
            GUILayout.BeginHorizontal();
            GUILayout.Space(40);
            GUILayout.BeginVertical();
        }

        public static void EndIndent() {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            Indent -= 40;
        }
    }

    #region Attrs

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TweakAttribute : Attribute {
        public TweakAttribute(string name) {
            Name = name;
        }
        
        public string Name { get; }
        public bool CannotDisable { get; set; }
        public int Priority { get; set; }
    }

    public class DrawAttribute : Attribute {
        public DrawAttribute(string label = null) {
            Label = label;
        }
        
        public string Label { get; private set; }
    }

    public class SyncTweakAttribute : Attribute { }
    #endregion
}