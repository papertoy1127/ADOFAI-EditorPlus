using System;
using System.Collections.Generic;
using ADOFAI;
using EditorPlus.Types;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace EditorPlus.Tweaks;

[Tweak("miscellaneous", SettingsType = typeof(MiscellaneousSettings), Priority = int.MaxValue)]
public class Miscellaneous : Tweak {
	[SyncSettings] public static MiscellaneousSettings Settings { get; set; }
	[SyncTweak] public static Miscellaneous Instance { get; set; }

	public class MiscellaneousSettings : TweakSettings {
		//Settings
	}

	[Tweak("removeFFOnRGBA", PatchesType = typeof(RemoveFFOnRGBA))]
	public class RemoveFFOnRGBA : Miscellaneous {
		[HarmonyPatch(typeof(RDColorPickerPopup), "Hide")]
		public static class RemoveFFLastPatch {
			public static void Postfix(RDColorPickerPopup __instance) {
				var colorPC = __instance.get<PropertyControl_Color>("colorPC")!;
				var text = colorPC.text;
				if (text.Length == 8 && text.EndsWith("ff")) {
					colorPC.text = text[..6];
				}
			}
		}
	}

	[Tweak("showTileAngle", SettingsType = typeof(SettingsClass), PatchesType = typeof(ShowTileAngle))]
	public class ShowTileAngle : Miscellaneous {
		[SyncSettings] public new static SettingsClass Settings { get; set; }
		public class SettingsClass : TweakSettings {
			public bool Reflect3Planet;
			public bool RequireKeybind;
			public DefaultKeyBind KeyBind;
		}
		
		public override void OnEnable() {
			(Settings.KeyBind.RequireCtrl, Settings.KeyBind.RequireShift, Settings.KeyBind.RequireAlt, Settings.KeyBind.FinalKey) 
				= (false, false, false, KeyCode.Mouse0);
			
		}

		public override void OnGUI() {
			Settings.Reflect3Planet = GUILayout.Toggle(Settings.Reflect3Planet, RDString.Get("editorPlus.showTileAngle.Reflect3Planet"));
			Settings.RequireKeybind = GUILayout.Toggle(Settings.RequireKeybind, RDString.Get("editorPlus.showTileAngle.RequireKeybind"));
			if (Settings.RequireKeybind) {
				GUILayout.BeginHorizontal(GUILayout.Width(300));
				GUILayout.Space(20);
				Settings.KeyBind.EditGUI(null, GUILayout.Width(160));
				GUILayout.EndHorizontal();
			}
		}

		[HarmonyPatch(typeof(scnEditor), "UpdateSelectedFloor")]
		public static class ShowSelectedTile {
			public static readonly List<scrLetterPress> Texts = new();

			public static void Prefix() {
				foreach (var textsValue in Texts.ToArray()) {
					try {
						UnityEngine.Object.Destroy(textsValue.gameObject);
					} catch (Exception) {
						// ignored
					}

					Texts.Remove(textsValue);
				}

				if (Settings.RequireKeybind && !Settings.KeyBind.KeyBind.GetKey()) return;
				foreach (var scrFloor in scnEditor.instance.selectedFloors) {
					try {
						var gameObject =
							UnityEngine.Object.Instantiate(scnEditor.instance.prefab_editorNum, scrFloor.transform);
						string text;
						if (scrFloor.midSpin) {
							text = "!";
						} else {
							if (scrFloor.prevfloor == null) {
								double angle = (scrFloor.exitangle - scrFloor.entryangle) * Mathf.Rad2Deg;
								if (scrFloor.isCCW) angle = -angle;
								angle %= 360;
								if (Settings.Reflect3Planet) {
									angle -= (180.0 * (scrFloor.numPlanets - 2) / scrFloor.numPlanets);
								}
								if (angle <= 0) angle += 360;
								text = $"{Math.Round(angle, 4)}";
							} else {
								var numpl = 2;
								if (!Settings.Reflect3Planet) {
									numpl = scrFloor.numPlanets;
									scrFloor.numPlanets = 2;
								}
								
								text = $"{Math.Round(scrLevelMaker.instance.CalculateSingleFloorAngleLength(scrFloor) * Mathf.Rad2Deg, 4)}";
								
								if (!Settings.Reflect3Planet) {
									scrFloor.numPlanets = numpl;
								}
							}
						}

						var letterPress = gameObject.GetComponent<scrLetterPress>();
						var field = letterPress.letterText.gameObject.AddComponent<InputField>();
						field.textComponent = letterPress.letterText;
						field.text = text;
						gameObject.transform.eulerAngles = Vector3.zero;
						Texts.Add(letterPress);
					} catch (Exception e) {
						UnityModManager.Logger.Log($"Error: {scrFloor.seqID} / {e}");
					}
				}
			}
		}
	}
}