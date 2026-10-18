using System;
using System.Collections.Generic;

namespace DAARPG.system.session;

public sealed class LevelSession
{
    public HashSet<string> PersistentFlags { get; } =
        new(StringComparer.Ordinal);

    public LevelSession DeepCopy()
    {
        var copy = new LevelSession();
        copy.PersistentFlags.UnionWith(PersistentFlags);
        return copy;
    }
}
