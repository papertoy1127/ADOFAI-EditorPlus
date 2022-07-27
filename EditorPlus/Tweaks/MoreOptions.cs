using System.Collections.Generic;
using System.Linq;
using ADOFAI;
using EditorPlus.Components;
using GDMiniJSON;
using HarmonyLib;

namespace EditorPlus.Tweaks;

[Tweak("moreOptions",
	SettingsType = typeof(MoreOptionsSettings),
	Priority = 40)]
public class MoreOptions : Tweak {
	[SyncSettings] public static MoreOptionsSettings Settings { get; set; }
	[SyncTweak] public static MoreOptions Instance { get; set; }

	public class MoreOptionsSettings : TweakSettings {
		//Settings
	}


	[Tweak("legacyTile", PatchesType = typeof(Patches))]
	public class LegacyTile : MoreOptions {
		[HarmonyPatch(typeof(PropertyControl_Toggle), "SelectVar")]
		public static class PropertySetPatch {
			public static void Prefix(PropertyControl_Toggle __instance, ref string var) {
				var selectedEvent = __instance.propertiesPanel.inspectorPanel.selectedEvent;
				if (__instance.propertyInfo?.name == "EH:useLegacyFloors") {
					var selected = CustomLevel.instance.levelData.isOldLevel ? "Enabled" : "Disabled";
					if (selected != var) FloorMeshConverter.Convert();
					var = CustomLevel.instance.levelData.isOldLevel ? "Enabled" : "Disabled";
				}
			}
		}

		[HarmonyPatch(typeof(scnEditor), "Awake")]
		public static class MoreEditorSettingsInit {
			public static void Prefix() {
				if (GCS.settingsInfo["MiscSettings"].propertiesInfo.ContainsKey("EH:useLegacyFloors"))
					GCS.settingsInfo["MiscSettings"].propertiesInfo.Remove("EH:useLegacyFloors");

				GCS.settingsInfo["MiscSettings"].propertiesInfo.Add("EH:useLegacyFloors",
					new PropertyInfo(new Dictionary<string, object> {
						{"name", "EH:useLegacyFloors"},
						{"type", "Enum:ToggleBool"},
						{"default", "Disabled"}
					}, GCS.settingsInfo["MiscSettings"]));
			}
		}

		[HarmonyPatch(typeof(LevelEvent), "Encode")]
		public static class EncodePatch {
			private static Dictionary<string, object> _data = null!;

			public static void Prefix(LevelEvent __instance) {
				_data = new Dictionary<string, object>();
				foreach (var (key, value) in __instance.data) {
					_data[key] = value;
				}

				foreach (var key in __instance.data.Keys.Where(s => s.StartsWith("EH:")).ToArray()) {
					__instance.data.Remove(key);
				}
			}

			public static void Postfix(LevelEvent __instance) {
				__instance.data = _data;
			}
		}

		[HarmonyPatch(typeof(LevelData), "Decode")]
		public static class DecodePatch {
			public static void Postfix(LevelData __instance) {
				__instance.miscSettings.data["EH:useLegacyFloors"] = __instance.isOldLevel ? "Enabled" : "Disabled";
			}
		}
	}

	[Tweak("moreDecorationOptions", PatchesType = typeof(MoreDecorationOptions))]
	public class MoreDecorationOptions : MoreOptions {
		public static Dictionary<string, PropertyInfo> Info_orig;
		public static Dictionary<string, PropertyInfo> Info_new;

		[HarmonyPatch(typeof(scnEditor), "Awake")]
		public static class BeforeEditorPatch {
			public static void Prefix() {

				if (Info_orig == null) Info_orig = GCS.levelEventsInfo["AddDecoration"].propertiesInfo;
				if (Info_new == null) {
					Info_new = new Dictionary<string, PropertyInfo>(Info_orig.Count + 4);
					foreach (var (key, info) in Info_orig) {
						Info_new.Add(key, info);
					}
					Info_new.Add("EH:lockToCameraRot",
						new PropertyInfo(new Dictionary<string, object> {
							{"name", "EH:lockToCameraRot"},
							{"type", "Enum:ToggleBool"},
							{"default", "Disabled"}
						}, GCS.levelEventsInfo["AddDecoration"]));

					Info_new.Add("EH:lockToCameraScale",
						new PropertyInfo(new Dictionary<string, object> {
							{"name", "EH:lockToCameraScale"},
							{"type", "Enum:ToggleBool"},
							{"default", "Disabled"}
						}, GCS.levelEventsInfo["AddDecoration"]));

				}

				GCS.levelEventsInfo["AddDecoration"].propertiesInfo = Info_new;
			}
		}

		[HarmonyPatch(typeof(PropertyControl_Toggle), "SelectVar")]
		public static class DecPropertyPatch {
			public static void Prefix(PropertyControl_Toggle __instance, ref string var) {
				var selectedEvent = __instance.propertiesPanel.inspectorPanel.selectedEvent;
				if (selectedEvent?.eventType != LevelEventType.AddDecoration) return;
				Dictionary<string, object> components = null;

				if (selectedEvent.data.TryGetValue("components", out var comp)) {
					try {
						components = (Dictionary<string, object>) Json.Deserialize($"{{{comp}}}");
					} catch { }
				}

				components ??= new Dictionary<string, object>();



				bool lockRot = false, lockScale = false;
				if (components.TryGetValue("scrLockToCamera", out object a)) {
					var data = (Dictionary<string, object>) a;
					lockRot = (bool) data.GetValueOrDefault("lockRot", false);
					lockScale = (bool) data.GetValueOrDefault("lockScale", false);
				}

				switch (__instance.propertyInfo?.name) {
					case "EH:lockToCameraRot":
						lockRot = var == "Enabled";
						break;
					case "EH:lockToCameraScale":
						lockScale = var == "Enabled";
						break;
					default:
						return;
				}

				if (lockRot || lockScale) {
					components["scrLockToCamera"] = new Dictionary<string, object>() {
						{"lockRot", lockRot},
						{"lockScale", lockScale}
					};
				} else {
					components.Remove("scrLockToCamera");
				}

				var compstr = Json.Serialize(components).Replace("\n", "");
				selectedEvent.data["components"] = compstr.Substring(1, compstr.Length - 2);
				scnEditor.instance.set("refreshDecSprites", true);
			}
		}

		[HarmonyPatch(typeof(LevelData), "Decode")]
		public static class DecodePatch {
			public static void Postfix(LevelData __instance) {
				foreach (var decoration in __instance.decorations) {
					if (!decoration.data.TryGetValue("components", out var comp)) return;
					Dictionary<string, object> components = null;
					try {
						components = (Dictionary<string, object>) Json.Deserialize($"{{{comp}}}");
					} catch {
						return;
					}

					if (components.TryGetValue("scrLockToCamera", out object a)) {
						var data = (Dictionary<string, object>) a;
						decoration.data["EH:lockToCameraRot"] =
							(bool) data.GetValueOrDefault("lockRot", false) == true ? "Enabled" : "Disabled";
						decoration.data["EH:lockToCameraScale"] =
							(bool) data.GetValueOrDefault("lockScale", false) == true ? "Enabled" : "Disabled";
					}
				}
			}
		}

		public override void OnDisable() {
			if (Info_orig != null) GCS.levelEventsInfo["AddDecoration"].propertiesInfo = Info_orig;
		}
	}
}