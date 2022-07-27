#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOFAI;
using SA.GoogleDoc;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus.Components {
    public static class FloorMeshConverter {
        public static bool Convert() {
            bool isMesh = !CustomLevel.instance.levelData.isOldLevel;
            if (isMesh) {
                var data = ToLegacy(CustomLevel.instance.levelData.angleData, out var unsucessfull);
                if (unsucessfull.HasValue) {
                    RDStringOverride.Overrides["editor.dialog.conversionSongNotFound"] = new Dictionary<LangCode, string>{
                        {LangCode.Korean, $"변환 실패\n타일 #{unsucessfull.Value.Item1}: {unsucessfull.Value.Item2}°"}, 
                        {LangCode.English, $"Conversion failed\nFloor #{unsucessfull.Value.Item1}: {unsucessfull.Value.Item2}°"}
                    };
                    scnEditor.instance.ShowPopup(true, scnEditor.PopupType.ConversionError);
                    RDStringOverride.Overrides.Remove("editor.dialog.conversionSongNotFound");
                    return false;
                } else {
                    CustomLevel.instance.levelData.pathData = data;
                    CustomLevel.instance.levelData.isOldLevel = true;
                    CustomLevel.instance.RemakePath();

                }
            } else {
                CustomLevel.instance.levelData.angleData = ToMesh(CustomLevel.instance.levelData.pathData);
                CustomLevel.instance.levelData.isOldLevel = false;
                CustomLevel.instance.RemakePath();
            }
            return true;
        }

        private static readonly Dictionary<char, short> pathAngle = new Dictionary<char, short> {
            {'R', 0},
            {'p', 15},
            {'J', 30},
            {'E', 45},
            {'T', 60},
            {'o', 75},
            {'U', 90},
            {'q', 105},
            {'G', 120},
            {'Q', 135},
            {'H', 150},
            {'W', 165},
            {'L', 180},
            {'x', 195},
            {'N', 210},
            {'Z', 225},
            {'F', 240},
            {'V', 255},
            {'D', 270},
            {'Y', 285},
            {'B', 300},
            {'C', 315},
            {'M', 330},
            {'A', 345},
            {'!', 999}
        };

        private static readonly Dictionary<short, char> anglePath = new Dictionary<short, char> {
            {0, 'R'},
            {15, 'p'},
            {30, 'J'},
            {45, 'E'},
            {60, 'T'},
            {75, 'o'},
            {90, 'U'},
            {105, 'q'},
            {120, 'G'},
            {135, 'Q'},
            {150, 'H'},
            {165, 'W'},
            {180, 'L'},
            {195, 'x'},
            {210, 'N'},
            {225, 'Z'},
            {240, 'F'},
            {255, 'V'},
            {270, 'D'},
            {285, 'Y'},
            {300, 'B'},
            {315, 'C'},
            {330, 'M'},
            {345, 'A'},
            {999, '!'}
        };

        private static readonly Dictionary<char, float> pathAngleCW = new Dictionary<char, float> {
            {'5', 108},
            {'6', 252},
            {'7', 128.57143f},
            {'8', 231.42857f}
        };

        private static readonly Dictionary<float, char> anglePathCW = new Dictionary<float, char> {
            {108, '5'},
            {252, '6'},
            {128.57143f, '7'},
            {231.42857f, '8'}
        };

        private const float delta = 0.01f;

        private static List<float> ToMesh(string data) {
            var angleData = new List<float>();
            var prevAngle = 0f;
            foreach (var path in data) {
                if (pathAngle.TryGetValue(path, out var value1)) {
                    angleData.Add(value1);
                    if (path == '!') prevAngle = (180 + prevAngle) % 360;
                    else prevAngle = value1;
                    continue;
                }

                if (pathAngleCW.TryGetValue(path, out var value2)) {
                    var angle = prevAngle + 180 - value2;
                    angleData.Add(angle);
                    prevAngle = angle;
                    continue;
                }


                throw new FormatException();
            }

            return angleData;
        }

        private static string ToLegacy(List<float> data, out (int, float)? UnsucessfullFloor) {
            var pathData = new StringBuilder();
            var prevAngle = 0f;
            var floor = 0;
            foreach (var _angle in data) {
                var angle = _angle % 360;
                if (Math.Abs(_angle - 999) < delta) angle = 999;
                while (angle < 0) {
                    angle = (360 + angle) % 360;
                }

                floor++;
                var available1 = anglePath.Where(pair => Math.Abs(pair.Key - angle) <= delta).ToArray();

                if (available1.Any()) {
                    var path = available1.First().Value;
                    pathData.Append(path);
                    if (path == '!') prevAngle = (180 + prevAngle) % 360;
                    else prevAngle = angle;
                    continue;
                }

                var diff = (540 - (angle - prevAngle)) % 360;
                var available2 = anglePathCW.Where(pair => Math.Abs(pair.Key - diff) <= delta).ToArray();
                if (available2.Any()) {
                    pathData.Append(available2.First().Value);
                    prevAngle = angle;
                    continue;
                }

                UnsucessfullFloor = (floor, angle);
                return null;
            }

            UnsucessfullFloor = null;
            return pathData.ToString();
        }
    }
}