using DAARPG.persistence.repository;
using DAARPG.system.service;

namespace DAARPG.system.bootstrap;

public static class GameComposition
{
    public static GameSaveService CreateGameSaveService()
    {
        ISaveRepository repository = new SaveRepository();

        return new GameSaveService(repository);
    }
}
