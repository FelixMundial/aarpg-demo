using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DAARPG.core.enums;
using DAARPG.levels.controller;
using DAARPG.persistence.domain;
using DAARPG.persistence.repository;
using DAARPG.player.enums;
using DAARPG.system.session;
using DAARPG.system.session.snapshot;
using Godot;

namespace DAARPG.system.service;

public class GameSaveService(ISaveRepository saveRepository)
{
    public bool TrySaveGame(string saveSlotId,
        GameSession gameSession,
        IGameSnapshotPort gameSnapshotPort,
        out string error)
    {
        GD.Print($"{nameof(GameSaveService)}: Saving game...");
        // Generate game snapshot.
        if (!gameSnapshotPort.TryCapture(
                out var worldSnapshot,
                out var snapshotError))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Error saving game: {snapshotError}";
            return false;
        }

        // Map to game save.
        var gameSave = MapToGameSave(worldSnapshot, gameSession);

        if (!saveRepository.TrySave(saveSlotId, gameSave,
                out var saveError))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Error saving game: {saveError}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryLoadGame(string saveSlotId,
        GameSession gameSession,
        IGameSnapshotPort gameSnapshotPort,
        out string error)
    {
        GD.Print($"{nameof(GameSaveService)}: Loading game...");
        if (!saveRepository.TryLoad(saveSlotId,
                out var gameSave,
                out var loadError))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Error loading game save: {loadError}";
            return false;
        }

        // Validate game save.
        if (!TryValidateGameSave(gameSave,
                out var validatedGameSave,
                out var validationError))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Error loading game save: {validationError}";
            return false;
        }

        // Map to game snapshot.
        if (!TryMapToGameSnapshot(validatedGameSave,
                out var gameSnapshot, out var mappingError))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Error loading game save: {mappingError}";
            return false;
        }

        // Restore 
        if (!gameSnapshotPort.TryRestore(gameSnapshot,
                out var restorationError))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Error loading game save: {restorationError}";
            return false;
        }

        error = string.Empty;
        // Commit the candidate session only after world restoration succeeds.
        gameSession.RetainProgressionFrom(gameSnapshot.GameSession);
        return true;
    }

    public bool TryReadGame(string saveSlotId,
        [NotNullWhen(true)] out GameSnapshot? gameSnapshot,
        out string error)
    {
        GD.Print($"{nameof(GameSaveService)}: Previewing game...");
        error = string.Empty;
        gameSnapshot = null;
        return false;
    }

    private static GameSave MapToGameSave(
        WorldSnapshot worldSnapshot, GameSession gameSession)
    {
        var playerSnapshot = worldSnapshot.PlayerSnapshot;

        var playerSave = new PlayerSave
        {
            Health = playerSnapshot.Health,
            MaxHealth = playerSnapshot.MaxHealth,
            LifeState = playerSnapshot.LifeState,
            PositionX = playerSnapshot.PositionX,
            PositionY = playerSnapshot.PositionY,
            FacingDirection = playerSnapshot.FacingDirection
        };

        var levelSaves = GetLevelSaves(gameSession);

        var gameSave = new GameSave
        {
            SchemaVersion = GameSave.CurrentSchemaVersion,
            CurrentLevelId = worldSnapshot.CurrentLevelId,
            PlayerSave = playerSave,
            LevelSaves = levelSaves! // todo type mismatch
        };
        return gameSave;
    }

    private static Dictionary<string, LevelSave> GetLevelSaves(GameSession gameSession)
    {
        var levelSaves = gameSession.LevelProgressions
            .ToDictionary(
                entry => entry.Key,
                entry => new LevelSave
                {
                    PersistentFlags =
                    [
                        .. entry.Value.PersistentFlags
                            .OrderBy(flag => flag, StringComparer.Ordinal)
                    ]
                },
                StringComparer.Ordinal);
        return levelSaves;
    }

    private static bool TryMapToGameSnapshot(GameSave gameSave,
        [NotNullWhen(true)] out GameSnapshot? gameSnapshot,
        out string error)
    {
        gameSnapshot = null;

        var playerSave = gameSave.PlayerSave!;
        var playerSnapshot = new PlayerSnapshot(
            playerSave.Health,
            playerSave.MaxHealth,
            playerSave.LifeState!,
            playerSave.PositionX,
            playerSave.PositionY,
            playerSave.FacingDirection!);
        var worldSnapshot = new WorldSnapshot(
            gameSave.CurrentLevelId!, playerSnapshot);

        var gameSession = new GameSession();
        if (!TryPopulateLevelProgressions(gameSave, gameSession,
                out var populationError))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Mapping game save to snapshot failed: {populationError}";
            return false;
        }

        error = string.Empty;
        gameSnapshot = new GameSnapshot(worldSnapshot, gameSession);
        return true;
    }

    private static bool TryPopulateLevelProgressions(
        GameSave gameSave, GameSession gameSession,
        out string error)
    {
        foreach (var (levelId, levelSave) in gameSave.LevelSaves!)
        {
            if (string.IsNullOrWhiteSpace(levelId) ||
                levelSave?.PersistentFlags is null)
            {
                error = $"{nameof(GameSaveService)}: " +
                        $"LevelSaves contains invalid data.";
                return false;
            }

            var levelSession = new LevelSession();

            foreach (var flag in levelSave.PersistentFlags)
            {
                if (string.IsNullOrWhiteSpace(flag))
                {
                    error = $"{nameof(GameSaveService)}: " +
                            $"Level '{levelId}' contains an invalid flag.";
                    return false;
                }

                levelSession.PersistentFlags.Add(flag);
            }

            gameSession.LevelProgressions.Add(levelId, levelSession);
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateGameSave(GameSave gameSave,
        [NotNullWhen(true)] out GameSave? validatedGameSave,
        out string error)
    {
        GD.Print($"{nameof(GameSaveService)}: Validating game save...");
        validatedGameSave = null;

        if (gameSave.SchemaVersion != GameSave.CurrentSchemaVersion)
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Unsupported save schema version {gameSave.SchemaVersion}.";
            return false;
        }

        var currentLevelId = gameSave.CurrentLevelId;
        if (string.IsNullOrWhiteSpace(currentLevelId))
        {
            error = $"{nameof(GameSaveService)}: " +
                    $"Invalid current Level identifier.";
            return false;
        }

        if (gameSave.PlayerSave is not { } playerSave)
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Player data is empty.";
            return false;
        }

        if (!TryValidatePlayerSave(playerSave,
                out var validatedPlayerSave,
                out var playerValidationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Player save validation failed: {playerValidationError}";
            return false;
        }

        if (gameSave.LevelSaves is not { } levelSaves)
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Level data is empty.";
            return false;
        }

        if (!TryValidateLevelSaves(levelSaves,
                out var validatedLevelSaves,
                out var levelValidationError))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Player save validation failed: {levelValidationError}";
            return false;
        }

        error = string.Empty;
        validatedGameSave = new GameSave
        {
            CurrentLevelId = gameSave.CurrentLevelId,
            PlayerSave = validatedPlayerSave,
            LevelSaves = validatedLevelSaves
        };
        return true;
    }

    private static bool TryValidatePlayerSave(PlayerSave playerSave,
        [NotNullWhen(true)] out PlayerSave? validatedPlayerSave,
        out string error)
    {
        validatedPlayerSave = null;

        var savedLifeState = playerSave.LifeState;
        if (savedLifeState == nameof(LifeState.Dead) ||
            Mathf.IsEqualApprox(playerSave.Health, 0f))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Player is already dead.";
            return false;
        }

        if (!Enum.TryParse<LifeState>(
                savedLifeState,
                out var lifeState) ||
            !Enum.IsDefined(lifeState))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Invalid player life state: {savedLifeState}.";
            return false;
        }

        var savedFacingDirection = playerSave.FacingDirection;
        if (!Enum.TryParse<CardinalDirection>(
                savedFacingDirection,
                out var facingDirection) ||
            !Enum.IsDefined(facingDirection))
        {
            error = $"{nameof(LevelLifecycleController)}: " +
                    $"Invalid player facing direction: {savedFacingDirection}.";
            return false;
        }

        error = string.Empty;
        validatedPlayerSave = playerSave;
        return true;
    }

    private static bool TryValidateLevelSaves(Dictionary<string, LevelSave?> levelSaves,
        [NotNullWhen(true)] out Dictionary<string, LevelSave?>? validatedLevelSaves,
        out string error)
    {
        validatedLevelSaves = [];
        // todo sufficient?
        foreach (var keyValuePair in levelSaves)
        {
            if (keyValuePair.Key is null || keyValuePair.Value is null)
            {
                error = $"Invalid level save entry " +
                        $"[{keyValuePair.Key}: {keyValuePair.Key}].";
                return false;
            }

            validatedLevelSaves.Add(keyValuePair.Key, keyValuePair.Value);
        }

        error = string.Empty;
        return true;
    }
}
