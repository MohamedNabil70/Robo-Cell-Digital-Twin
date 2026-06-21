// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace realvirtual
{
    //! Registry system for discovering and managing custom overlay buttons.
    //! Automatically scans assemblies for classes marked with [OverlayButton] attribute
    //! and provides organized button lists grouped by overlay type, section, and row.
    //!
    //! This is a generic system that works with any Unity Overlay-derived class.
    [InitializeOnLoad]
    public static class OverlayButtonRegistry
    {
        //! Information about a registered overlay button
        public class ButtonInfo
        {
            public Type ButtonType { get; set; }
            public Type OverlayType { get; set; }
            public double Order { get; set; }
            public string Section { get; set; }
            public string Group { get; set; }
            public OverlayButtonAttribute Attribute { get; set; }

            //! Row number (integer part of order)
            public int Row => (int)Math.Floor(Order);

            //! Position within row (decimal part of order)
            public double RowPosition => Order - Math.Floor(Order);
        }

        private static Dictionary<Type, List<ButtonInfo>> buttonsByOverlay;
        private static bool isInitialized = false;

        //! Static constructor - automatically discovers buttons when Unity loads
        static OverlayButtonRegistry()
        {
            EditorApplication.delayCall += Initialize;
        }

        //! Initializes the registry by discovering all overlay buttons
        private static void Initialize()
        {
            if (isInitialized)
                return;

            DiscoverButtons();
            isInitialized = true;
        }

        //! Scans all loaded assemblies for classes with [OverlayButton] attribute
        private static void DiscoverButtons()
        {
            buttonsByOverlay = new Dictionary<Type, List<ButtonInfo>>();

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
                            // Check if type has OverlayButton attributes (can have multiple)
                            var attributes = type.GetCustomAttributes<OverlayButtonAttribute>();

                            foreach (var attribute in attributes)
                            {
                                // Validate that the type inherits from a valid base class
                                if (!IsValidButtonType(type))
                                {
                                    Logger.Warning($"Type {type.Name} has [OverlayButton] but doesn't inherit from a valid base class (OverlayButtonBase, OverlayToggleBase, or OverlaySectionBase)", null);
                                    continue;
                                }

                                // Register the button for this overlay type
                                var buttonInfo = new ButtonInfo
                                {
                                    ButtonType = type,
                                    OverlayType = attribute.OverlayType,
                                    Order = attribute.Order,
                                    Section = attribute.Section ?? "Custom",
                                    Group = attribute.Group ?? string.Empty,
                                    Attribute = attribute
                                };

                                // Add to dictionary by overlay type
                                if (!buttonsByOverlay.ContainsKey(attribute.OverlayType))
                                {
                                    buttonsByOverlay[attribute.OverlayType] = new List<ButtonInfo>();
                                }

                                buttonsByOverlay[attribute.OverlayType].Add(buttonInfo);

                                Logger.Message($"Registered overlay button: {type.Name} for {attribute.OverlayType.Name} (Order: {attribute.Order}, Section: {attribute.Section})", null);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip assemblies that can't be scanned
                        Logger.Warning($"Could not scan assembly {assembly.GetName().Name}: {ex.Message}", null);
                    }
                }

                // Sort buttons by order within each overlay type
                foreach (var overlayType in buttonsByOverlay.Keys.ToList())
                {
                    buttonsByOverlay[overlayType] = buttonsByOverlay[overlayType]
                        .OrderBy(b => b.Order)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error discovering overlay buttons: {ex.Message}", null);
            }
        }

        //! Checks if a type is a valid button type (inherits from base classes)
        private static bool IsValidButtonType(Type type)
        {
            // Check against base types (will be defined next)
            return type.GetInterfaces().Any(i => i.Name == "IOverlayButton") ||
                   type.BaseType?.Name == "OverlayButtonBase" ||
                   type.BaseType?.Name == "OverlayToggleBase" ||
                   type.BaseType?.Name == "OverlaySectionBase";
        }

        //! Gets all registered buttons for a specific overlay type
        public static List<ButtonInfo> GetButtonsForOverlay(Type overlayType)
        {
            if (!isInitialized)
                Initialize();

            if (buttonsByOverlay.TryGetValue(overlayType, out var buttons))
            {
                return new List<ButtonInfo>(buttons);
            }

            return new List<ButtonInfo>();
        }

        //! Gets all registered buttons for a specific overlay type (generic version)
        public static List<ButtonInfo> GetButtonsForOverlay<T>() where T : Overlay
        {
            return GetButtonsForOverlay(typeof(T));
        }

        //! Gets buttons for a specific overlay and section
        public static List<ButtonInfo> GetButtonsForSection(Type overlayType, string section)
        {
            if (!isInitialized)
                Initialize();

            return GetButtonsForOverlay(overlayType)
                .Where(b => b.Section == section)
                .ToList();
        }

        //! Gets buttons grouped by row for a specific overlay and section
        //! Returns dictionary where key is row number and value is list of buttons in that row (sorted by position)
        public static Dictionary<int, List<ButtonInfo>> GetButtonsByRow(Type overlayType, string section)
        {
            if (!isInitialized)
                Initialize();

            var buttons = GetButtonsForSection(overlayType, section);

            return buttons
                .GroupBy(b => b.Row)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(b => b.RowPosition).ToList()
                );
        }

        //! Gets all unique sections for a specific overlay type
        public static List<string> GetSectionsForOverlay(Type overlayType)
        {
            if (!isInitialized)
                Initialize();

            return GetButtonsForOverlay(overlayType)
                .Select(b => b.Section)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        //! Forces a re-scan of all assemblies for overlay buttons
        public static void Refresh()
        {
            isInitialized = false;
            Initialize();
        }

        //! Manually refresh the button registry
        private static void RefreshFromMenu()
        {
            Refresh();
            Logger.Message("Overlay button registry refreshed", null);
        }

        //! Show registered buttons in console
        private static void ShowRegisteredButtons()
        {
            if (!isInitialized)
                Initialize();

            Logger.Message($"=== Registered Overlay Buttons ===", null);

            foreach (var kvp in buttonsByOverlay)
            {
                Logger.Message($"\n{kvp.Key.Name} ({kvp.Value.Count} buttons):", null);

                var buttonsBySection = kvp.Value.GroupBy(b => b.Section);
                foreach (var section in buttonsBySection)
                {
                    Logger.Message($"  Section: {section.Key}", null);
                    foreach (var button in section)
                    {
                        Logger.Message($"    {button.ButtonType.Name} - Order: {button.Order} (Row: {button.Row}, Pos: {button.RowPosition:F1})", null);
                    }
                }
            }
        }
    }
}
