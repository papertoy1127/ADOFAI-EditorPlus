using System;
using System.Linq;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace EditorPlus.Tweaks;

[Tweak("unlimitedEditor",
    Priority = 0)]
public class UnlimitedEditor : Tweak<UnlimitedEditor.UnlimitedEditorSettings> {
    [SyncTweak] public static UnlimitedEditor Instance { get; set; }

    public class UnlimitedEditorSettings : TweakSettings {
        //Settings
    }


    [Tweak("firstTile")]
    public class FirstTile : Tweak {
        [HarmonyPatch(typeof(scnEditor), "OnSelectedFloorChange")]
        public static class OnSelectedFloorChangePatch {
            public static bool Prefix(scnEditor __instance) {
                if (__instance.SelectionIsSingle()) {
                    __instance.levelEventsPanel.ShowTabsForFloor(__instance.selectedFloors[0].seqID);
                    __instance.invoke("UpdateFloorDirectionButtons")(true);
                    __instance.invoke("ShowEventPicker")(true);
                    __instance.ShowEventIndicators(__instance.selectedFloors[0]);
                    return false;
                } else return true;
            }
        }

        [HarmonyPatch(typeof(scnEditor), "AddEventAtSelected")]
        public static class AddEventAtFirstFloorPatch {
            public static bool Prefix(scnEditor __instance, LevelEventType eventType) {
                if (!__instance.SelectionIsSingle()) {
                    return false;
                }

                if (eventType is LevelEventType.AddDecoration or LevelEventType.AddText) return true;

                if (__instance.get<bool>("lockPathEditing") &&
                    !__instance.get<LevelEventType[]>("whitelistedEvents")!.Contains(eventType)) {
                    return false;
                }

                int sequenceID = __instance.selectedFloors[0].seqID;

                using (new SaveStateScope(__instance, false, false, false)) {
                    var levelEvent =
                        __instance.events.Find((LevelEvent x) => x.eventType == eventType && x.floor == sequenceID);
                    bool flag = Array.Exists<LevelEventType>(EditorConstants.toggleableTypes,
                        (LevelEventType element) => element == eventType);
                    if (levelEvent == null || !Array.Exists<LevelEventType>(EditorConstants.soloTypes,
                            (LevelEventType element) => element == eventType) || flag) {
                        if (flag && levelEvent != null) {
                            __instance.RemoveEvent(levelEvent);
                            __instance.DecideInspectorTabsAtSelected();
                        } else {
                            __instance.invoke("AddEvent")(sequenceID, eventType);
                            __instance.levelEventsPanel.selectedEventType = eventType;
                            int count = __instance.events.FindAll((LevelEvent x) =>
                                x.eventType == eventType && x.floor == sequenceID).Count;
                            if (count == 1) {
                                __instance.DecideInspectorTabsAtSelected();
                                __instance.levelEventsPanel.ShowPanel(eventType, 0);
                            } else {
                                __instance.levelEventsPanel.ShowPanel(eventType, count - 1);
                            }
                        }

                        __instance.ApplyEventsToFloors();
                        __instance.ShowEventIndicators(__instance.selectedFloors[0]);
                    }
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(scnEditor), "AddEvent")]
        public static class AddEventAtFirstFloorPatch2 {
            public static bool Prefix(scnEditor __instance, int floorID, LevelEventType eventType) {
                var newLevelEvent = new LevelEvent(floorID, eventType);
                var selectedEvent = __instance.levelEventsPanel.selectedEvent;
                if (selectedEvent != null && selectedEvent.data.ContainsKey("angleOffset") &&
                    newLevelEvent.data.ContainsKey("angleOffset")) {
                    newLevelEvent["angleOffset"] = selectedEvent["angleOffset"];
                }

                __instance.events.Add(newLevelEvent);
                if (eventType == LevelEventType.SetHitsound) {
                    var list = __instance.events
                        .FindAll((LevelEvent e) => e.eventType == LevelEventType.SetHitsound)
                        .FindAll((LevelEvent e) => e.data["gameSound"] == newLevelEvent.data["gameSound"]);
                    list.Sort((LevelEvent a, LevelEvent b) => a.floor.CompareTo(b.floor));
                    var levelEvent = list.FindLast((LevelEvent e) => e.floor < floorID);
                    newLevelEvent.data["hitsoundVolume"] =
                        ((levelEvent != null) ? levelEvent.data["hitsoundVolume"] : 100);
                }

                return false;
            }
        }
    }

    [Tweak("unlimitedValues")]
    public class UnlimitedValues : Tweak {
        [HarmonyPatch(typeof(PropertyInfo), "Validate", typeof(int))]
        public static class ValidateIntPatch {
            public static bool Prefix(int value, out int __result) {
                __result = value;
                return false;
            }
        }

        [HarmonyPatch(typeof(PropertyInfo), "Validate", typeof(float))]
        public static class ValidateFloatPatch {
            public static bool Prefix(float value, out float __result) {
                __result = value;
                return false;
            }
        }

        [HarmonyPatch(typeof(PropertyInfo), "Validate", typeof(Vector2))]
        public static class ValidateVector2Patch {
            public static bool Prefix(Vector2 value, out Vector2 __result) {
                __result = value;
                return false;
            }
        }
    }
}