namespace MagnumOpus.Module.UI
{
    /// <summary>The module's own saved preferences, read through the settings store on every access
    /// and written at once.</summary>
    internal static class Prefs
    {
        private const string BoardItemsStayKey = "board_items_stay";
        private const string ShortDirectionsKey = "short_directions";
        private const string InterruptWhilePlayingKey = "interrupt_while_playing";
        private const string ArmChangeToEndKey = "arm_change_to_end";

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

        /// <summary>While a run plays, each cycle's first message cuts off the last cycle's, instead
        /// of everything queueing (the mod menu).</summary>
        public static bool InterruptWhilePlaying
        {
            get => Store.GetBool(InterruptWhilePlayingKey, false);
            set => Store.SetBool(InterruptWhilePlayingKey, value);
        }

        /// <summary>Changing the program cursor's arm (the tape's and the board's Up / Down) puts the
        /// cursor on the cell after the arm's last instruction; off, it keeps its cycle (the mod menu,
        /// user request 2026-11-09).</summary>
        public static bool ArmChangeToEnd
        {
            get => Store.GetBool(ArmChangeToEndKey, false);
            set => Store.SetBool(ArmChangeToEndKey, value);
        }
    }
}
