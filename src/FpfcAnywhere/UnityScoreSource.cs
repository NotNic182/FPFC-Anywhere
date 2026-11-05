using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FpfcAnywhere.Core;
using Object = UnityEngine.Object;

namespace FpfcAnywhere
{
    /// <summary>
    /// Reads the score list behind the leaderboard in the game's spectator menu. It only ever calls
    /// getters on the game's own objects, so it can't change anything or send anything.
    /// </summary>
    internal sealed class UnityScoreSource : IScoreSource
    {
        private static readonly Type? ProviderType = Type.GetType(GameScoreNames.Provider + ", " + GameScoreNames.Assembly);
        private static readonly Type? PlayerType = ProviderType?.GetNestedType(GameScoreNames.Player);
        private static readonly PropertyInfo? RankedPlayers = ProviderType?.GetProperty(GameScoreNames.RankedPlayers);
        private static readonly PropertyInfo? UserName = PlayerType?.GetProperty(GameScoreNames.UserName);
        private static readonly PropertyInfo? Score = PlayerType?.GetProperty(GameScoreNames.Score);
        private static readonly PropertyInfo? IsFailed = PlayerType?.GetProperty(GameScoreNames.IsFailed);

        private Object? _provider;

        public bool TryRead(List<PlayerScore> players)
        {
            players.Clear();

            if (ProviderType == null || RankedPlayers == null || UserName == null || Score == null || IsFailed == null)
            {
                throw new MissingMemberException("The game's score list isn't where it should be");
            }

            if (_provider == null)
            {
                _provider = Object.FindAnyObjectByType(ProviderType);
            }

            if (_provider == null)
            {
                return false;
            }

            var ranked = (IList)RankedPlayers.GetValue(_provider);
            for (var index = 0; index < ranked.Count; index++)
            {
                var player = ranked[index];
                players.Add(new PlayerScore((string?)UserName.GetValue(player), (int)Score.GetValue(player), (bool)IsFailed.GetValue(player)));
            }

            return true;
        }
    }
}
