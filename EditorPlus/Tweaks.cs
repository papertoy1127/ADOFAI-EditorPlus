using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Serialization;
using HarmonyLib;
using UnityEngine;
using static UnityModManagerNet.UnityModManager;

namespace EditorPlus {
    #region Publics

    public class Tweak {
        static Tweak() => Tweaks = new Dictionary<Type, Tweak>();
        public static Dictionary<Type, Tweak> Tweaks { get; }
        public static ModEntry TweakEntry { get; internal set; }

        public void Log(object obj)
            => TweakEntry.Logger.Log($"{Runner.LogPrefix}{obj}");

        public void Enable() => Runner.Enable();
        public void Disable() => Runner.Disable();
        public virtual void OnPreGUI() { }
        public virtual void OnGUI() { }
        public virtual void OnPostGUI() { }
        public virtual void OnPatch() { }
        public virtual void OnUnpatch() { }
        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        public virtual void OnUpdate() { }
        public virtual void OnHideGUI() { }
        internal TweakRunner Runner { get; set; }
    }

    public enum EnableState {
        Enabled,
        Disabled,
        Error
    }
    
    public class TweakSettings : ModSettings {
        static TweakSettings() {
            string name = new object().GetHashCode().ToString();
            dynSettingsAssembly =
                AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
            dynSettingsModule = dynSettingsAssembly.DefineDynamicModule(name);
            settingTypes = new Dictionary<TweakAttribute, Type>();
        }

        public static Type GetSettingType(TweakAttribute metadata, Type tweakType, out bool isError) {
            isError = false;
            if (metadata.SettingsType != null) {
                if (metadata.SettingsType.IsSubclassOf(typeof(TweakSettings))) return metadata.SettingsType;
                isError = true;
                metadata.SettingsType = null;
            }
            if (settingTypes.TryGetValue(metadata, out Type created)) return created;
            else return settingTypes[metadata] = dynSettingsModule
                    .DefineType($"{tweakType.FullName.Replace('.', '_').Replace('+', '_')}Settings",
                        TypeAttributes.Public, typeof(TweakSettings)).CreateType();
        }

        static readonly AssemblyBuilder dynSettingsAssembly;
        static readonly ModuleBuilder dynSettingsModule;
        static readonly Dictionary<TweakAttribute, Type> settingTypes;
        public EnableState EnableState = EnableState.Enabled;
        public bool IsExpanded;

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

        public List<MemberInfo> DrawMembers;

        public void Draw() {
            
        }
    }

    public static class Runner {
        static Runner() {
            OnHarmony = new Harmony($"onHarmony{new object().GetHashCode()}");
            Runners = new List<TweakRunner>();
            RunnersDict = new Dictionary<Type, TweakRunner>();
            OT = typeof(Runner).GetMethod(nameof(Runner.OnToggle), (BindingFlags) 15420);
            OG = typeof(Runner).GetMethod(nameof(Runner.OnGUI), (BindingFlags) 15420);
            OS = typeof(Runner).GetMethod(nameof(Runner.OnSaveGUI), (BindingFlags) 15420);
            OH = typeof(Runner).GetMethod(nameof(Runner.OnHideGUI), (BindingFlags) 15420);
            OU = typeof(Runner).GetMethod(nameof(Runner.OnUpdate), (BindingFlags) 15420);
        }

        private static readonly MethodInfo OT;
        private static readonly MethodInfo OG;
        private static readonly MethodInfo OS;
        private static readonly MethodInfo OH;
        private static readonly MethodInfo OU;
        private static Harmony OnHarmony { get; }
        public static void Load(ModEntry modEntry) => Run(modEntry);

        public static void Run(ModEntry modEntry, bool preGUI = false, params Assembly[] assemblies) {
            Tweak.TweakEntry = modEntry;
            TweakTypes = new List<Type>();
            for (int i = 0; i < assemblies.Length; i++) {
                Assembly asm = assemblies[i];
                if (asm == modEntry.Assembly) continue;
                TweakTypes.AddRange(asm.GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsNested && t.GetCustomAttribute<TweakAttribute>() != null));
            }

            TweakTypes.AddRange(modEntry.Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsNested && t.GetCustomAttribute<TweakAttribute>() != null));
            if (modEntry.OnToggle == null)
                modEntry.OnToggle = (m, v) => OnToggle(v);
            else OnHarmony.Patch(modEntry.OnToggle.Method, postfix: new HarmonyMethod(OT));
            if (modEntry.OnGUI == null)
                modEntry.OnGUI = (m) => OnGUI();
            else {
                if (preGUI)
                    OnHarmony.Patch(modEntry.OnGUI.Method, new HarmonyMethod(OG));
                else OnHarmony.Patch(modEntry.OnGUI.Method, postfix: new HarmonyMethod(OG));
            }

            if (modEntry.OnHideGUI == null)
                modEntry.OnHideGUI = (m) => OnHideGUI();
            else OnHarmony.Patch(modEntry.OnHideGUI.Method, postfix: new HarmonyMethod(OH));
            if (modEntry.OnSaveGUI == null)
                modEntry.OnSaveGUI = (m) => OnSaveGUI();
            else OnHarmony.Patch(modEntry.OnSaveGUI.Method, postfix: new HarmonyMethod(OS));
            if (modEntry.OnUpdate == null)
                modEntry.OnUpdate = (m, dt) => OnUpdate();
            else OnHarmony.Patch(modEntry.OnUpdate.Method, postfix: new HarmonyMethod(OU));
        }

        private static List<Type> TweakTypes { get; set; }
        private static Dictionary<Type, TweakRunner> RunnersDict { get; }
        private static List<TweakRunner> Runners { get; }

        private static void Start() {
            foreach (Type tweakType in TweakTypes
                         .OrderBy(t => t.GetCustomAttribute<TweakAttribute>().Priority)
                         .ThenBy(t => t.GetCustomAttribute<TweakAttribute>().Name)) {
                RegisterTweakInternal(tweakType, null, false);
            }

            Runners.ForEach(runner => {
                if (runner.Settings.EnableState == EnableState.Enabled)
                    runner.Start(true);
            });
        }

        private static void Stop() {
            Runners.ForEach(runner => runner.Stop());
            Runners.Clear();
            OnSaveGUI();
        }

        private static bool OnToggle(bool value) {
            if (value)
                Start();
            else Stop();
            return true;
        }

        private static void OnHideGUI() => Runners.ForEach(runner => runner.OnHideGUI());
        private static void OnGUI() => Runners.ForEach(runner => runner.OnGUI());

        private static void OnSaveGUI()
            => SyncSettings.Save(Tweak.TweakEntry);

        private static void OnUpdate()
            => Runners.ForEach(runner => runner.OnUpdate());

        internal static void RegisterTweakInternal(Type tweakType, TweakRunner outerRunner, bool last,
            int innerTime = 0) {
            try {
                if (tweakType.BaseType != typeof(Tweak) && outerRunner == null) return;
                if (Tweak.Tweaks.Keys.Contains(tweakType)) return;
                Tweak tweak = InitTweak(tweakType, out var settings, out var attr);
                TweakRunner runner = new TweakRunner(tweak, attr, settings, last, outerRunner, innerTime);
                tweak.Runner = runner;
                if (outerRunner != null) outerRunner.InnerTweaks.Add(runner);
                else runner.Last = Tweak.Tweaks.Values.Last() == tweak;
                if (outerRunner == null)
                    Runners.Add(runner);
                var nestedTypes = tweakType.GetNestedTypes((BindingFlags) 15420).Where(t => t.IsSubclassOf(tweakType));
                if (nestedTypes.Any()) {
                    var lastType = nestedTypes.Last();
                    innerTime++;
                    foreach (Type type in nestedTypes)
                        RegisterTweakInternal(type, runner, type == lastType, innerTime);
                }

                SyncSettings.Sync(tweak.GetType(), tweak);
                SyncTweak.Sync(tweak.GetType(), tweak);
                if (runner.Metadata.PatchesType != null) {
                    SyncSettings.Sync(runner.Metadata.PatchesType, tweak);
                    SyncTweak.Sync(runner.Metadata.PatchesType, tweak);
                }
            } catch (Exception e) {
                Tweak.TweakEntry.Logger.Log($"{tweakType}\n{e}");
                throw e;
            }
        }

        internal static Tweak InitTweak(Type tweakType, out TweakSettings settings, out TweakAttribute attr) {
            ConstructorInfo constructor = tweakType.GetConstructor(new Type[] { });
            Tweak tweak = (Tweak) constructor.Invoke(null);
            attr = tweakType.GetCustomAttribute<TweakAttribute>();
            if (attr == null)
                throw new NullReferenceException("Cannot Find Tweak Metadata! (TweakAttribute)");
            Type settingType = TweakSettings.GetSettingType(attr, tweakType, out var error);
            SyncSettings.Register(Tweak.TweakEntry, tweakType, settingType);
            settings = SyncSettings.SettingsTweaktype[tweakType];
            if (error) settings.EnableState = EnableState.Error;
            Tweak.Tweaks.Add(tweakType, tweak);
            return tweak;
        }
    }

    #endregion

    #region Internals

    internal class TweakGroup {
        public List<TweakRunner> runners;

        public TweakGroup(List<TweakRunner> runners)
            => this.runners = runners;

        public void Enable(TweakRunner runner) {
            foreach (TweakRunner rnr in runners.Where(r => r != runner))
                rnr.Disable();
        }
    }

    internal class TweakRunner {
        public static Dictionary<int, Dictionary<string, TweakGroup>> Groups =
            new Dictionary<int, Dictionary<string, TweakGroup>>();

        public static GUIStyle Expan;
        public static GUIStyle Enabl;
        public static GUIStyle Enabl_Label;
        public static GUIStyle Descr;
        public static bool StyleInitialized = false;
        public Tweak Tweak { get; }
        public TweakRunner OuterTweak { get; }
        public List<TweakRunner> InnerTweaks { get; }
        public TweakAttribute Metadata { get; }
        public TweakSettings Settings { get; internal set; }
        public Harmony Harmony { get; }
        public List<TweakPatch> Patches { get; }
        public bool Inner { get; }
        public bool Last;
        public int InnerTime;
        public TweakGroup Group;

        public TweakRunner(Tweak tweak, TweakAttribute attr, TweakSettings settings, bool last, TweakRunner outerTweak,
            int innerTime) {
            Type tweakType = tweak.GetType();
            Tweak = tweak;
            Metadata = attr;
            Settings = settings;
            Harmony = new Harmony($"Tweaks.{Metadata.Name}");
            InnerTweaks = new List<TweakRunner>();
            OuterTweak = outerTweak;
            Patches = new List<TweakPatch>();
            Inner = outerTweak != null;
            InnerTime = innerTime;
            TweakGroupAttribute group = tweakType.GetCustomAttribute<TweakGroupAttribute>();
            if (group != null) {
                if (!Groups.TryGetValue(innerTime, out var groups))
                    Groups.Add(innerTime, groups = new Dictionary<string, TweakGroup>());
                if (groups.TryGetValue(group.Id, out Group))
                    Group.runners.Add(this);
                else groups.Add(group.Id, Group = new TweakGroup(new List<TweakRunner>() {this}));
            }

            Last = last;
            if (Metadata.PatchesType != null)
                AddPatches(Metadata.PatchesType, true);
            AddPatches(tweakType, false);
            if (Metadata.MustNotBeDisabled)
                Settings.EnableState = EnableState.Enabled;
        }

        public string LogPrefix {
            get {
                if (logPrefix != null) return logPrefix;
                StringBuilder sb = new StringBuilder();
                TweakRunner runner = this;
                List<string> names = new List<string>();
                while (runner.OuterTweak != null) {
                    names.Add(runner.OuterTweak.Metadata.Name);
                    runner = runner.OuterTweak;
                }

                names.Reverse();
                foreach (string name in names)
                    sb.Append($"[{name}] ");
                sb.Append($"[{Metadata.Name}] ");
                return logPrefix = sb.ToString();
            }
        }

        private string logPrefix;

        public void Start(bool force = false) {
            if (force || Settings.EnableState == EnableState.Disabled) Enable();
            InnerTweaks.ForEach(runner => {
                if (runner.Settings.EnableState == EnableState.Enabled)
                    runner.Start(true);
            });
        }

        public void Stop() {
            if (Settings.EnableState == EnableState.Enabled)
                Disable();
        }

        public void Enable() {
            Tweak.OnEnable();
            if (Metadata.PatchesType != null)
                foreach (Type type in GetNestedTypes(Metadata.PatchesType))
                    Harmony.CreateClassProcessor(type).Patch();
            foreach (Type type in Tweak.GetType().GetNestedTypes((BindingFlags) 15420))
                Harmony.CreateClassProcessor(type).Patch();
            foreach (var patch in Patches.OrderBy(tp => tp.Priority)) {
                if (patch.Prefix)
                    Harmony.Patch(patch.Target, new HarmonyMethod(patch.Patch));
                else Harmony.Patch(patch.Target, postfix: new HarmonyMethod(patch.Patch));
            }

            Tweak.OnPatch();
            Settings.EnableState = EnableState.Enabled;
            Group?.Enable(this);
        }

        public void Disable() {
            if (Metadata.MustNotBeDisabled) return;
            Tweak.OnDisable();
            Harmony.UnpatchAll(Harmony.Id);
            Tweak.OnUnpatch();
            InnerTweaks.ForEach(runner => runner.Disable());
            Settings.EnableState = EnableState.Disabled;
        }

        public static int Indent;
        public const float FontSizeMax = 18f;
        public const float FontSizeMin = 10f;

        public void OnGUI() {
            var font = GUI.skin.font;
            GUI.skin.font = RDC.data.arialFont;
            if (!StyleInitialized) {
                Expan = new GUIStyle() {
                    fixedWidth = 10,
                    normal = new GUIStyleState() {textColor = Color.white},
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

            Tweak.OnPreGUI();
            GUILayout.BeginHorizontal();
            bool canBeExpanded = Metadata.SettingsType != null;
            bool newIsExpanded = Settings.IsExpanded;
            EnableState newIsEnabled = Settings.EnableState;

            var fontSize = (int) (2 * FontSizeMax + FontSizeMin * Indent) / (Indent + 2);

            if (canBeExpanded && Settings.EnableState == EnableState.Enabled) {
                if (GUILayout.Button(Settings.IsExpanded ? "▼" : "▶", Expan)) newIsExpanded = !newIsExpanded;
            } else {
                GUILayout.Label("", Expan);
            }

            Func<bool> enableTxt;
            switch (newIsEnabled) {
                case EnableState.Enabled:
                    enableTxt = () => GUILayout.Button(Metadata.MustNotBeDisabled ? "" : "<size=36><color=#22DD22>●</color></size>", Enabl, GUILayout.Width(22));
                    break;
                case EnableState.Disabled:
                    enableTxt = () => GUILayout.Button(Metadata.MustNotBeDisabled ? "" : "<size=36><color=#888888>●</color></size>", Enabl, GUILayout.Width(22));
                    break;
                case EnableState.Error:
                    enableTxt = () => {
                        GUILayout.Label(Metadata.MustNotBeDisabled ? "" : "<size=36><color=#DD2222>●</color></size>", Enabl, GUILayout.Width(50));
                        GUILayout.Space(-45);
                        GUILayout.BeginVertical();
                        GUILayout.Space(4);
                        GUILayout.Label(Metadata.MustNotBeDisabled ? "" : "<b><size=14><color=#FFFFFF>！</color></size></b>", Enabl, GUILayout.Width(45));
                        GUILayout.Space(-4);
                        GUILayout.EndVertical();
                        GUILayout.Space(-30);
                        return false;
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            var ctn = RDString.GetWithCheck($"editorPlus.tweakName.{Metadata.Name}", out bool exists);
            if (!exists) ctn = $"editorPlus.tweakName.{Metadata.Name}";
            if (enableTxt() || GUILayout.Button($"<size={fontSize}><b>{ctn}</b></size>", Enabl_Label))
                newIsEnabled = 1 - newIsEnabled;
            var desc = RDString.GetWithCheck($"editorPlus.tweakDesc.{Metadata.Name}", out exists);
            if (exists) {
                GUILayout.Label("-");
                GUILayout.Label(desc, Descr);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal(); 
            GUILayout.Space(-5);
            
            if (!Metadata.MustNotBeDisabled && Settings.EnableState != EnableState.Error && newIsEnabled != Settings.EnableState) {
                Settings.EnableState = newIsEnabled;
                if (newIsEnabled == EnableState.Enabled) {
                    Enable();
                    newIsExpanded = true;
                } else Disable();
            }

            if (newIsExpanded != Settings.IsExpanded) {
                Settings.IsExpanded = newIsExpanded;
                if (!newIsExpanded)
                    Tweak.OnHideGUI();
            }

            if (canBeExpanded && Settings.IsExpanded && Settings.EnableState == EnableState.Enabled) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Metadata.IndentSize);
                Indent += 1;
                GUILayout.BeginVertical();
                Tweak.OnGUI();
                InnerTweaks.ForEach(runner => runner.OnGUI());
                GUILayout.EndVertical();
                Indent -= 1;
                GUILayout.EndHorizontal();
                if (!Last)
                    GUILayout.Space(32f);
            }

            GUI.skin.font = font;
            Tweak.OnPostGUI();
        }

        public void OnUpdate() {
            if (Settings.EnableState == EnableState.Enabled)
                Tweak.OnUpdate();
            InnerTweaks.ForEach(runner => runner.OnUpdate());
        }

        public void OnHideGUI() {
            if (Settings.EnableState == EnableState.Enabled)
                Tweak.OnHideGUI();
            InnerTweaks.ForEach(runner => runner.OnHideGUI());
        }

        private void AddPatches(Type patchesType, bool patchNestedTypes) {
            void AddPatches(Type t) {
                foreach (MethodInfo method in t.GetMethods((BindingFlags) 15420))
                foreach (TweakPatch patch in method.GetCustomAttributes<TweakPatch>(true)) {
                    patch.Patch = method;
                    Patches.Add(patch);
                }
            }

            if (patchNestedTypes) {
                AddPatches(patchesType);
                foreach (Type type in GetNestedTypes(patchesType))
                    AddPatches(type);
            } else AddPatches(patchesType);
        }

        public static List<Type> GetNestedTypes(Type type) {
            void GetNestedTypes(Type ty, List<Type> toContain) {
                foreach (Type t in ty.GetNestedTypes((BindingFlags) 15420)) {
                    toContain.Add(t);
                    GetNestedTypes(t, toContain);
                }
            }

            var container = new List<Type>();
            GetNestedTypes(type, container);
            return container;
        }
    }

    #endregion

    #region Attributes

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class TweakPatch : Attribute {
        public static readonly int Version = (int) AccessTools.Field(typeof(GCNS), "releaseNumber").GetValue(null);
        public bool Prefix;
        public string PatchId;
        public int Priority;
        public int MinVersion;
        public int MaxVersion;
        public MethodBase Target;
        internal MethodInfo Patch;

        public TweakPatch(Type type, string name, params Type[] parameterTypes) {
            if (parameterTypes.Length == 0) {
                try {
                    Target = type.GetMethod(name, AccessTools.all);
                } catch (AmbiguousMatchException) {
                    goto ParamMethod;
                }

                return;
            }

            ParamMethod:
            Target = type.GetMethod(name, AccessTools.all, null, parameterTypes, null);
        }

        public bool IsValid =>
            (MinVersion == -1 || Version >= MinVersion) && (MaxVersion == -1 || Version <= MaxVersion);
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TweakAttribute : Attribute {
        public TweakAttribute(string name) {
            Name = name;
            IndentSize = 40f;
        }
        
        public string Name;
        public Type PatchesType;
        public Type SettingsType;
        public bool MustNotBeDisabled;
        public int Priority;
        public float IndentSize;
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TweakGroupAttribute : Attribute {
        public string Id;
        public TweakGroupAttribute() => Id = "Default";
    }

    public class SyncSettings : Attribute {
        public static Dictionary<Type, TweakSettings> SettingsTweaktype = new Dictionary<Type, TweakSettings>();
        public static Dictionary<Type, TweakSettings> SettingsSettingType = new Dictionary<Type, TweakSettings>();

        static readonly MethodInfo load = typeof(ModSettings).GetMethod(nameof(ModSettings.Load), (BindingFlags) 15420,
            null, new Type[] {typeof(ModEntry)}, null);

        public static void Register(ModEntry modEntry, Type tweakType, Type settingsType) {
            try {
                Debug.Log($"Setting type {settingsType}");
                SettingsTweaktype[tweakType] = (TweakSettings) load.MakeGenericMethod(settingsType).Invoke(null, new object[] {modEntry});
            } catch {
                SettingsTweaktype[tweakType] = (TweakSettings) Activator.CreateInstance(settingsType);
            }
            
            SettingsSettingType[settingsType] = SettingsTweaktype[tweakType];
        }

        public static void Sync(Type type, object instance = null) {
            foreach (var field in type.GetFields((BindingFlags) 15420)) {
                SyncSettings sync = field.GetCustomAttribute<SyncSettings>();
                if (sync != null)
                    if (field.IsStatic)
                        field.SetValue(null, SettingsSettingType.GetValueOrDefault(field.FieldType));
                    else field.SetValue(instance, SettingsSettingType.GetValueOrDefault(field.FieldType));
            }

            foreach (var prop in type.GetProperties((BindingFlags) 15420)) {
                SyncSettings sync = prop.GetCustomAttribute<SyncSettings>();
                if (sync != null)
                    if (prop.GetGetMethod(true).IsStatic)
                        prop.SetValue(null, SettingsSettingType.GetValueOrDefault(prop.PropertyType));
                    else prop.SetValue(instance, SettingsSettingType.GetValueOrDefault(prop.PropertyType));
            }
        }

        public static void Save(ModEntry modEntry) {
            foreach (var setting in SettingsTweaktype.Values)
                setting.Save(modEntry);
        }
    }

    public class SyncTweak : Attribute {
        public static void Sync(Type type, object instance = null) {
            foreach (var field in type.GetFields((BindingFlags) 15420)) {
                SyncTweak sync = field.GetCustomAttribute<SyncTweak>();
                if (sync != null)
                    if (field.IsStatic)
                        field.SetValue(null, Tweak.Tweaks[field.FieldType]);
                    else field.SetValue(instance, Tweak.Tweaks[field.FieldType]);
            }

            foreach (var prop in type.GetProperties((BindingFlags) 15420)) {
                SyncTweak sync = prop.GetCustomAttribute<SyncTweak>();
                if (sync != null)
                    if (prop.GetGetMethod(true).IsStatic)
                        prop.SetValue(null, Tweak.Tweaks[prop.PropertyType]);
                    else prop.SetValue(instance, Tweak.Tweaks[prop.PropertyType]);
            }
        }
    }

    #endregion
}