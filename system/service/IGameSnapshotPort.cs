using System.Diagnostics.CodeAnalysis;
using DAARPG.system.session.snapshot;

namespace DAARPG.system.service;

public interface IGameSnapshotPort
{
    bool TryCapture(
        [NotNullWhen(true)] out WorldSnapshot? worldSnapshot,
        out string error);

    // todo async
    bool TryRestore(GameSnapshot gameSnapshot,
        out string error);
}
