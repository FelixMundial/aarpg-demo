using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DAARPG.levels.metadata;
using Godot;

namespace DAARPG.levels.registry;

public sealed class LevelRegistry
{
    private readonly Dictionary<string, ResolvedLevelDefinition> _resolvedLevelDefinitions =
        new(StringComparer.Ordinal);

    public LevelRegistry(LevelCatalog levelCatalog)
    {
        ArgumentNullException.ThrowIfNull(levelCatalog);

        foreach (var levelDefinition in levelCatalog.LevelDefinitions)
        {
            if (levelDefinition is null)
            {
                throw new InvalidOperationException(
                    "LevelCatalog contains a null definition.");
            }

            if (string.IsNullOrWhiteSpace(levelDefinition.Id) ||
                levelDefinition.Id != levelDefinition.Id.Trim())
            {
                throw new InvalidOperationException(
                    "Every LevelDefinition must have a non-empty, " +
                    "trimmed ID.");
            }

            if (string.IsNullOrWhiteSpace(levelDefinition.SceneResourceReference))
            {
                throw new InvalidOperationException(
                    $"Level '{levelDefinition.Id}' has no scene reference stored.");
            }

            var id = levelDefinition.Id;
            if (_resolvedLevelDefinitions.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate Level ID '{id}'.");
            }

            var scenePath = levelDefinition.SceneResourceReference.Trim();
            if (scenePath.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Level '{id}' has no scene reference stored.");
            }

            var scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene is null)
            {
                throw new InvalidOperationException(
                    $"Level '{id}' scene reference '{scenePath}' could not be loaded as a PackedScene.");
            }

            var resolvedDefinition =
                new ResolvedLevelDefinition(levelDefinition, scene);

            _resolvedLevelDefinitions.Add(id, resolvedDefinition);
        }

        if (_resolvedLevelDefinitions.Count == 0)
        {
            throw new InvalidOperationException(
                "LevelCatalog contains no definitions.");
        }
    }

    public bool TryResolveLevelById(
        string levelId,
        [NotNullWhen(true)] out ResolvedLevelDefinition? resolvedLevelDefinition,
        out string error)
    {
        resolvedLevelDefinition = null;

        if (string.IsNullOrWhiteSpace(levelId))
        {
            error = "Level ID is empty.";
            return false;
        }

        if (!_resolvedLevelDefinitions.TryGetValue(levelId,
                out resolvedLevelDefinition))
        {
            error = $"Unknown Level ID '{levelId}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
