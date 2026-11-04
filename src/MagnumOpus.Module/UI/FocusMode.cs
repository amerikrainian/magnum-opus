namespace MagnumOpus.Module.UI
{
    /// <summary>
    /// Whether the mod owns the keyboard (Ctrl+Shift+N toggles; on at launch unless the setting says
    /// otherwise). While on, the navigation keys a modeled screen claims are blanked from the game's
    /// own input queries (<see cref="Patches.GameKeys"/>); while off, the game gets every key and only
    /// the global keys stay ours.
    /// </summary>
    public static class FocusMode
    {
        public static bool Active { get; private set; }

        public static void Set(bool on) => Active = on;

        public static void Toggle() => Active = !Active;
    }
}
