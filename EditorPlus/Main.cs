using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GDMiniJSON;
using HarmonyLib;
using SA.GoogleDoc;
using UnityEngine;
using UnityModManagerNet;

namespace EditorPlus {
    public static class Main {
        public static Harmony Harmony { get; private set; }
        internal static UnityModManager.ModEntry _mod;
        internal static readonly KeyCode[] AllKeyCodes = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().ToArray();

        private static bool _credits = false;
        private static bool Load(UnityModManager.ModEntry modEntry) { 
            _mod = modEntry;
            
            const string resourceName = "EditorPlus.editorPlus-Logo2.png";
            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new BinaryReader(resourceStream);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(reader.ReadBytes((int)resourceStream.Length));
            new TextureScale().Bilinear(texture, texture.width * 240 / texture.height, 240);
            
            _mod.OnGUI = e => {
                GUILayout.BeginHorizontal();
                GUILayout.Space(8);
                GUILayout.BeginVertical();
                GUILayout.Space(8);
                GUILayout.Label(texture, GUILayout.Height(120));

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Space(-12);
            };
            Harmony = new Harmony(modEntry.Info.Id);
            Harmony.CreateClassProcessor(typeof(RDStringOverride)).Patch();
            Harmony.CreateClassProcessor(typeof(ExistsLocalizedStringPatch)).Patch();
            Harmony.CreateClassProcessor(typeof(TextFieldHandleEvent)).Patch();
            Harmony.CreateClassProcessor(typeof(TextFieldHandleEvent2)).Patch();
            Runner.Run(modEntry);
            _mod.OnGUI += _ => {
                if (GUILayout.Button("Credits", GUILayout.Height(18), GUILayout.Width(200))) {
                    _credits = !_credits;
                }

                if (_credits) {
                    GUILayout.Label($"<size=16><b>PatrickKR</b> {RDString.Get("editorPlus.credits.originalMod")}</size>");
                    GUILayout.Label($"<size=16><b>PERIOT</b> {RDString.Get("editorPlus.credits.develop")}</size>");
                    GUILayout.Label($"<size=16><b>C＃＃ (C# 0.1%;᲼)</b> {RDString.Get("editorPlus.credits.develop2")}</size>");
                    GUILayout.Label($"<size=16><b>Editor AlriC</b> {RDString.Get("editorPlus.credits.logo")}</size>");
                    GUILayout.Label($"<size=16><b>Gunbuster</b> {RDString.Get("editorPlus.credits.translation")}</size>");
                }
            };
            return true;
        }
    }

    [HarmonyPatch(typeof(Localization), "GetLocalizedString", typeof(string))]
    public static class RDStringOverride {
        public static Dictionary<string, Dictionary<LangCode, string>> Overrides;

        static RDStringOverride() {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "EditorPlus.overrides.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            var data1 = (Dictionary<string, object>) Json.Deserialize(reader.ReadToEnd());
            Overrides = new Dictionary<string, Dictionary<LangCode, string>>();
            foreach (var (k, v) in data1) {
                UnityModManager.Logger.Log(k);
                Overrides[k] = new Dictionary<LangCode, string>();
                foreach (var (lang, value) in (Dictionary<string, object>) v) {
                    var l = Enum.Parse<LangCode>(lang);
                    Overrides[k][l] = value as string;
                    UnityModManager.Logger.Log($"{l} {value}");
                }
            }
        }

        public static void Postfix(ref string __result, string token) {
            var lang = Localization.CurrentLanguage;
            if (Overrides.ContainsKey(token) && Overrides[token].ContainsKey(lang)) __result = Overrides[token][lang];
            else if (Overrides.ContainsKey(token) && Overrides[token].ContainsKey(LangCode.English)) __result = Overrides[token][LangCode.English];
        }
    }

    [HarmonyPatch(typeof(Localization), "ExistsLocalizedString", typeof(string))]
    public static class ExistsLocalizedStringPatch {
        public static void Postfix(ref bool __result, string token) {
            var lang = Localization.CurrentLanguage;
            if (RDStringOverride.Overrides.ContainsKey(token) && RDStringOverride.Overrides[token].ContainsKey(lang)) __result = true;
            else if (RDStringOverride.Overrides.ContainsKey(token) && RDStringOverride.Overrides[token].ContainsKey(LangCode.English)) __result = true;
        }
    }

    [HarmonyPatch(typeof(GUI), "HandleTextFieldEventForDesktop")]
    public static class TextFieldHandleEvent {
        public static bool Disable = false;

        public static bool Prefix() {
            var current = Event.current;
            return current.type is not EventType.KeyDown || !Disable;
        }
    }

    [HarmonyPatch(typeof(Event), "Use")]
    public static class TextFieldHandleEvent2 {
        public static bool Prefix() {
            var e = Event.current;
            if (e.type != EventType.MouseDown) return true;
            e.keyCode = KeyCode.A;
            return !TextFieldHandleEvent.Disable;
        }
    }
}