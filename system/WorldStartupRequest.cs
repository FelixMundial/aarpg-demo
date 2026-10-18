namespace DAARPG.system;

public abstract record WorldStartupRequest
{
    private WorldStartupRequest()
    {
    }

    public sealed record NewGame : WorldStartupRequest;

    public sealed record LoadGame(
        string SaveSlotId) : WorldStartupRequest;
}
