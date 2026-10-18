using System.Diagnostics.CodeAnalysis;
using Godot;

namespace DAARPG.core.extensions;

public static class PackedSceneExtensions
{
    public static bool TryInstantiateScene<TNode>(
        this PackedScene scene,
        [NotNullWhen(true)] out TNode? instance,
        out string error)
        where TNode : Node
    {
        var root = scene.Instantiate();
        if (root is not TNode typedRoot)
        {
            root?.Free();
            instance = null;
            error = $"Scene '{scene.ResourcePath}' must have a " +
                    $"{typeof(TNode).Name} root.";
            return false;
        }

        instance = typedRoot;
        error = string.Empty;
        return true;
    }
}
