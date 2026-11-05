using System;
using System.Collections.Generic;
using System.Linq;

namespace FpfcAnywhere.Core
{
    /// <summary>
    /// Decides whether a game controller may be moved to act as the mouse pointer. Other players
    /// receive the local head and hands, so moving those, or anything attached to them, would put
    /// the operator's aim into the match. Only menu controllers that are proven separate qualify.
    /// </summary>
    public static class PointerRules
    {
        private const string MenuSegment = "/MenuControllers/";
        private const string TrackedRigSegment = "/Origin/VRGameCore/";
        private const char SceneSeparator = ':';
        private const char PathSeparator = '/';

        /// <param name="controllerPath">Full hierarchy path, starting with "Scene:".</param>
        /// <param name="sentPaths">Paths of every head and hand. Empty means unknown, so it refuses.</param>
        public static bool IsSafe(string controllerPath, IReadOnlyCollection<string> sentPaths)
        {
            if (sentPaths.Count == 0
                || !controllerPath.Contains(MenuSegment)
                || controllerPath.Contains(TrackedRigSegment)
                || GameplayScenes.IsMenuScene(SceneOf(controllerPath)))
            {
                return false;
            }

            return !sentPaths.Any(sent => IsSameOrRelated(controllerPath, sent));
        }

        private static bool IsSameOrRelated(string first, string second)
            => first == second
                || first.StartsWith(second + PathSeparator, StringComparison.Ordinal)
                || second.StartsWith(first + PathSeparator, StringComparison.Ordinal);

        private static string SceneOf(string path)
        {
            var end = path.IndexOf(SceneSeparator);
            return end < 0 ? string.Empty : path.Substring(0, end);
        }
    }
}
