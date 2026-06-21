/*
Copyright 2017, Jeiel Aranal

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;

using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;


#if UNITY_EDITOR
namespace realvirtual
{
    [InitializeOnLoad]
    public static class QuickToggle
    {
        #region Extension Hooks

        /// <summary>
        /// Delegate type for professional package to draw additional hierarchy icons.
        /// </summary>
        /// <param name="instanceId">The instance ID of the hierarchy item</param>
        /// <param name="selectionRect">The selection rect for drawing</param>
        /// <param name="target">The target GameObject</param>
        /// <param name="behaviors">Array of realvirtualBehavior components on the target</param>
        /// <param name="curpos">Current position offset (ref to allow modification)</param>
        public delegate void DrawProfessionalIconsDelegate(int instanceId, Rect selectionRect, GameObject target, realvirtualBehavior[] behaviors, ref float curpos);

        /// <summary>
        /// Hook for professional package to draw additional hierarchy icons (e.g., CAD status).
        /// </summary>
        public static DrawProfessionalIconsDelegate OnDrawProfessionalIcons;

        #endregion

        #region LogicStep Progress Bar

        /// <summary>
        /// Gets LogicStep from a GameObject if exactly one exists and target is active.
        /// </summary>
        private static LogicStep GetLogicStep(GameObject target)
        {
            LogicStep[] steps = target.GetComponents<LogicStep>();
            if (target.activeSelf == false)  // do nothing if target is not active
                return null;
            if (steps.Length == 1) // only display something if there is only one step
                return steps[0];
            return null;
        }

        /// <summary>
        /// Draws LogicStep progress bars in the hierarchy window.
        /// </summary>
        private static void DrawLogicStepProgress(Rect selectionRect, GameObject target)
        {
            if (realvirtualcontroller == null || !realvirtualcontroller.ShowComponents)
                return;

            LogicStep logicStep = GetLogicStep(target);
            if (logicStep == null)
                return;

            float progress = Mathf.Clamp(logicStep.State, 0, 100);
            if (progress > 0)
            {
                var rect = selectionRect;
                rect.x += 18;
                float barWidth = rect.width * (progress / 100f);
                // limit width to be greater 0
                barWidth = Mathf.Max(barWidth, 20);
                float barHeight = rect.height * 0.2f;
                Rect progressBarRect = new Rect(rect.x, rect.y + rect.height - barHeight, barWidth - 20, barHeight);

                // if it is a parallel step or a serial step draw a progress bar in white
                if (logicStep.IsContainer)
                {
                    EditorGUI.DrawRect(progressBarRect, Color.white);  // Parallel or Serial step
                }
                else
                {
                    // Draw the progress bar in green if the step is running, yellow if waiting
                    if (logicStep.State == 100)
                        EditorGUI.DrawRect(progressBarRect, Color.green); // Step is finished
                    else if (!logicStep.IsWaiting)
                        EditorGUI.DrawRect(progressBarRect, Color.green); // Step is running
                    else
                        EditorGUI.DrawRect(progressBarRect, Color.yellow); // Step is waiting
                }
            }
        }

        #endregion

        #region Constants

        private const string PrefKeyShowToggle = "UnityToolbag.QuickToggle.Visible";
        private const string PrefKeyShowDividers = "UnityToolbag.QuickToggle.Dividers";
        private const string PrefKeyShowIcons = "UnityToolbag.QuickToggle.Icons";
        private const string PrefKeyGutterLevel = "UnityToolbag.QuickToggle.Gutter";

        private const string MENU_NAME = "Tools/realvirtual/Settings/Hierarchy Window/Show Icons";
        private const string MENU_DIVIDER = "Tools/realvirtual/Settings/Hierarchy Window/Dividers";

        private const string
            MENU_ICONS = "Tools/realvirtual/Settings/Hierarchy Window/Object Icons";

        private const string
            MENU_GUTTER_0 = "Tools/realvirtual/Settings/Hierarchy Window/Right Gutter/0";

        private const string
            MENU_GUTTER_1 = "Tools/realvirtual/Settings/Hierarchy Window/Right Gutter/1";

        private const string
            MENU_GUTTER_2 = "Tools/realvirtual/Settings/Hierarchy Window/Right Gutter/2"; 

        #endregion

        private static readonly Type HierarchyWindowType;
        private static readonly MethodInfo getObjectIcon;
        public static realvirtualController realvirtualcontroller;

        private static bool stylesBuilt;
        private static bool game4automationNotNull;

        private static GUIStyle styleLock,
            styleUnlocked,
            styleVisOn,
            styleVisOff,
            styleDivider,
            stylesignal;

        private static bool showDivider, showIcons;

        private static  realvirtualBehavior[] behaviors = new realvirtualBehavior[0];

        // Selection-based signal cache: avoids GetComponents<ISignalInterface> per hierarchy item
        private static GameObject _cachedSelectionForSignals;
        private static System.Collections.Generic.HashSet<Signal> _cachedSelectionSignals = new System.Collections.Generic.HashSet<Signal>();

        private static Texture icondrive,
            iconsensor,
            iconbehaviour,
            iconcontroller,
            iconinterface,
            icondriveinactive,
            iconsensorinactive,
            iconbehaviourinactive,
            iconinterfaceinactive,
            iconcontrollerinactive,
            iconhide,
            iconshow,
            iconsource,
            iconsourceinactive,
            icongrip,
            icongripinactive,
            icontransport,
            icontransportinactive,
            icondisplayon,
            icondisplayoff,
            iconkinematic,
            iconkinematicinactive,
            iconcam,
            iconcaminactive;

        // Public icons for Professional extension (CAD status icons)
        public static Texture iconok,
            iconchanged,
            icondeleted,
            iconadded,
            iconmoved;

        /// <summary>
        /// Loads an icon from Editor/EditorAssets/Icons folder using AssetDatabase
        /// </summary>
        private static Texture LoadEditorIcon(string iconName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture>(
                RealvirtualAssetPaths.StarterEditorAssets($"Icons/{iconName}.png"));
        }

        #region Menu stuff

        [MenuItem(MENU_NAME, false, 500)]
        private static void QuickToggleMenu()
        {
            bool toggle = EditorPrefs.GetBool(PrefKeyShowToggle);
            ShowQuickToggle(!toggle);
            Menu.SetChecked(MENU_NAME, !toggle);
        }

        [MenuItem(MENU_NAME, true, 501)]
        private static bool SetupMenuCheckMarks()
        {
            Menu.SetChecked(MENU_NAME, EditorPrefs.GetBool(PrefKeyShowToggle));
            Menu.SetChecked(MENU_DIVIDER, EditorPrefs.GetBool(PrefKeyShowDividers));
            Menu.SetChecked(MENU_ICONS, EditorPrefs.GetBool(PrefKeyShowIcons));

            int gutterLevel = EditorPrefs.GetInt(PrefKeyGutterLevel, 0);
            gutterCount = gutterLevel;
            UpdateGutterMenu(gutterCount);
            return true;
        }

        [MenuItem(MENU_DIVIDER, false, 502)]
        private static void ToggleDivider()
        {
            ToggleSettings(PrefKeyShowDividers, MENU_DIVIDER, out showDivider);
        }

        [MenuItem(MENU_ICONS, false, 503)]
        private static void ToggleIcons()
        {
            ToggleSettings(PrefKeyShowIcons, MENU_ICONS, out showIcons);
        }

        private static void ToggleSettings(string prefKey, string menuString, out bool valueBool)
        {
            valueBool = !EditorPrefs.GetBool(prefKey);
            EditorPrefs.SetBool(prefKey, valueBool);
            Menu.SetChecked(menuString, valueBool);
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(MENU_GUTTER_0, false, 540)]
        private static void SetGutter0()
        {
            SetGutterLevel(0);
        }

        [MenuItem(MENU_GUTTER_1, false, 541)]
        private static void SetGutter1()
        {
            SetGutterLevel(1);
        }

        [MenuItem(MENU_GUTTER_2, false, 542)]
        private static void SetGutter2()
        {
            SetGutterLevel(2);
        }

        private static void SetGutterLevel(int gutterLevel)
        {
            gutterLevel = Mathf.Clamp(gutterLevel, 0, 2);
            EditorPrefs.SetInt(PrefKeyGutterLevel, gutterLevel);
            gutterCount = gutterLevel;
            UpdateGutterMenu(gutterCount);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void UpdateGutterMenu(int gutterLevel)
        {
            string[] gutterKeys = new[] {MENU_GUTTER_0, MENU_GUTTER_1, MENU_GUTTER_2};
            bool[] gutterValues = null;
            switch (gutterLevel)
            {
                case 1:
                    gutterValues = new[] {false, true, false};
                    break;
                case 2:
                    gutterValues = new[] {false, false, true};
                    break;
                default:
                    gutterValues = new[] {true, false, false};
                    break;
            }

            for (int i = 0; i < gutterKeys.Length; i++)
            {
                string key = gutterKeys[i];
                bool isChecked = gutterValues[i];
                Menu.SetChecked(key, isChecked);
            }
        }

        #endregion

        static QuickToggle()
        {
            // Setup initial state of editor prefs if there are no prefs keys yet
            string[] resetPrefs = new string[] {PrefKeyShowToggle, PrefKeyShowDividers, PrefKeyShowIcons};
            foreach (string prefKey in resetPrefs)
            {
                if (EditorPrefs.HasKey(prefKey) == false)
                    EditorPrefs.SetBool(prefKey, false);
            }

            // Fetch some reflection/type stuff for use later on
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            HierarchyWindowType = editorAssembly.GetType("UnityEditor.SceneHierarchyWindow");

            var flags = BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic;
            Type editorGuiUtil = typeof(EditorGUIUtility);
            getObjectIcon = editorGuiUtil.GetMethod("GetIconForObject", flags, null,
                new Type[] {typeof(UnityEngine.Object)}, null);

            // Not calling BuildStyles() in constructor because script gets loaded
            // on Unity initialization, styles might not be loaded yet

            // Reset mouse state
            ResetVars();
            // Setup quick toggle
            ShowQuickToggle(EditorPrefs.GetBool(PrefKeyShowToggle));
        }

        public static void Refresh()
        {
            ShowQuickToggle(EditorPrefs.GetBool(PrefKeyShowToggle));
        }

        public static void SetGame4Automation(realvirtualController controller)
        {
            realvirtualcontroller = controller;
            if (controller != null)
                game4automationNotNull = true;
            else
                game4automationNotNull = false;
            Refresh();
        }

        public static void ShowQuickToggle(bool show)
        {
            stylesBuilt = false;
            EditorPrefs.SetBool(PrefKeyShowToggle, show);
            showDivider = EditorPrefs.GetBool(PrefKeyShowDividers, false);
            showIcons = EditorPrefs.GetBool(PrefKeyShowIcons, false);
            gutterCount = EditorPrefs.GetInt(PrefKeyGutterLevel);

            if (show)
            {
                EditorApplication.update -= HandleEditorUpdate;
                ResetVars();
                EditorApplication.update += HandleEditorUpdate;
                EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchyItem;
            }
            else
            {
                EditorApplication.update -= HandleEditorUpdate;
                EditorApplication.hierarchyWindowItemOnGUI -= DrawHierarchyItem;
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private struct PropagateState
        {
            public bool isVisibility;
            public bool propagateValue;

            public PropagateState(bool isVisibility, bool propagateValue)
            {
                this.isVisibility = isVisibility;
                this.propagateValue = propagateValue;
            }
        }

        private static PropagateState propagateState;

        // Because we can't hook into OnGUI of HierarchyWindow, doing a hack
        // button that involves the editor update loop and the hierarchy item draw event
        private static bool isFrameFresh;
        private static bool isMousePressed;

        public static int gutterCount = 0;

        private static void ResetVars()
        {
            isFrameFresh = false;
            isMousePressed = false;
        }

        private static void HandleEditorUpdate()
        {
            //Debug.Log("HandleEditorUpdate");
            EditorWindow window = EditorWindow.mouseOverWindow;
            if (window == null)
            {
                ResetVars();
                return;
            }

            if (window.GetType() == HierarchyWindowType)
            {
                if (window.wantsMouseMove == false)
                    window.wantsMouseMove = true;

                isFrameFresh = true;
            }
        }

        public static Rect CreateRect(Rect selrect, float posmin, float posmax, int padding)
        {
            float gutterX = selrect.height * gutterCount;
            if (gutterX > 0)
                gutterX += selrect.height * 0.1f;
            float xMax = selrect.xMax - gutterX;
            Rect rect = new Rect(selrect)
            {
                xMin = xMax - (selrect.height * posmin),
                xMax = xMax - selrect.height * posmax
            };
            rect.xMax -= padding;
            rect.xMin += padding;
            rect.yMax -= padding;
            rect.yMin += padding;

            return rect;
        }

        private static realvirtualBehavior[] GetGame4AutomationComponents(GameObject target)
        {
            realvirtualBehavior[] behav;
            behav = target.GetComponents<realvirtualBehavior>();
            return behav;
        }

        private static realvirtualBehavior GetG4AComponent(Type type)
        {
            if (behaviors == null)
                return null;
            foreach (realvirtualBehavior behavior in behaviors)
            {
                Type mytype = behavior.GetType();
                if (mytype.IsSubclassOf(type))
                    return behavior;
                if (mytype == type)
                    return behavior;
            }
            return null;
        }
       
        private static void DrawHierarchyItem(int instanceId, Rect selectionRect)
        {
            Color fontColor = Color.blue;
            Color backgroundColor = new Color(.76f, .76f, .76f);

            Signal _signal = null;
            BaseDrive _drive = null;;
            Behaviour _behaviour = null;;
            InterfaceBaseClass _interface2 = null; ;
            BaseSource _source = null;
            BaseSensor _sensor = null;
            ControlLogic _controllogic = null; ;
            BaseGrip _grip = null; ;
            BaseTransportSurface _tansport = null;;
            Group group = null;;
            Kinematic kinematic = null;
            BaseCAM _cam = null;
          
            
            if (!game4automationNotNull)
                return;

            if (!realvirtualcontroller.ShowHierarchyIcons)
                return;
            
            BuildStyles();
            
            if (iconsensor == null)
                return;

            if (icondrive == null)
                return;

#pragma warning disable CS0618 // InstanceIDToObject is obsolete - no documented replacement for int instanceId
            GameObject target = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618


            if (target == null)
                return;

            if (!ReferenceEquals(target.GetComponent<realvirtualController>(), null))
                return;
   
            // game4automation types
            behaviors = GetGame4AutomationComponents(target);

            if (behaviors.Length > 0)
            {
               
                 _signal = (Signal)GetG4AComponent(typeof(Signal));
                _drive =  (BaseDrive)GetG4AComponent(typeof(BaseDrive));
                 _behaviour =  (BehaviorInterface)GetG4AComponent(typeof(BehaviorInterface));
                 _interface2 = (InterfaceBaseClass)GetG4AComponent(typeof(InterfaceBaseClass));

                 _sensor =  (BaseSensor)GetG4AComponent(typeof(BaseSensor));
                 _controllogic = (ControlLogic)GetG4AComponent(typeof(ControlLogic));
                 _grip = (BaseGrip)GetG4AComponent(typeof(BaseGrip));
                 _tansport = (BaseTransportSurface)GetG4AComponent(typeof(BaseTransportSurface));
                 group = (Group)GetG4AComponent(typeof(Group));
                 kinematic = (Kinematic)GetG4AComponent(typeof(Kinematic));
                 _cam = (BaseCAM) GetG4AComponent(typeof(BaseCAM));
                 _source = (BaseSource) GetG4AComponent(typeof(BaseSource));
              
            }
            
            
            // Reserve the draw rects
            float gutterX = selectionRect.height * gutterCount;
            if (gutterX > 0)
                gutterX += selectionRect.height * 0.1f;
            float xMax = selectionRect.xMax - gutterX;
            float curpos = 1.1f;
            float size=0;
            Rect hiddenRect =  CreateRect(selectionRect, 0 , 0, 0);
            
          
            bool isHidden = false;

            curpos += size;

            // Allow professional package to draw additional icons (e.g., CAD status)
            OnDrawProfessionalIcons?.Invoke(instanceId, selectionRect, target, behaviors, ref curpos);

            Rect sensorrect = new Rect();
            sensorrect.center = new Vector2(0,0);
            Rect unforcrect = new Rect();
            sensorrect.center  =  new Vector2(0,0);
            if (!ReferenceEquals(_signal, null))
            {
         

                if (_signal.Settings.Override)
                {
                    size = 1; unforcrect = CreateRect(selectionRect, curpos + size, curpos, 1);
                    GUI.DrawTexture(unforcrect, iconchanged, ScaleMode.ScaleToFit);
                    curpos += size;
                }
                
                size = 5;
                sensorrect = CreateRect(selectionRect, curpos + size, curpos, 0);

                if (_signal.IsInput())
                {
                    stylesignal.normal.textColor = new Color(1, 0.4f, 0.016f, 1);
                }
                else
                {
                    stylesignal.normal.textColor = Color.green;
                }

                if (_signal.Settings.Override)
                {
                    stylesignal.fontStyle = FontStyle.Italic;
                }
                else
                {
                    stylesignal.fontStyle = FontStyle.Normal;
                }

                // Gray only if signal is mapped to interface AND interface is not connected
                // Signals without interface are always colored (for LogicSteps use)
                if (_signal.interfacesignal != null && _signal.GetStatusConnected() == false)
                {
                    stylesignal.normal.textColor = Color.gray;
                }
                
                /// Highlight Signals if connected to current gameobject
                var sel = Selection.activeGameObject;
                if (!ReferenceEquals(sel, null))
                {
                    // Rebuild cache only when selection changes (not per hierarchy item)
                    if (_cachedSelectionForSignals != sel)
                    {
                        _cachedSelectionForSignals = sel;
                        _cachedSelectionSignals.Clear();
                        var signalints = sel.GetComponents<ISignalInterface>();
                        foreach (var signalint in signalints)
                        {
                            if (!ReferenceEquals(signalint, null))
                            {
                                var conns = signalint.GetSignals();
                                foreach (var conn in conns)
                                    _cachedSelectionSignals.Add(conn);
                            }
                        }
                    }
                    if (_cachedSelectionSignals.Contains(_signal))
                        stylesignal.normal.textColor = Color.yellow;
                }

                // No parentheses if connected to behavior OR connected via interface
                bool isConnectedToAnything = _signal.IsConnectedToBehavior() || _signal.GetStatusConnected();
                if (isConnectedToAnything)
                    GUI.Label(sensorrect, _signal.GetVisuText(), stylesignal);
                else
                    GUI.Label(sensorrect, "(" + _signal.GetVisuText() + ")", stylesignal);
                curpos += size;
            }
            

            if (!ReferenceEquals(_interface2, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (iconinterface != null)
                {
                    if (_interface2.IsConnected && _interface2.enabled)
                    {
                        GUI.DrawTexture(driverect, iconinterface, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, iconinterfaceinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }

            // Draw LogicStep progress bars
            DrawLogicStepProgress(selectionRect, target);

            if (!ReferenceEquals(_controllogic, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (iconcontroller != null)
                {
                    if (_controllogic.enabled)
                    {
                        GUI.DrawTexture(driverect, iconcontroller, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, iconcontrollerinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }

            if (!ReferenceEquals(_tansport, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (icontransport != null)
                {
                    if (_tansport.enabled)
                    {
                        GUI.DrawTexture(driverect, icontransport, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, icontransportinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }


            if (!ReferenceEquals(_behaviour, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (iconbehaviour != null)
                {
                    if (_behaviour.enabled)
                    {
                        GUI.DrawTexture(driverect, iconbehaviour, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, iconbehaviourinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }
            
            if (!ReferenceEquals(_cam, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (iconcam != null)
                {
                    if (_cam.enabled)
                    {
                        GUI.DrawTexture(driverect, iconcam, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, iconcaminactive, ScaleMode.ScaleToFit);
                    }
                }
                curpos += size;
            }

            if (!ReferenceEquals(_grip, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (icongrip != null)
                {
                    if (_grip.enabled)
                    {
                        GUI.DrawTexture(driverect, icongrip, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, icongripinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }

            if (!ReferenceEquals(_source, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (iconsource != null)
                {
                    if (_source.enabled)
                    {
                        GUI.DrawTexture(driverect, iconsource, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, iconsourceinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }

            if (!ReferenceEquals(_sensor, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (iconsensor != null)
                {
                    if (_sensor.enabled)
                    {
                        GUI.DrawTexture(driverect, iconsensor, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, iconsensorinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }

            if (!ReferenceEquals(_drive, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (icondrive != null)
                {
                    if (_drive.enabled)
                    {
                        GUI.DrawTexture(driverect, icondrive, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, icondriveinactive, ScaleMode.ScaleToFit);
                    }
                }

                curpos += size;
            }
            
            if (!ReferenceEquals(kinematic, null) && realvirtualcontroller.ShowComponents)
            {
                size = 1;
                Rect driverect = CreateRect(selectionRect, curpos + size, curpos, 1);

                if (iconkinematic != null)
                {
                    if (kinematic.enabled)
                    {
                        GUI.DrawTexture(driverect, iconkinematic, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.DrawTexture(driverect, iconkinematicinactive, ScaleMode.ScaleToFit);
                    }
                }
                curpos += size;
            }

            if (!ReferenceEquals(group, null))
            {
                size =realvirtualcontroller.WidthGroupName;
                Rect grouprect = CreateRect(selectionRect, curpos + size, curpos, 0);
             
                stylesignal.normal.textColor = Color.yellow;
           
                GUI.Label(grouprect, group.GetVisuText(), stylesignal);
            }
            
            if (!ReferenceEquals(kinematic, null))
            {
                size =realvirtualcontroller.WidthGroupName;
                Rect kinrect = CreateRect(selectionRect, curpos + size, curpos, 0);
             
                stylesignal.normal.textColor =new Color(255,165,0);
           
                GUI.Label(kinrect, kinematic.GetVisuText(), stylesignal);
            }
            

            curpos += size;
            // Get states
            bool isVisible = target.activeSelf;
            bool isLocked = (target.hideFlags & HideFlags.NotEditable) > 0;


            // Draw the visibility toggle
            Rect visRect = new Rect(selectionRect)
            {
                xMin = xMax - (selectionRect.height * 1.05f),
                xMax = xMax - selectionRect.height * 0f
            };
            
            var iconshowhide = (isVisible) ? icondisplayon : icondisplayoff;
            GUI.DrawTexture(visRect, iconshowhide, ScaleMode.ScaleToFit);

          
            // Draw optional divider
            if (showDivider)
            {
                Rect lineRect = new Rect(selectionRect)
                {
                    yMin = selectionRect.yMax - 1f,
                    yMax = selectionRect.yMax + 2f
                };
                GUI.Label(lineRect, GUIContent.none, styleDivider);
            }

            // Draw optional object icons
            if (showIcons && getObjectIcon != null)
            {
                Texture2D iconImg = getObjectIcon.Invoke(null, new object[] {target}) as Texture2D;
                if (iconImg != null)
                {
                    Rect iconRect = new Rect(selectionRect)
                    {
                        xMin = visRect.xMin - 30,
                        xMax = visRect.xMin - 5
                    };
                    GUI.DrawTexture(iconRect, iconImg, ScaleMode.ScaleToFit);
                }
            }

            if (Event.current == null)
                return;
            HandleMouse(target, isVisible, isLocked, isHidden, visRect, hiddenRect, sensorrect,unforcrect);
        }


        private static void HandleMouse(GameObject target, bool isVisible, bool isLocked, bool isHidden, Rect visRect,
            Rect hiddenrect, Rect signalrect, Rect unforerect)
        {
            Event evt = Event.current;

            bool toggleActive = visRect.Contains(evt.mousePosition);
        
            bool toggleHide = hiddenrect.Contains(evt.mousePosition);
            bool toggleSignal = signalrect.Contains(evt.mousePosition);
            bool toogleUnforce = unforerect.Contains(evt.mousePosition);
            bool stateChanged = (toggleActive || toggleHide || toggleSignal || toogleUnforce);

            bool doMouse = false;
            switch (evt.type)
            {
                case EventType.MouseDown:
                    // Checking is frame fresh so mouse state is only tested once per frame
                    // instead of every time a hierarchy item is drawn
                    bool isMouseDown = false;
                    if (isFrameFresh && stateChanged)
                    {
                        isMouseDown = !isMousePressed;
                        isMousePressed = true;
                        isFrameFresh = false;
                    }

                    if (stateChanged && isMouseDown)
                    {
                        doMouse = true;
                        if (toggleActive) isVisible = !isVisible;

                        if (toogleUnforce)
                        {
                            Signal signal = target.GetComponent<Signal>();
                            signal.Unforce();
                            signal.Settings.Override = false;
                        }
                        if (toggleSignal)
                        {
                            Signal signal = target.GetComponent<Signal>();
                            signal.OnToggleHierarchy();

                        }
                        propagateState = new PropagateState(toggleActive, (toggleActive) ? isVisible : isLocked);
                        evt.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    doMouse = isMousePressed;
                    break;
                case EventType.DragPerform:
                case EventType.DragExited:
                case EventType.DragUpdated:
                case EventType.MouseUp:
                    ResetVars();
                    break;
            }

            if (doMouse && stateChanged)
            {
                if (propagateState.isVisibility)
                    realvirtualcontroller.SetVisible(target, propagateState.propagateValue);
                //else
                //   realvirtual.SetLockObject(target, propagateState.propagateValue);

                EditorApplication.RepaintHierarchyWindow();
            }
        }


        private static void BuildStyles()
        {
            // All of the styles have been built, don't do anything
            if (stylesBuilt)
                return;

            // Now build the GUI styles
            // Using icons different from regular lock button so that
            // it would look darker
            var tempStyle = GUI.skin.FindStyle("IN LockButton");
            styleLock = new GUIStyle(tempStyle)
            {
                normal = tempStyle.onNormal,
                active = tempStyle.onActive,
                hover = tempStyle.onHover,
                focused = tempStyle.onFocused,
            };


            // Unselected just makes the normal states have no lock images
            tempStyle = GUI.skin.FindStyle("OL Toggle");
            styleUnlocked = new GUIStyle(tempStyle);
#if UNITY_2018_3_OR_NEWER
            tempStyle = new GUIStyle()
            {
                normal = new GUIStyleState()
                    {background = EditorGUIUtility.Load("Icons/animationvisibilitytoggleoff.png") as Texture2D},
                onNormal = new GUIStyleState()
                    {background = EditorGUIUtility.Load("Icons/animationvisibilitytoggleon.png") as Texture2D},
                fixedHeight = 11,
                fixedWidth = 13,
                border = new RectOffset(2, 2, 2, 2),
                overflow = new RectOffset(-1, 1, -2, 2),
                padding = new RectOffset(3, 3, 3, 3),
                richText = false,
                stretchHeight = false,
                stretchWidth = false,
            };
#else
            tempStyle = GUI.skin.FindStyle("VisibilityToggle");
#endif

            styleVisOff = new GUIStyle(tempStyle);
            styleVisOn = new GUIStyle(tempStyle)
            {
                normal = new GUIStyleState() {background = tempStyle.onNormal.background}
            };

            styleDivider = GUI.skin.FindStyle("EyeDropperHorizontalLine");


            // Styles Game4Automation

            tempStyle = GUI.skin.FindStyle("WhiteLabel");
            stylesignal = new GUIStyle(tempStyle);
            stylesignal.fontSize = 10;
            stylesignal.alignment = TextAnchor.MiddleRight;

            // Load hierarchy icons from Editor/EditorAssets/Icons
            icondrive = LoadEditorIcon("Drive");
            iconsensor = LoadEditorIcon("Sensor");
            iconbehaviour = LoadEditorIcon("Behaviour");
            iconcontroller = LoadEditorIcon("PLC");
            iconinterface = LoadEditorIcon("Interface");
            icondriveinactive = LoadEditorIcon("Driveinactive");
            iconsensorinactive = LoadEditorIcon("Sensorinactive");
            iconbehaviourinactive = LoadEditorIcon("Behaviourinactive");
            iconcontrollerinactive = LoadEditorIcon("PLCinactive");
            iconinterfaceinactive = LoadEditorIcon("Interfaceinactive");

            iconsource = LoadEditorIcon("Source");
            iconsourceinactive = LoadEditorIcon("Sourceinactive");
            icongrip = LoadEditorIcon("Grip");
            icongripinactive = LoadEditorIcon("Gripinactive");
            icontransport = LoadEditorIcon("Transportsurface");
            icontransportinactive = LoadEditorIcon("Transportsurfaceinactive");

            iconkinematic = LoadEditorIcon("Kinematic");
            iconkinematicinactive = LoadEditorIcon("Kinematicinactive");

            iconcam = LoadEditorIcon("CAM");
            iconcaminactive = LoadEditorIcon("CAMinactive");

            iconmoved = LoadEditorIcon("Moved");
            iconadded = LoadEditorIcon("Added");
            iconchanged = LoadEditorIcon("Updated");
            icondeleted = LoadEditorIcon("Deleted");
            iconhide = LoadEditorIcon("Hide");
            iconshow = LoadEditorIcon("Show");
            iconok = LoadEditorIcon("OK");
            icondisplayon = LoadEditorIcon("displayon");
            icondisplayoff = LoadEditorIcon("displayoff");
        

            stylesBuilt = (styleLock != null && styleUnlocked != null &&
                           styleVisOn != null && styleVisOff != null &&
                           styleDivider != null);
        }
    }
}
#endif