using GitPulse.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>
/// The folder tree under the hotspots table. Each node shows its churn and share of the total,
/// colored from hot (red) to cold (grey). Folders that were cut off show "… N more".
/// </summary>
internal static class ChurnTreeRenderer
{
    /// <param name="width">Available width; long folder names are shortened from the left to fit.</param>
    public static IRenderable Render(ChurnNode root, int width)
    {
        if (root.Churn == 0)
        {
            return new Markup(Theme.Dim("No file changes."));
        }

        var tree = new Tree(Label(root, root.Churn, isRoot: true, maxName: int.MaxValue))
            .Guide(TreeGuide.Line)
            .Style(Theme.Border);

        AddChildren(tree, root, root.Churn, depth: 1, width);
        return tree;
    }

    private static void AddChildren(IHasTreeNodes parent, ChurnNode node, int total, int depth, int width)
    {
        // Guides take 4 columns per level; leave room for "  12,345 · 12.3%" and the panel border.
        var maxName = Math.Max(12, width - depth * 4 - 24);
        foreach (var child in node.Children)
        {
            var treeNode = parent.AddNode(Label(child, total, isRoot: false, maxName));
            AddChildren(treeNode, child, total, depth + 1, width);
        }

        if (node.HiddenChildren > 0)
        {
            parent.AddNode(new Markup(Theme.Dim(
                $"… {Format.Number(node.HiddenChildren)} more ({Format.Number(node.HiddenChurn)})")));
        }
    }

    private static Markup Label(ChurnNode node, int total, bool isRoot, int maxName)
    {
        var share = total == 0 ? 0 : (double)node.Churn / total;
        var name = node.IsFile || isRoot ? node.Name : node.Name + "/";
        if (name.Length > maxName)
        {
            name = Format.TruncatePath(name, maxName);
        }
        var nameMarkup = isRoot ? Theme.Title(name) : Theme.Colored(name, Theme.Heat(share));

        var details = isRoot
            ? $"{Format.Number(node.Churn)} lines changed in {Format.Plural(node.Files, "file")}"
            : $"{Format.Number(node.Churn)} · {Format.Percent(share * 100)}";

        return new Markup($"{nameMarkup}  {Theme.Dim(details)}");
    }
}
