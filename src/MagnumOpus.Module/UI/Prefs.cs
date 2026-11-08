namespace MagnumOpus.Module.UI
{
    /// <summary>The module's own saved preferences, read through the settings store on every access
    /// and written at once.</summary>
    internal static class Prefs
    {
        private const string BoardItemsStayKey = "board_items_stay";
        private const string ShortDirectionsKey = "short_directions";

        private static MagnumOpus.Contracts.ISettingsStore Store => ModuleMain.Host.Settings.Store;

        /// <summary>The board's , and . say an item and where it lies instead of moving the cursor
        /// to it (Backslash).</summary>
        public static bool BoardItemsStay
        {
            get => Store.GetBool(BoardItemsStayKey, false);
            set => Store.SetBool(BoardItemsStayKey, value);
        }

        /// <summary>Direction words are spoken short, "nw" for "northwest" (the mod menu).</summary>
        public static bool ShortDirections
        {
            get => Store.GetBool(ShortDirectionsKey, false);
            set => Store.SetBool(ShortDirectionsKey, value);
        }
    }
}
