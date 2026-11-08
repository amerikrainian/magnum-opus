using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The end credits (CreditsScreen, and DlcCreditsScreen after the journal's last puzzles): timed
    /// cards the game fades in and out by itself, the words hardcoded in its render code, so they are
    /// read from what it draws (<see cref="TextCapture"/>). Each new card is spoken as it appears,
    /// queued so that nothing cuts the card before it off. Escape stays the game's: it skips only once
    /// the credits have been seen, and the screen closes by itself at the end.
    /// </summary>
    public abstract class CreditsGameScreenBase<T> : GameScreen<T> where T : class, IScreen
    {
        private T _seenScreen;
        private string _seenCard = "";

        protected CreditsGameScreenBase() { ReadsText.Add(typeof(T)); }

        public override string ScreenName => Strings.ScreenCredits;

        public override void Build(GraphBuilder b)
        {
            b.AddItem(ControlId.Structural("credits:card"), Nodes.Text(Card));
        }

        // The card on screen, name first (the game draws top to bottom); empty between the opening
        // delay and the first card, and after the last.
        private static string Card() =>
            string.Join(", ", TextCapture.LastFrame.Select(d => GameText.Clean(d.Text)).Where(t => !string.IsNullOrWhiteSpace(t)));

        public override void OnUpdate()
        {
            var g = Game;
            if (g == null) return;
            string card = Card();
            // A card already up when the screen opens is read by the landing.
            if (!ReferenceEquals(g, _seenScreen)) { _seenScreen = g; _seenCard = card; return; }
            if (card == _seenCard) return;
            _seenCard = card;
            if (card.Length > 0) Speech.Say(card, interrupt: false);
        }
    }

    public sealed class CreditsGameScreen : CreditsGameScreenBase<CreditsScreen>
    {
        public override string Key => "game.credits";
    }

    public sealed class DlcCreditsGameScreen : CreditsGameScreenBase<DlcCreditsScreen>
    {
        public override string Key => "game.credits.dlc";
    }
}
