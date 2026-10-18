using System;
using System.Collections.Generic;

namespace DAARPG.system.session;

/*
 * Runtime game state representation,
 *  containing character state alongside party, relationship, quest and Level progress
 */
public sealed class GameSession
{
    public Dictionary<string, LevelSession> LevelProgressions { get; } =
        new(StringComparer.Ordinal);

    public GameSession DeepCopy()
    {
        var copy = new GameSession();

        foreach (var (levelId, levelSession) in LevelProgressions)
        {
            copy.LevelProgressions.Add(levelId, levelSession.DeepCopy());
        }

        return copy;
    }

    public void RetainProgressionFrom(GameSession source)
    {
        var copy = source.DeepCopy();

        LevelProgressions.Clear();

        foreach (var (levelId, levelSession) in copy.LevelProgressions)
        {
            LevelProgressions.Add(levelId, levelSession);
        }
    }
}
