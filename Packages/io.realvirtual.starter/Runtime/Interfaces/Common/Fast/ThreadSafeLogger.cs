// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    //! Thread-safe logging system for background threads to safely log to Unity main thread
    public static class ThreadSafeLogger
    {
        //! Log severity levels
        public enum LogLevel
        {
            Info,    //!< Informational message
            Warning, //!< Warning message
            Error    //!< Error message
        }
        
        //! Log entry structure containing all log information
        public struct LogEntry
        {
            public DateTime Timestamp; //!< When the log entry was created
            public LogLevel Level;     //!< Severity level of the log entry
            public string Message;     //!< Log message content
            public string Source;      //!< Source component or class name
            
            //! Creates a new log entry with timestamp
            public LogEntry(LogLevel level, string message, string source = null)
            {
                Timestamp = DateTime.Now;
                Level = level;
                Message = message;
                Source = source ?? "Unknown";
            }
        }
        
        // Thread-safe queue for background threads to add log entries
        private static readonly ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();
        
        // Maximum number of log entries to keep in memory
        private const int MaxLogEntries = 1000;
        
        // Statistics
        private static int _totalLogsProcessed = 0;
        private static int _logsDropped = 0;
        
        //! Adds info log entry from background thread
        public static void LogInfo(string message, string source = null)
        {
            EnqueueLogEntry(new LogEntry(LogLevel.Info, message, source));
        }
        
        //! Adds warning log entry from background thread
        public static void LogWarning(string message, string source = null)
        {
            EnqueueLogEntry(new LogEntry(LogLevel.Warning, message, source));
        }
        
        //! Adds error log entry from background thread
        public static void LogError(string message, string source = null)
        {
            EnqueueLogEntry(new LogEntry(LogLevel.Error, message, source));
        }

        //! Checks if debug logging is enabled (for performance optimization)
        public static bool IsDebugEnabled()
        {
            // For now always return true during debugging
            // In production this could check a global debug flag
            return true;
        }
        
        //! Processes all queued log entries on Unity main thread
        public static void ProcessLogQueue()
        {
            if (!Application.isPlaying)
                return;
                
            int processedThisFrame = 0;
            const int maxLogsPerFrame = 10; // Limit to prevent frame drops
            
            while (_logQueue.TryDequeue(out var logEntry) && processedThisFrame < maxLogsPerFrame)
            {
                DisplayLogEntry(logEntry);
                _totalLogsProcessed++;
                processedThisFrame++;
            }
            
            // If queue is getting too large, drop some entries to prevent memory issues
            if (_logQueue.Count > MaxLogEntries)
            {
                var dropCount = _logQueue.Count - MaxLogEntries;
                for (int i = 0; i < dropCount && _logQueue.TryDequeue(out _); i++)
                {
                    _logsDropped++;
                }
                
                Logger.Warning($"ThreadSafeLogger: Dropped {dropCount} log entries due to queue overflow. Total dropped: {_logsDropped}");
            }
        }
        
        //! Gets current queue statistics
        public static (int queueSize, int totalProcessed, int dropped) GetStatistics()
        {
            return (_logQueue.Count, _totalLogsProcessed, _logsDropped);
        }
        
        //! Clears all queued log entries
        public static void ClearQueue()
        {
            while (_logQueue.TryDequeue(out _)) { }
        }
        
        private static void EnqueueLogEntry(LogEntry entry)
        {
            if (_logQueue.Count < MaxLogEntries * 2)
            {
                _logQueue.Enqueue(entry);
            }
            else
            {
                _logsDropped++;
                Console.WriteLine($"[ThreadSafeLogger OVERFLOW] {entry.Level}: {entry.Message}");
            }
        }
        
        private static void DisplayLogEntry(LogEntry entry)
        {
            var timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");
            var formattedMessage = $"[{timestamp}] [{entry.Source}] {entry.Message}";
            
            switch (entry.Level)
            {
                case LogLevel.Info:
                    Logger.Log(formattedMessage,null,true);
                    break;
                case LogLevel.Warning:
                    Logger.Warning(formattedMessage,null,true);
                    break;
                case LogLevel.Error:
                    Logger.Error(formattedMessage,null,true);
                    break;
            }
        }
        
        //! Logs info message only if condition is true
        public static void LogInfoIf(bool condition, string message, string source = null)
        {
            if (condition)
                LogInfo(message, source);
        }
        
        //! Logs warning message only if condition is true
        public static void LogWarningIf(bool condition, string message, string source = null)
        {
            if (condition)
                LogWarning(message, source);
        }
        
        //! Logs error message only if condition is true
        public static void LogErrorIf(bool condition, string message, string source = null)
        {
            if (condition)
                LogWarning(message, source);
        }
        
        //! Formats log message with cycle information for interfaces
        public static void LogCycle(LogLevel level, int cycle, string message, string source = null)
        {
            var cycleMessage = $"Cycle {cycle}: {message}";
            
            switch (level)
            {
                case LogLevel.Info:
                    LogInfo(cycleMessage, source);
                    break;
                case LogLevel.Warning:
                    LogWarning(cycleMessage, source);
                    break;
                case LogLevel.Error:
                    LogError(cycleMessage, source);
                    break;
            }
        }
    }
}