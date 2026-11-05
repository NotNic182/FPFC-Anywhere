using System.Collections.Generic;

namespace FpfcAnywhere.Core
{
    /// <summary>What the picker needs to know about one enabled camera.</summary>
    public readonly struct CameraCandidate
    {
        public readonly bool TaggedMainCamera;
        public readonly string SceneName;
        public readonly bool RendersToScreen;
        public readonly bool TargetsEyes;

        public CameraCandidate(bool taggedMainCamera, string sceneName, bool rendersToScreen, bool targetsEyes)
        {
            TaggedMainCamera = taggedMainCamera;
            SceneName = sceneName;
            RendersToScreen = rendersToScreen;
            TargetsEyes = targetsEyes;
        }
    }

    /// <summary>Finds the game's own camera among the cameras currently enabled.</summary>
    public static class GameCameraPicker
    {
        public const int None = -1;

        /// <summary>
        /// Chooses what the clone should follow once it exists. Multiplayer shows an untagged
        /// loading camera first and spawns the real rig later, so a tagged camera always replaces
        /// an untagged one. Otherwise the current camera stays, because a rebuild is not free.
        /// <paramref name="currentIndex"/> is <see cref="None"/> when the current camera has
        /// switched off, which is how the loading camera ends.
        /// </summary>
        public static int PickSource(IReadOnlyList<CameraCandidate> candidates, int currentIndex)
        {
            var best = Pick(candidates);
            if (currentIndex == None || best == None)
            {
                return currentIndex == None ? best : currentIndex;
            }

            var isUpgrade = candidates[best].TaggedMainCamera && !candidates[currentIndex].TaggedMainCamera;
            return isUpgrade ? best : currentIndex;
        }

        /// <summary>
        /// Returns the index of the game's camera, or <see cref="None"/>. The MainCamera tag is
        /// what solo play uses. The spectator rig could not be inspected offline, so an untagged
        /// camera that renders for the player's eyes is accepted as a fallback.
        /// </summary>
        public static int Pick(IReadOnlyList<CameraCandidate> candidates)
        {
            var fallback = None;

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!candidate.RendersToScreen || GameplayScenes.IsMenuScene(candidate.SceneName))
                {
                    continue;
                }

                if (candidate.TaggedMainCamera)
                {
                    return index;
                }

                if (candidate.TargetsEyes && fallback == None)
                {
                    fallback = index;
                }
            }

            return fallback;
        }
    }
}
