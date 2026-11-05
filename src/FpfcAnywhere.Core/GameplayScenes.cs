using System;
using System.Collections.Generic;
using System.Linq;

namespace FpfcAnywhere.Core
{
    /// <summary>Scene names read from 1.45.3 game code and confirmed in a live song.</summary>
    public static class GameplayScenes
    {
        private const string CoreScene = "GameCore";

        private static readonly string[] LevelScenes = { "StandardGameplay", "MultiplayerGameplay", "Tutorial" };

        private static readonly string[] MenuScenes = { "MainMenu", "GameInit" };

        /// <summary>
        /// True while a song is running. The menu stays loaded underneath a song, so its presence
        /// says nothing; a song needs both the core scene and a level scene.
        /// </summary>
        public static bool IsGameplay(IEnumerable<string> loadedScenes)
        {
            var scenes = loadedScenes.ToList();
            return scenes.Contains(CoreScene, StringComparer.Ordinal)
                && scenes.Any(scene => LevelScenes.Contains(scene, StringComparer.Ordinal));
        }

        public static bool IsMenuScene(string sceneName) => MenuScenes.Contains(sceneName, StringComparer.Ordinal);
    }
}
