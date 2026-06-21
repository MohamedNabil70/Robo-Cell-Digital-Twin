// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;

namespace realvirtual
{
    //! Static logging class for realvirtual framework with automatic hierarchy path inclusion
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class Logger
    {
        private const string PINK = "#FF1493";
        private const string ICON = "◆";
        private const string WARNING_ICON = "⚠";
        private const string ERROR_ICON = "✖";
        
        //! Gets the full path from context
        private static string GetFullPath(Object context)
        {
            if (context == null) return "";
            
            GameObject go = null;
            
            // Handle different context types
            if (context is GameObject)
            {
                go = context as GameObject;
            }
            else if (context is Component)
            {
                go = (context as Component).gameObject;
            }
            
            if (go != null)
            {
                return SceneTools.GetObjectPath(go);
            }
            
            return "";
        }
        
        //! Logs a message without stack trace
        [HideInCallstack]
        public static void Message(string message, Object context = null)
        {
            var path = GetFullPath(context);
            var pathPrefix = string.IsNullOrEmpty(path) ? "" : $" [{path}]";
            var formatted = $"<color={PINK}>{ICON}</color> <b>realvirtual:</b>{pathPrefix} {message}";
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, "{0}", formatted);
        }
        
        //! Logs a message with stack trace (excluding Logger from stack)
        [HideInCallstack]
        public static void Log(string message, Object context = null, bool showStackTrace = true)
        {
            var path = GetFullPath(context);
            var pathPrefix = string.IsNullOrEmpty(path) ? "" : $" [{path}]";
            var formatted = $"<color={PINK}>{ICON}</color> <b>realvirtual:</b>{pathPrefix} {message}";
            if (showStackTrace)
                Debug.Log(formatted, context);
            else
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, "{0}", formatted);
        }

        //! Logs a warning with stack trace (excluding Logger from stack)
        [HideInCallstack]
        public static void Warning(string message, Object context = null, bool showStackTrace = true)
        {
            var path = GetFullPath(context);
            var pathPrefix = string.IsNullOrEmpty(path) ? "" : $" [{path}]";
            var formatted = $"<color={PINK}>{WARNING_ICON}</color> <b>realvirtual:</b>{pathPrefix} {message}";
            if (showStackTrace)
                Debug.LogWarning(formatted, context);
            else
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, context, "{0}", formatted);
        }

        //! Logs an error with stack trace (excluding Logger from stack)
        [HideInCallstack]
        public static void Error(string message, Object context = null, bool showStackTrace = true)
        {
            var path = GetFullPath(context);
            var pathPrefix = string.IsNullOrEmpty(path) ? "" : $" [{path}]";
            var formatted = $"<color={PINK}>{ERROR_ICON}</color> <b>realvirtual:</b>{pathPrefix} {message}";
            if (showStackTrace)
                Debug.LogError(formatted, context);
            else
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, context, "{0}", formatted);
        }
    }
}