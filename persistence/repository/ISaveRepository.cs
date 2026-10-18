using System.Diagnostics.CodeAnalysis;
using DAARPG.persistence.domain;

namespace DAARPG.persistence.repository;

public interface ISaveRepository
{
    bool TrySave(string slotId, GameSave save, out string error);

    // todo async
    bool TryLoad(string slotId,
        [NotNullWhen(true)] out GameSave? save, out string error);
}
