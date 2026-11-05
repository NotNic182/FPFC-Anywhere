using System;
using System.IO;
using UnityEngine;

namespace FpfcAnywhere
{
    /// <summary>Writes to Logs/FpfcAnywhere.log beside the game, and to Unity's own log.</summary>
    internal static class ModLog
    {
        private static string? _path;

        public static void Start()
        {
            var logs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs");
            Directory.CreateDirectory(logs);
            _path = Path.Combine(logs, "FpfcAnywhere.log");
            File.WriteAllText(_path, string.Empty);
        }

        public static void Info(string message)
        {
            Debug.Log("[FpfcAnywhere] " + message);
            Append(message);
        }

        public static void Error(string message, Exception exception)
        {
            Debug.LogError($"[FpfcAnywhere] {message}: {exception}");
            Append($"ERROR {message}: {exception}");
        }

        private static void Append(string message)
        {
            try
            {
                if (_path != null)
                {
                    File.AppendAllText(_path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
            }
            catch (IOException)
            {
                // A locked or full log must never interrupt a live match.
            }
        }
    }
}
