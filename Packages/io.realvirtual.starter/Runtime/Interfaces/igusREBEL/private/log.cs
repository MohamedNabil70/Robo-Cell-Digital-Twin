using UnityEngine;

namespace realvirtual
{
    public static class log
    {
        public static bool DebugEnabled;

        public static void Info(string message)
        {
            if (DebugEnabled)
                Logger.Message(message);
        }

        public static void Debug(string message)
        {
            if (DebugEnabled)
                Logger.Message(message);
        }

        public static void DebugFormat(string format, params object[] args)
        {
            if (DebugEnabled)
                Logger.Message(string.Format(format, args));
        }

        public static void InfoFormat(string format, params object[] args)
        {
            if (DebugEnabled)
                Logger.Message(string.Format(format, args));
        }

        public static void ErrorFormat(string format, params object[] args)
        {
            Logger.Error(string.Format(format, args));
        }
    }
}
