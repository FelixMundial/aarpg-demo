using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DAARPG.persistence.domain;
using Godot;

namespace DAARPG.persistence.repository;

public sealed class SaveRepository : ISaveRepository
{
    // todo
    private const string SaveDirectory = "user://saves";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool TrySave(string slotId, GameSave gameSave,
        out string error)
    {
        GD.Print($"{nameof(SaveRepository)}: Writing game file...");
        var filePath = $"{SaveDirectory}/{slotId}.json";
        if (!DirAccess.DirExistsAbsolute(SaveDirectory))
        {
            var directoryError =
                DirAccess.MakeDirRecursiveAbsolute(SaveDirectory);

            if (directoryError != Error.Ok)
            {
                error = $"{nameof(SaveRepository)}: " +
                        $"Could not create directory {SaveDirectory}: {directoryError}";
                return false;
            }
        }

        var serializedGameSave =
            JsonSerializer.Serialize(gameSave, JsonOptions);
        if (string.IsNullOrWhiteSpace(serializedGameSave))
        {
            error = $"{nameof(SaveRepository)}: " +
                    $"No save data: {slotId}.";
            return false;
        }

        using var file = FileAccess.Open(
            filePath,
            FileAccess.ModeFlags.Write
        );

        if (file is null)
        {
            error = $"{nameof(SaveRepository)}: " +
                    $"Could not open {filePath}: {FileAccess.GetOpenError()}";
            return false;
        }

        if (!file.StoreString(serializedGameSave))
        {
            error = $"{nameof(SaveRepository)}: " +
                    $"Could not write {filePath}: {file.GetError()}";
            return false;
        }

        file.Flush();
        var writeError = file.GetError();
        if (writeError != Error.Ok)
        {
            error = $"{nameof(SaveRepository)}: " +
                    $"Could not finish writing {filePath}: {writeError}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryLoad(string slotId,
        [NotNullWhen(true)] out GameSave? gameSave, out string error)
    {
        GD.Print($"{nameof(SaveRepository)}: Reading game file...");
        var filePath = $"{SaveDirectory}/{slotId}.json";
        if (!FileAccess.FileExists(filePath))
        {
            error = $"{nameof(SaveRepository)}: " +
                    $"File {filePath} not found.";
            gameSave = null;
            return false;
        }

        var file = FileAccess.Open(
            filePath,
            FileAccess.ModeFlags.Read
        );
        if (file is null)
        {
            error = $"{nameof(SaveRepository)}: " +
                    $"Error reading save file: {filePath}.";
            gameSave = null;
            return false;
        }

        using (file)
        {
            var fileText = file.GetAsText();

            try
            {
                gameSave = JsonSerializer.Deserialize<GameSave>(
                    fileText,
                    JsonOptions
                );
            }
            catch (JsonException exception)
            {
                error = $"{nameof(SaveRepository)}: " +
                        $"Cannot deserialize save file: {slotId}: " +
                        exception.Message;
                gameSave = null;
                return false;
            }
        }

        if (gameSave is null)
        {
            error = $"{nameof(SaveRepository)}: " +
                    $"Empty Save: {slotId}.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
