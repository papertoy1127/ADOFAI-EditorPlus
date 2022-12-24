using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using RDTools;
using UnityEngine;

namespace EditorPlus.Tweaks;

[Tweak("oldDecorations",
	PatchesType = typeof(Patches),
	SettingsType = typeof(SettingsClass),
	Priority = 10)]
public class OldDecorations : Tweak {
	[SyncSettings] public static SettingsClass Settings { get; set; }
	[SyncTweak] public static OldDecorations Instance { get; set; }


	public class SettingsClass : TweakSettings {
		public bool HideDecorationsTab = false;
	}

	public override void OnGUI() {
		Settings.HideDecorationsTab = GUILayout.Toggle(Settings.HideDecorationsTab, RDString.Get("editorPlus.oldDecorations.hideTab"));
	}
	

	public static class Patches {
		[HarmonyPatch(typeof(InspectorTab), "Init")] // 장식 설정 탭을 숨긴다
		public static class HideDecorationTab {
			public static void Postfix(InspectorTab __instance, LevelEventType type, InspectorPanel panel) {
				if (type == LevelEventType.DecorationSettings && Settings.HideDecorationsTab) __instance.gameObject.SetActive((false));
			}
		}
		
		[HarmonyPatch(typeof(scnEditor), "SetupFavoritesCategory")]
		public static class EnableDecorationFloor {
			public static bool Prefix(bool firstTime, scnEditor __instance) {
				var eventButtons =
					__instance.get<Dictionary<LevelEventCategory, List<LevelEventButton>>>("eventButtons");

				if (firstTime) {
					__instance.favoriteEvents = Persistence.GetFavoriteEditorEvents();
				} else {
					foreach (LevelEventButton levelEventButton in eventButtons[LevelEventCategory.Favorites]) {
						UnityEngine.Object.Destroy(levelEventButton.gameObject);
					}

					eventButtons[LevelEventCategory.Favorites].Clear();
				}

				int num = 0;
				foreach (LevelEventInfo levelEventInfo in GCS.levelEventsInfo.Values) {
					LevelEventType levelEventType =
						RDUtils.ParseEnum<LevelEventType>(levelEventInfo.name, LevelEventType.None);
					if (levelEventType != LevelEventType.ChangeTrack &&
					    __instance.favoriteEvents.Contains(levelEventType)) {
						GameObject gameObject =
							UnityEngine.Object.Instantiate<GameObject>(__instance.prefab_levelEventButton,
								__instance.levelEventsBarButtons);
						RectTransform component = gameObject.GetComponent<RectTransform>();
						float x = 0f + (0f + component.sizeDelta.x) * (float) (num % 11);
						component.SetAnchorPosX(x);
						LevelEventButton component2 = gameObject.GetComponent<LevelEventButton>();
						component2.Init(levelEventType, num / 11, num % 11 + 1);
						eventButtons[LevelEventCategory.Favorites].Add(component2);
						num++;
					}
				}

				if (!firstTime) {
					__instance.SetCategory(__instance.currentCategory, true);
				}

				return false;
			}
		}

		[HarmonyPatch(typeof(scnEditor), "LoadEditorProperties")]
		public static class LoadEventPatch {
			public static bool ContainsFix(IEnumerable<LevelEventType> source, LevelEventType value,
				ref bool __result) {
				if (source is not LevelEventType[] levelEventTypes) {
					return true;
				}

				var array = (levelEventTypes as ICollection<LevelEventType>);
				if (!array.Contains(LevelEventType.AddDecoration)) return true;
				if (value is LevelEventType.AddDecoration or LevelEventType.AddText) {
					__result = false;
					return false;
				}

				return true;
			}

			public static MethodInfo Contains = typeof(Enumerable).GetMethods()
				.First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
				.MakeGenericMethod(typeof(LevelEventType));

			public static MethodInfo Patch = typeof(LoadEventPatch).GetMethod("ContainsFix");

			public static void Prefix(scnEditor __instance) {
				Main.Harmony.Patch(Contains, new HarmonyMethod(Patch));
			}

			public static void Postfix(scnEditor __instance) {
				Main.Harmony.Unpatch(Contains, Patch);
			}
		}


		[HarmonyPatch(typeof(scnEditor), "AddEventAtSelected")] // 장식 추가시 타일이 아닌 레벨에 추가
		public static class AddDecorationToFloorPatch {
			public static bool Prefix(scnEditor __instance, LevelEventType eventType) {
				if (eventType is not (LevelEventType.AddDecoration or LevelEventType.AddText)) return true;
				if (!__instance.SelectionIsSingle() || __instance.get<bool>("lockPathEditing")) return false;

				using (new SaveStateScope(__instance)) {
					var floor = __instance.selectedFloors[0].seqID;
					LevelEvent item = __instance.invoke<LevelEvent>("CreateDecoration")(eventType);
					__instance.levelData.decorations.Add(item);
					__instance.set("refreshDecSprites", true);
					item.floor = floor;
					__instance.levelEventsPanel.selectedEventType = eventType;
					int count = __instance.decorations
						.FindAll((LevelEvent x) => x.eventType == eventType && x.floor == floor).Count;
					if (count == 1) {
						__instance.DecideInspectorTabsAtSelected();
						__instance.levelEventsPanel.ShowPanel(eventType, 0);
					} else {
						__instance.levelEventsPanel.ShowPanel(eventType, count - 1);
					}

					__instance.ApplyEventsToFloors();
					__instance.ShowEventIndicators(__instance.selectedFloors[0]);
					return false;
				}

			}
		}

		[HarmonyPatch(typeof(InspectorPanel), "ShowTabsForFloor")]
		public static class ShowDecTabsPatch {
			public static bool Prefix(InspectorPanel __instance, int floorID) {
				List<LevelEventType> list = new List<LevelEventType>();
				foreach (LevelEvent levelEvent in scnEditor.instance.events) {
					if (levelEvent.floor == floorID) {
						list.Add(levelEvent.eventType);
					}
				}

				foreach (LevelEvent levelEvent in scnEditor.instance.decorations) {
					if (levelEvent.floor == floorID) {
						list.Add(levelEvent.eventType);
					}
				}

				__instance.titleCanvas.SetActive(list.Count > 0);
				__instance.invoke("ModifyMessageText")("", false);
				if (list.Count == 0) {
					__instance.ShowPanel(LevelEventType.None, 0);
					__instance.invoke("ModifyMessageText")(RDString.Get("editor.dialog.noEventsOnTile", null), 0f,
						true);
					scnEditor.instance.DeselectAllDecorations();
				} else {
					LevelEventType levelEventType = LevelEventType.None;
					foreach (object obj in Enum.GetValues(typeof(LevelEventType))) {
						LevelEventType levelEventType2 = (LevelEventType) obj;
						if (levelEventType2 != LevelEventType.None) {
							foreach (LevelEventType levelEventType3 in list) {
								if (levelEventType3 == levelEventType2) {
									levelEventType = levelEventType3;
									break;
								}
							}

							if (levelEventType != LevelEventType.None) {
								break;
							}
						}
					}

					__instance.selectedEventType = LevelEventType.None;
					if (levelEventType != LevelEventType.AddDecoration && levelEventType != LevelEventType.AddText) {
						scnEditor.instance.DeselectAllDecorations();
					}

					__instance.ShowPanel(levelEventType, 0);
					__instance.ShowInspector(true, false);
				}

				List<string> list2 = new List<string>();
				foreach (LevelEventType levelEventType4 in list) {
					string item = levelEventType4.ToString();
					if (!list2.Contains(item)) {
						list2.Add(item);
					}
				}

				int num = -1;
				float num2 = 68f;
				if (list2.Count > 7) {
					num2 = 476f / (float) list2.Count;
				}

				foreach (object obj2 in __instance.tabs) {
					Transform transform = (Transform) obj2;
					bool flag = list2.Contains(transform.name);
					transform.gameObject.SetActive(flag);
					if (flag) {
						num++;
						list2.Remove(transform.name);
					}

					float y = 8f - num2 * (float) num;
					transform.GetComponent<RectTransform>().SetAnchorPosY(y);
				}

				return false;
			}
		}

		[HarmonyPatch(typeof(scnEditor), "GetFloorEvents")]
		public static class GetFloorEventsPatch {
			public static bool Prefix(scnEditor __instance, int floorID, LevelEventType eventType,
				ref List<LevelEvent> __result) {
				if (eventType is not (LevelEventType.AddDecoration or LevelEventType.AddText)) return true;
				List<LevelEvent> list = new List<LevelEvent>();
				foreach (LevelEvent levelEvent in __instance.decorations) {
					if (floorID == levelEvent.floor && levelEvent.eventType == eventType) {
						list.Add(levelEvent);
					}
				}

				__result = list;
				return false;
			}
		}

		[HarmonyPatch(typeof(InspectorPanel), "ShowPanel")]
		public static class ShowPanelPatch {

			public static bool Prefix(InspectorPanel __instance, LevelEventType eventType, int eventIndex = 0) {
				__instance.set("showingPanel", true);
				using (new SaveStateScope(scnEditor.instance, false, false, false)) {
					PropertiesPanel propertiesPanel = null;
					foreach (PropertiesPanel propertiesPanel2 in __instance.panelsList) {
						if (propertiesPanel2.levelEventType == eventType) {
							propertiesPanel2.gameObject.SetActive(true);
							__instance.titleCanvas.SetActive(true);
							propertiesPanel = propertiesPanel2;
						} else {
							propertiesPanel2.gameObject.SetActive(false);
						}
					}

					if (eventType != LevelEventType.None) {
						__instance.title.text = RDString.Get("editor." + eventType, null);
						LevelEvent levelEvent = null;
						int num = 1;
						if (eventType == LevelEventType.SongSettings) {
							levelEvent = scnEditor.instance.levelData.songSettings;
						} else if (eventType == LevelEventType.LevelSettings) {
							levelEvent = scnEditor.instance.levelData.levelSettings;
						} else if (eventType == LevelEventType.TrackSettings) {
							levelEvent = scnEditor.instance.levelData.trackSettings;
						} else if (eventType == LevelEventType.BackgroundSettings) {
							levelEvent = scnEditor.instance.levelData.backgroundSettings;
						} else if (eventType == LevelEventType.CameraSettings) {
							levelEvent = scnEditor.instance.levelData.cameraSettings;
						} else if (eventType == LevelEventType.MiscSettings) {
							levelEvent = scnEditor.instance.levelData.miscSettings;
						} else if (eventType == LevelEventType.DecorationSettings) {
							levelEvent = scnEditor.instance.levelData.decorationSettings;
						} else if (eventType is LevelEventType.AddDecoration or LevelEventType.AddText &&
						           scnEditor.instance.selectedDecorations.Count > 0) {
							if (scnEditor.instance.selectedDecorations.Count > 1) {
								eventType = LevelEventType.None;
								levelEvent = null;
								__instance.invoke("ModifyMessageText")(
									RDString.Get("editor.dialog.multipleDecorationSelected", null), 0f, true);
								__instance.titleCanvas.SetActive(false);
								__instance.ShowPanel(LevelEventType.None, 0);
							} else {
								levelEvent = scnEditor.instance.selectedDecorations[0];
								__instance.invoke("ModifyMessageText")("", false);
							}
						} else {
							List<LevelEvent> selectedFloorEvents = scnEditor.instance.GetSelectedFloorEvents(eventType);
							Debug.Log("selectedFloorEvents.Count: " + selectedFloorEvents.Count);
							num = selectedFloorEvents.Count;
							if (eventIndex > selectedFloorEvents.Count - 1) {
								RDBaseDll.printesw("undo is trying to break down, fix!! or dont");
							} else {
								levelEvent = selectedFloorEvents[eventIndex];
							}
						}

						if (propertiesPanel == null) {
							RDBaseDll.printesw("selectedPanel should not be null!! >:(");
							goto IL_320;
						}

						if (levelEvent == null) {
							goto IL_320;
						}

						__instance.selectedEvent = levelEvent;
						__instance.selectedEventType = levelEvent.eventType;
						if (__instance.selectedEventType == LevelEventType.KillPlayer) {
							__instance.invoke("ModifyMessageText")(RDString.Get("editor.dialog.usingKillPlayer", null),
								-35f,
								true);
						}

						propertiesPanel.SetProperties(levelEvent, true);
						foreach (var component in from object obj in __instance.tabs
						                          select ((RectTransform) obj).gameObject
							                          .GetComponent<InspectorTab>()) {
							if (component != null) {
								if (eventType == component.levelEventType) {
									component.SetSelected(true);
									Debug.Log("selected tab: " + component.levelEventType);
									component.eventIndex = eventIndex;
									if (component.cycleButtons != null) {
										component.cycleButtons.text.text =
											$"{eventIndex + 1}/{num}";
									}
								} else {
									component.SetSelected(false);
								}
							}
						}

						goto IL_320;
					}

					__instance.selectedEventType = LevelEventType.None;
					IL_320:
					__instance.set("showingPanel", false);
					return false;
				}
			}
		}

		[HarmonyPatch(typeof(scnEditor), "RemoveEventAtSelected")] // 장식 삭제 제대로 되도록
		public static class RemoveSelectedDecoration {
			public static void Postfix(scnEditor __instance, LevelEventType eventType) {
				if (eventType is not (LevelEventType.AddDecoration or LevelEventType.AddText)) return;

				int num = __instance.levelEventsPanel.EventNumOfTab(eventType);
				List<LevelEvent> selectedFloorEvents = __instance.GetSelectedFloorEvents(eventType);
				if (__instance.get<bool>("lockPathEditing")) return;
				if (selectedFloorEvents == null) return;
				if (num >= selectedFloorEvents.Count) return;
				using (new SaveStateScope(__instance, false, false, false)) {
					var evnt = selectedFloorEvents[num];
					__instance.levelData.decorations.Remove(evnt);
					__instance.UpdateDecorationObjects();
					__instance.set("refreshBgSprites", true);

					int num2 = selectedFloorEvents.Count - 1;
					if (num2 > 0) {
						num = Mathf.Clamp(num, 0, num2 - 1);
						__instance.levelEventsPanel.ShowPanel(eventType, num);
					} else {
						__instance.DecideInspectorTabsAtSelected();
					}

					__instance.ApplyEventsToFloors();
					__instance.ShowEventIndicators(__instance.selectedFloors[0]);
					__instance.floorButtonCanvas.transform.position = __instance.selectedFloors[0].transform.position;
				}
			}
		}

		[HarmonyPatch(typeof(scnEditor), "SelectDecoration", typeof(LevelEvent), typeof(bool), typeof(bool), typeof(bool), typeof(bool))] // 장식 선택시 타일도 선택
		public static class SelectDecorationTile {
			public static void Prefix(scnEditor __instance) {
				__instance.propertyControlList.OnItemSelected = e => {
					scnEditor.instance.SelectFloor(scnEditor.instance.floors[e.floor], false);
				};
			}
			
			public static void Postfix(PropertyControl_List __instance) {
				__instance.OnItemSelected = (Action<LevelEvent>) info.CreateDelegate(typeof(Action<LevelEvent>), scnEditor.instance);;
			}

			public static MethodInfo info = typeof(scnEditor).GetMethod("OnDecorationSelected", BindingFlags.Instance | BindingFlags.NonPublic);
		}


		[HarmonyPatch(typeof(scnEditor), "CopyOfFloor")]
		public static class CopyOfFloorWithDec {
			public static void Postfix(scnEditor __instance, scrFloor floor, bool selectedEventOnly,
				ref scnEditor.FloorData __result) {
				if (selectedEventOnly) return;
				var list = __instance.decorations.FindAll(x => x.floor == floor.seqID)
					.Select(eventToCopy => CopyEvent(eventToCopy, -1));
				__result.levelEventData.AddRange(list);

				LevelEvent CopyEvent(LevelEvent eventToCopy, int floor = -1) {
					LevelEvent levelEvent = new LevelEvent(floor, eventToCopy.eventType);
					levelEvent.data = new Dictionary<string, object>();
					levelEvent.disabled = new Dictionary<string, bool>();
					foreach (string key in eventToCopy.data.Keys) {
						object value = eventToCopy[key];
						levelEvent.data.Add(key, value);
						levelEvent.disabled.Add(key, eventToCopy.disabled[key]);
					}

					return levelEvent;
				}
			}
		}

		[HarmonyPatch(typeof(scnEditor), "PasteFloors")]
		public static class PasteDecFloors {
			public static void Postfix(scnEditor __instance) => EvntToDec(__instance);
		}

		[HarmonyPatch(typeof(scnEditor), "PasteEvents")]
		public static class PasteDecEvents {
			public static void Postfix(scnEditor __instance) => EvntToDec(__instance);
		}

		[HarmonyPatch(typeof(scrFloor), "UpdateIconSprite")]
		public static class IconPatch {
			public static void Prefix(scrFloor __instance) {
				if (scnEditor.instance == null) return;

				var decs = scnEditor.instance.decorations;
				var e = decs.FirstOrDefault(e => e.floor == __instance.seqID && e.visible);
				if (e != null) {
					if (__instance.floorIcon == FloorIcon.None) __instance.floorIcon = FloorIcon.Vfx;
					if (__instance.eventIcon is not (LevelEventType.None or LevelEventType.AddDecoration
					    or LevelEventType.AddText)) __instance.eventIcon = LevelEventType.None;
					else {
						__instance.eventIcon = decs.Any(d => d.eventType != e.eventType)
							? LevelEventType.None
							: e.eventType;
					}
				}
			}
		}

		[HarmonyPatch(typeof(scnEditor), "ShowEvent")] // 장식 보임 여부 설정시 아이콘 적용
		public static class ShowDecIconPatch {
			public static void Postfix(scnEditor __instance) {
				__instance.ApplyEventsToFloors();
			}
		}

		[HarmonyPatch(typeof(scnEditor), "CutFloor")] // 잘라내기 시 장식 제대로 잘라지게
		public static class CutDecorationPatch {
			public static bool Prefix(scnEditor __instance, scrFloor toCut, bool clearClipboard = true,
				bool selectedEventOnly = false) {
				int id = toCut.seqID;
				if (id != 0) {
					__instance.invoke("CopyFloor")(toCut, clearClipboard, true, selectedEventOnly);
					List<LevelEvent> targetEvents = new List<LevelEvent>();
					if (selectedEventOnly) {
						if (__instance.levelEventsPanel.selectedEventType != LevelEventType.None) {
							targetEvents.Add(__instance.levelEventsPanel.selectedEvent);
						}
					} else {
						targetEvents = __instance.events.FindAll((LevelEvent x) => x.floor == id);
						targetEvents.AddRange(__instance.decorations.FindAll((LevelEvent x) => x.floor == id));
					}

					foreach (LevelEvent levelEvent in targetEvents) {
						if (__instance.EventHasBackgroundSprite(levelEvent)) {
							__instance.set("refreshBgSprites", true);
						}

						if (levelEvent.eventType == LevelEventType.AddDecoration ||
						    levelEvent.eventType == LevelEventType.AddText) {
							__instance.set("refreshDecSprites", true);
						}
					}

					__instance.events.RemoveAll((LevelEvent x) => targetEvents.Contains(x));
					__instance.decorations.RemoveAll((LevelEvent x) => targetEvents.Contains(x));
					__instance.ApplyEventsToFloors();
				}

				__instance.levelEventsPanel.ShowTabsForFloor(id);
				__instance.ShowEventIndicators(toCut);

				return false;
			}
		}

		[HarmonyPatch(typeof(scnEditor), "DeleteFloor")] // 타일과 함께 장식이 지워지도록
		public static class DeleteDecWithFloorPatch {
			public static void Prefix(scnEditor __instance, int sequenceIndex) {
				__instance.SaveState();
				__instance.changingState++;
				__instance.decorations.RemoveAll((LevelEvent x) => x.floor == sequenceIndex);
			}

			public static void Postfix(scnEditor __instance) {
				__instance.changingState--;
			}
		}

		public static void EvntToDec(scnEditor editor) {
			for (int i = 0; i < editor.events.Count; i++) {
				var evnt = editor.events[i];
				if (evnt.eventType is not (LevelEventType.AddDecoration or LevelEventType.AddText)) continue;
				editor.events.RemoveAt(i);
				editor.decorations.Add(evnt);
				i--;
			}

			CustomLevel.instance.ApplyEventsToFloors(editor.floors);
		}
	}
}