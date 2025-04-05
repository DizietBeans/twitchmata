using UnityEngine;

namespace Twitchmata { 
    internal class Logger {
        internal static TwitchManager TwitchManager;
        internal static string version = "1.3.3";

        internal static Logger Instance = new Logger();

        internal static void LogInfo(string log) {
            if (Logger.TwitchManager.LogLevel == LogLevel.Info) {
                Debug.Log("[TWITCHMATA][" + version + "] " + log);
            }
        }

        internal static void LogWarning(string log) {
            if ((int)Logger.TwitchManager.LogLevel >= (int)LogLevel.Warning) {
                Debug.LogWarning("[TWITCHMATA][" + version + "] " + log);
            }
        }

        internal static void LogError(string log) {
            if ((int)Logger.TwitchManager.LogLevel >= (int)LogLevel.Error) {
                Debug.LogError("[TWITCHMATA][" + version + "] " + log);
            }
        }
    }

    public enum LogLevel {
        None = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
    }
}

