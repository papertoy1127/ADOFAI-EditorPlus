using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Rewired.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus.Types;

public struct DefaultKeyBind {
    public bool RequireShift;
    public bool RequireCtrl;
    public bool RequireAlt;

    public KeyCode FinalKey;

    public KeyBind KeyBind;

    public void Reset() {
        KeyBind.RequireShift = RequireShift;
        KeyBind.RequireCtrl = RequireCtrl;
        KeyBind.RequireAlt = RequireAlt;
        KeyBind.FinalKey = FinalKey;
    }

    public DefaultKeyBind(bool requireShift, bool requireCtrl, bool requireAlt, KeyCode finalKey) {
        RequireShift = requireShift;
        RequireCtrl = requireCtrl;
        RequireAlt = requireAlt;
        FinalKey = finalKey;

        KeyBind = new KeyBind(requireShift, requireCtrl, requireAlt, finalKey);
    }

    public void EditGUI(string label, KeyBind.DoTextField textField) {
        KeyBind.EditGUI(label, textField, false);
        GUILayout.Space(15);
        if (GUILayout.Button(RDString.Get("editorPlus.GUI.Reset"))) {
            Reset();
        }

        GUILayout.EndHorizontal();
    }

    public void EditGUI(string label, params GUILayoutOption[] options) {
        EditGUI(label, GUI.skin.textField, options);
    }

    public void EditGUI(string label, GUIStyle style, params GUILayoutOption[] options) {
        EditGUI(label, (s1, s2) => {
            GUI.SetNextControlName(s1);
            GUILayout.TextField(s2, style, options);
        });
    }
}

public struct KeyBind {
    public bool RequireShift;
    public bool RequireCtrl;
    public bool RequireAlt;
    
    public KeyCode FinalKey;

    public bool GetKeyDown() {
        return RequireCtrl      == (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
               RequireShift     == (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
               RequireAlt       == (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) &&
               (FinalKey == KeyCode.None || Input.GetKey(FinalKey));
    }

    public bool GetKey() {
        return RequireCtrl      == (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
               RequireShift     == (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
               RequireAlt       == (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) &&
               (FinalKey == KeyCode.None || Input.GetKey(FinalKey));
    }

    public override string ToString() {
        var b = new StringBuilder();
        if (RequireCtrl) b.Append("Ctrl + ");
        if (RequireShift) b.Append("Shift + ");
        if (RequireAlt) b.Append("Alt + ");
        b.Append(FinalKey.ToString());

        return b.ToString();
    }

    private int lastFrame;
    private int _keyBindCount_member;
    private int _focusDelay;

    public delegate void DoTextField(string controlName, string text);

    public void EditGUI(string label, DoTextField textField, bool endHorizontal = true) {
        if (Time.frameCount != lastFrame) {
            lastFrame = Time.frameCount;
            _keyBindCount_member = 0;
        } else {
            _keyBindCount_member += 1;
        }
        var id = _keyBindCount;
        var controlName = $"EH:KeyBind:{id}:{_keyBindCount_member}";
        _keyBindCount++;
        
        if (label != null) {
            GUILayout.Label(label);
        }
        GUILayout.BeginHorizontal();
        TextFieldHandleEvent.Disable = true;
        textField(controlName, ToString());
        TextFieldHandleEvent.Disable = false;
        bool focus = GUI.GetNameOfFocusedControl() == controlName;
        if (focus) {
            var e = Event.current;
            switch (e.type) {
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) {
                        GUI.FocusControl(null);
                        break;
                    }
                    if (e.keyCode is not
                        (KeyCode.LeftControl or KeyCode.RightControl or
                        KeyCode.LeftShift or KeyCode.RightShift or
                        KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.None)) FinalKey = e.keyCode;

                    RequireCtrl = e.control;
                    RequireShift = e.shift;
                    RequireAlt = e.alt;

                    e.Use();
                    break;
                case EventType.KeyUp:
                    e.Use();
                    break;
                case EventType.MouseDown:
                    if (e.keyCode != KeyCode.A || _focusDelay > 0) {
                        e.Use();
                        break;
                    }
                    switch (e.button) {
                        case 0:
                            FinalKey = KeyCode.Mouse0;
                            break;
                        case 1:
                            FinalKey = KeyCode.Mouse1;
                            break;
                        case 2:
                            FinalKey = KeyCode.Mouse2;
                            break;
                        case 3:
                            FinalKey = KeyCode.Mouse3;
                            break;
                        case 4:
                            FinalKey = KeyCode.Mouse4;
                            break;
                        case 5:
                            FinalKey = KeyCode.Mouse5;
                            break;
                        case 6:
                            FinalKey = KeyCode.Mouse6;
                            break;
                    }

                    RequireCtrl = e.control;
                    RequireShift = e.shift;
                    RequireAlt = e.alt;

                    e.Use();
                    break;
            }
        } else {
            _focusDelay = 10;
        }
        if (endHorizontal) GUILayout.EndHorizontal();
        if (_focusDelay > 0) _focusDelay--;
        /*
            foreach (var keyCode in Main.AllKeyCodes) {
                if (!Input.GetKeyDown(keyCode)) continue;
                RequireCtrl = false;
                RequireShift = false;
                RequireAlt = false;
                RequireBackQuote = false;
                FinalKey = keyCode;
                break;
            }
            
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) RequireCtrl = true;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) RequireShift = true;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) RequireAlt = true;
            if (Input.GetKey(KeyCode.BackQuote)) RequireBackQuote = true;
         */
    }

    private static int _keyBindCount = 0;
    public KeyBind(bool requireShift, bool requireCtrl, bool requireAlt, KeyCode finalKey) {
        RequireShift = requireShift;
        RequireCtrl = requireCtrl;
        RequireAlt = requireAlt;
        FinalKey = finalKey;
        
        lastFrame = 0;
        _keyBindCount_member = 0;
        _focusDelay = 0;
    }
}