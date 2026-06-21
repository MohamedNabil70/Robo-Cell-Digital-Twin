// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
#if UNITY_2021_2_OR_NEWER
    //! Registry system for discovering and managing custom toolbar buttons.
    //! Automatically scans assemblies for classes marked with [RealvirtualToolbarButton]
    //! and provides an ordered list of button IDs for the toolbar overlay.
    [InitializeOnLoad]
    public static class ToolbarButtonRegistry
    {
        //! Information about a registered toolbar button
        public class ButtonInfo
        {
            public Type ButtonType { get; set; }
            public string ButtonId { get; set; }
            public int Order { get; set; }
            public string Group { get; set; }
            public RealvirtualToolbarButtonAttribute Attribute { get; set; }
        }

        private static List<ButtonInfo> registeredButtons;
        private static bool isInitialized = false;

        //! Static constructor - automatically discovers buttons when Unity loads
        static ToolbarButtonRegistry()
        {
            EditorApplication.delayCall += Initialize;
        }

        //! Initializes the registry by discovering all toolbar buttons
        private static void Initialize()
        {
            if (isInitialized)
                return;

            DiscoverButtons();
            isInitialized = true;
        }

        //! Scans all loaded assemblies for classes with [RealvirtualToolbarButton] attribute
        private static void DiscoverButtons()
        {
            registeredButtons = new List<ButtonInfo>();

            try
            {
                // Get all loaded assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Skip system assemblies for performance
                        var assemblyName = assembly.GetName().Name;
                        if (assemblyName.StartsWith("System") ||
                            assemblyName.StartsWith("mscorlib") ||
                            assemblyName.StartsWith("netstandard") ||
                            assemblyName.StartsWith("Microsoft"))
                            continue;

                        // Get all types in assembly
                        var types = assembly.GetTypes();

                        foreach (var type in types)
                        {
                            // Check if type has the RealvirtualToolbarButton attribute
                            var attribute = type.GetCustomAttribute<RealvirtualToolbarButtonAttribute>();
                            if (attribute != null)
                            {
                                // Validate that the type inherits from one of the base classes
                                // Skip validation for core buttons (they use the old pattern)
                                if (!IsValidButtonType(type))
                                {
                                    // Silently skip core buttons (they're hardcoded in RealvirtualToolbarOverlay)
                                    var coreButtonNames = new[] { "GizmoToggle", "DrawModeDropdown", "QuickEditButton", "MovePivotButton" };
                                    if (!coreButtonNames.Contains(type.Name))
                                    {
                                        Logger.Warning($"Type {type.Name} has [RealvirtualToolbarButton] but doesn't inherit from a valid base class", null);
                                    }
                                    continue;
                                }

                                // Check if type has EditorToolbarElement attribute
                                var elementAttr = type.GetCustomAttribute<UnityEditor.Toolbars.EditorToolbarElementAttribute>();
                                if (elementAttr == null)
                                {
                                    Logger.Warning($"Type {type.Name} has [RealvirtualToolbarButton] but missing [EditorToolbarElement] attribute", null);
                                    continue;
                                }

                                // Get the button ID from the EditorToolbarElement attribute
                                var idField = elementAttr.GetType().GetField("id", BindingFlags.Public | BindingFlags.Instance);
                                var buttonId = idField?.GetValue(elementAttr) as string;

                                if (string.IsNullOrEmpty(buttonId))
                                {
                                    // Try to get ID from a static 'id' field on the button class
                                    var staticIdField = type.GetField("id", BindingFlags.Public | BindingFlags.Static);
                                    buttonId = staticIdField?.GetValue(null) as string;
                                }

                                if (string.IsNullOrEmpty(buttonId))
                                {
                                    Logger.Warning($"Could not determine button ID for type {type.Name}", null);
                                    continue;
                                }

                                // Register the button
                                var buttonInfo = new ButtonInfo
                                {
                                    ButtonType = type,
                                    ButtonId = buttonId,
                                    Order = attribute.Order,
                                    Group = attribute.Group ?? string.Empty,
                                    Attribute = attribute
                                };

                                registeredButtons.Add(buttonInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip assemblies that can't be scanned
                        Logger.Warning($"Could not scan assembly {assembly.GetName().Name}: {ex.Message}", null);
                    }
                }

                // Sort buttons by order
                registeredButtons = registeredButtons.OrderBy(b => b.Order).ToList();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error discovering toolbar buttons: {ex.Message}", null);
            }
        }

        //! Checks if a type is a valid button type (inherits from base classes)
        private static bool IsValidButtonType(Type type)
        {
            return typeof(RealvirtualToolbarButtonBase).IsAssignableFrom(type) ||
                   typeof(RealvirtualToolbarToggleBase).IsAssignableFrom(type) ||
                   typeof(RealvirtualToolbarDropdownBase).IsAssignableFrom(type);
        }

        //! Gets all registered button IDs in the correct order
        public static string[] GetButtonIds()
        {
            if (!isInitialized)
                Initialize();

            return registeredButtons.Select(b => b.ButtonId).ToArray();
        }

        //! Gets all registered buttons
        public static List<ButtonInfo> GetButtons()
        {
            if (!isInitialized)
                Initialize();

            return new List<ButtonInfo>(registeredButtons);
        }

        //! Gets buttons in a specific group
        public static List<ButtonInfo> GetButtonsInGroup(string group)
        {
            if (!isInitialized)
                Initialize();

            return registeredButtons.Where(b => b.Group == group).ToList();
        }

        //! Forces a re-scan of all assemblies for toolbar buttons
        public static void Refresh()
        {
            isInitialized = false;
            Initialize();
        }

        //! Manually refresh the button registry
        private static void RefreshFromMenu()
        {
            Refresh();
            Logger.Message("Toolbar button registry refreshed", null);
        }

        //! Show registered buttons in console
        private static void ShowRegisteredButtons()
        {
            if (!isInitialized)
                Initialize();

            Logger.Message($"=== Registered Toolbar Buttons ({registeredButtons.Count}) ===", null);
            foreach (var button in registeredButtons)
            {
                Logger.Message($"  {button.ButtonType.Name} - ID: {button.ButtonId}, Order: {button.Order}, Group: {button.Group}", null);
            }
        }
    }
#endif
}
