// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace realvirtual
{
    //! Simple action definition for Quick Edit
    public class QuickEditAction
    {
        public string Name { get; set; }
        public string Tooltip { get; set; }
        public Func<GameObject[], bool> IsValid { get; set; }
        public Action<GameObject[]> Execute { get; set; }
        public int Priority { get; set; } = 100;
        public int ColumnsPerRow { get; set; } = 2; // Default to 2 columns like most text buttons
        public Texture2D Icon { get; set; }
        public string GroupId { get; set; } = null; // Optional group identifier to keep actions together
        public Color? ButtonColor { get; set; } = null; // Optional custom button color
        
        // Cached validation result for current frame
        internal bool? _cachedValidation;
        internal int _lastValidationFrame = -1;
    }
    
    //! Registry for Quick Edit actions - allows dynamic registration via code
    public static class QuickEditRegistry
    {
        private static List<QuickEditAction> registeredActions = new List<QuickEditAction>();
        private static bool isInitialized = false;
        
        //! Register a new Quick Edit action
        public static void RegisterAction(string name, Action<GameObject[]> execute, 
            Func<GameObject[], bool> isValid = null, string tooltip = "", int priority = 100, 
            int columnsPerRow = 2, Texture2D icon = null, string groupId = null, Color? buttonColor = null)
        {
            // Remove any existing action with the same name to prevent duplicates
            registeredActions.RemoveAll(a => a.Name == name);
            
            var action = new QuickEditAction
            {
                Name = name,
                Tooltip = tooltip,
                IsValid = isValid ?? (objs => true), // Default: always valid
                Execute = execute,
                Priority = priority,
                ColumnsPerRow = columnsPerRow,
                Icon = icon,
                GroupId = groupId,
                ButtonColor = buttonColor
            };
            
            registeredActions.Add(action);
            SortActions();
        }
        
        //! Register a Quick Edit action object
        public static void RegisterAction(QuickEditAction action)
        {
            if (action != null && !registeredActions.Contains(action))
            {
                registeredActions.Add(action);
                SortActions();
            }
        }
        
        //! Unregister an action by name
        public static void UnregisterAction(string name)
        {
            registeredActions.RemoveAll(a => a.Name == name);
        }
        
        //! Get all actions valid for the current selection
        public static List<QuickEditAction> GetValidActions(GameObject[] selection)
        {
            EnsureInitialized();
            
            int currentFrame = Time.frameCount;
            var validActions = new List<QuickEditAction>();
            
            foreach (var action in registeredActions)
            {
                // Use cached validation if available for this frame
                if (action._lastValidationFrame != currentFrame)
                {
                    action._cachedValidation = action.IsValid(selection);
                    action._lastValidationFrame = currentFrame;
                }
                
                if (action._cachedValidation == true)
                {
                    validActions.Add(action);
                }
            }
            
            return validActions;
        }
        
        //! Get all registered actions
        public static List<QuickEditAction> GetAllActions()
        {
            EnsureInitialized();
            return registeredActions;
        }
        
        //! Clear all registered actions
        public static void ClearActions()
        {
            registeredActions.Clear();
        }
        
        private static void SortActions()
        {
            registeredActions = registeredActions.OrderBy(a => a.Priority).ToList();
        }
        
        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
                isInitialized = true;
            }
        }
        
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Clear any existing registrations to prevent duplicates
            registeredActions.Clear();
            
            // Let other scripts register their actions
            EditorApplication.delayCall += () =>
            {
                // This delay ensures other scripts have time to register
                // Look for all static methods with the [QuickEditExtension] attribute
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            var methods = type.GetMethods(System.Reflection.BindingFlags.Static | 
                                                         System.Reflection.BindingFlags.Public | 
                                                         System.Reflection.BindingFlags.NonPublic);
                            foreach (var method in methods)
                            {
                                if (method.GetCustomAttributes(typeof(QuickEditExtensionAttribute), false).Length > 0)
                                {
                                    method.Invoke(null, null);
                                }
                            }
                        }
                    }
                    catch { }
                }
            };
        }
    }
    
    //! Attribute to mark methods that register QuickEdit extensions
    [AttributeUsage(AttributeTargets.Method)]
    public class QuickEditExtensionAttribute : Attribute
    {
    }
}