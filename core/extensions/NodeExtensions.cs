using System.Collections.Generic;
using Godot;

namespace DAARPG.core.extensions;

public static class NodeExtensions
{
    public static T? TryGetNode<T>(this Node node, NodePath path) where T : Node
    {
        if (!node.HasNode(path))
        {
            return null;
        }

        return node.GetNode<T>(path);
    }

    /*
     * 遍历所有该类型的后代节点
     */
    public static IEnumerable<T> GetDescendantsOfType<T>(
        this Node root)
        where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in
                     child.GetDescendantsOfType<T>())
            {
                yield return nested;
            }
        }
    }
}
