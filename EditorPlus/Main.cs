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
        internal static MainSettings Settings { get; private set; }
        internal static readonly KeyCode[] AllKeyCodes = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().ToArray();

        private static bool Load(UnityModManager.ModEntry modEntry) { 
            _mod = modEntry;
            Settings = UnityModManager.ModSettings.Load<MainSettings>(modEntry);
            _mod.OnSaveGUI = m => Settings.Save(m);
            
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
        }
    }

    [HarmonyPatch(typeof(Localization), "ExistsLocalizedString", typeof(string))]
    public static class ExistsLocalizedStringPatch {
        public static void Postfix(ref bool __result, string token) {
            var lang = Localization.CurrentLanguage;
            if (RDStringOverride.Overrides.ContainsKey(token) && RDStringOverride.Overrides[token].ContainsKey(lang)) __result = true;
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