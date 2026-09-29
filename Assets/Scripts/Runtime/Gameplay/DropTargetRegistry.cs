using System.Collections.Generic;

namespace BookBound
{
    /// <summary>
    /// Self-registering list of every IDropTarget in the scene, so BookView can find the nearest drop
    /// target within snap radius without a scene-wide FindObjectsByType scan on every drag.
    /// </summary>
    public static class DropTargetRegistry
    {
        private static readonly List<IDropTarget> Targets = new List<IDropTarget>();

        public static void Register(IDropTarget target) => Targets.Add(target);
        public static void Unregister(IDropTarget target) => Targets.Remove(target);
        public static IReadOnlyList<IDropTarget> All => Targets;
    }
}
