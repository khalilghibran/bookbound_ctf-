using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BookBound
{
    /// <summary>
    /// Owns the state machine and run state. The only script that drives state transitions
    /// (plan.md section 9).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState { Title, Playing, ShelfClearing, HeartLost, Paused, GameOver }

        private const float ShelfClearBannerSeconds = 1.5f; // plan.md section 13: 1.2-1.8s
        private const float HeartLostBannerSeconds = 1.2f;

        [SerializeField] private GenreDefinition[] genres = new GenreDefinition[ShelfGeometry.RowCount];
        [SerializeField] private DifficultyCurve difficultyCurve;
        [SerializeField] private ShelfController shelfController;
        [SerializeField] private BookQueue bookQueue;
        [SerializeField] private InfoPanelController infoPanel;
        [SerializeField] private ShelfTimer shelfTimer;
        [SerializeField] private HUDController hudController;
        [SerializeField] private ShellUI shellUI;
        [SerializeField] private OptionsPanelController optionsPanel;
        [SerializeField] private ScreenFader screenFader;
        [SerializeField] private GameObject bookPrefab;

        [Tooltip("0 = reseed randomly per run. Any other value makes every run reproducible.")]
        [SerializeField] private int seed;

        private ScoreService _score;
        private LivesService _lives;
        private System.Random _rng;
        private int _shelfIndex;
        private bool _optionsFromPause;
        private bool _shuffleUsedThisShelf;
        private int _shuffleSeed;
        private float _shuffleReadyAt;
        private string _playerInitials = "YOU";
        private bool _runStarted;
        private bool _runSubmitted;

        /// <summary>Chosen on the title screen (see ShellUI.OnPlay). Restart keeps using whatever was
        /// last selected rather than resetting to a default.</summary>
        private ShellUI.PlayDifficulty _difficulty = ShellUI.PlayDifficulty.Medium;

        public GameState State { get; private set; } = GameState.Title;

        private void Start()
        {
            shelfController.OnSlotBecameCorrect += HandleSlotBecameCorrect;
            shelfTimer.OnExpired += HandleTimerExpired;
            BookView.OnAnyBookMoved += HandleBookMoved;

            shellUI.OnPlay += HandlePlay;
            shellUI.OnResume += Resume;
            shellUI.OnRestart += BeginRun;
            shellUI.OnPauseRequested += Pause;
            shellUI.OnShuffleRequested += HandleShuffleRequested;
            shellUI.OnQuit += QuitGame;
            shellUI.OnOptionsRequested += OpenOptions;
            shellUI.OnExitToMenu += ExitToMenu;
            optionsPanel.OnClosed += CloseOptions;

            // Populate a board behind the title screen so the player sees the game, not an empty frame.
            BuildRunState();
            SpawnBookcase();
            EnterTitle();
        }

        private void OnDestroy()
        {
            BookView.OnAnyBookMoved -= HandleBookMoved;
            if (shelfController != null) shelfController.OnSlotBecameCorrect -= HandleSlotBecameCorrect;
            if (shelfTimer != null) shelfTimer.OnExpired -= HandleTimerExpired;
            if (shellUI != null) shellUI.OnShuffleRequested -= HandleShuffleRequested;
            if (_lives != null) _lives.OnGameOver -= HandleGameOver;
        }

        private void Update()
        {
            shellUI.SetShuffleAvailable(CanUseShuffle());

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            // Options is a modal over Title or Paused, not a state of its own. Esc has to back out of
            // it rather than fall through and resume a game the player can't even see.
            if (optionsPanel != null && optionsPanel.IsOpen)
            {
                optionsPanel.Hide();
                CloseOptions();
                return;
            }

            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }

        /// <summary>Options overlays whichever screen asked for it and restores that screen on close,
        /// so <paramref name="fromPause"/> is remembered rather than re-derived from State (which
        /// stays Paused either way).</summary>
        private void OpenOptions(bool fromPause)
        {
            _optionsFromPause = fromPause;
            if (fromPause) shellUI.HidePause();
            else shellUI.HideAll();
            optionsPanel.Show();
        }

        private void CloseOptions()
        {
            if (_optionsFromPause) shellUI.ShowPause();
            else shellUI.ShowTitle();
        }

        /// <summary>Abandons the run and goes back to the title. The partial score is submitted first
        /// so quitting out never costs the player a high score they'd already earned.</summary>
        private void ExitToMenu()
        {
            if (_score != null) SaveService.Submit(_score.Score, _shelfIndex + 1);
            SubmitRunToLeaderboard();

            StopAllCoroutines();
            shellUI.HideAll();
            BuildRunState();
            SpawnBookcase();
            EnterTitle();
        }

        private void BuildRunState()
        {
            if (_lives != null) _lives.OnGameOver -= HandleGameOver;

            _score = new ScoreService();
            _lives = new LivesService();
            _lives.OnGameOver += HandleGameOver;
            _rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
            _shelfIndex = 0;
            _runStarted = false;
            _runSubmitted = false;

            hudController.BindScore(_score);
            hudController.BindLives(_lives);
            hudController.SetBookcaseNumber(1);
        }

        private void EnterTitle()
        {
            State = GameState.Title;
            shelfTimer.Pause();
            shellUI.ShowTitle();
        }

        /// <summary>Title screen Play button: records which difficulty was picked, then hands off to
        /// the same BeginRun flow Restart also uses (Restart keeps re-using this same value).</summary>
        private void HandlePlay(ShellUI.PlayDifficulty difficulty)
        {
            _difficulty = difficulty;
            BeginRun();
        }

        /// <summary>Title -> game and game-over -> restart both hand off through a full-screen fade
        /// (plan.md section 12) so the board teardown/rebuild in StartRun never happens on-screen.</summary>
        private void BeginRun()
        {
            if (screenFader != null) screenFader.FadeOutIn(StartRun);
            else StartRun();
        }

        private void StartRun()
        {
            StopAllCoroutines();
            BuildRunState();
            _playerInitials = shellUI.PlayerInitials;
            _runStarted = true;
            shellUI.HideAll();
            SpawnBookcase();
            State = GameState.Playing;
        }

        private void Pause()
        {
            if (State != GameState.Playing) return;

            State = GameState.Paused;
            shelfTimer.Pause();
            shellUI.ShowPause();
        }

        private void Resume()
        {
            if (State != GameState.Paused) return;
            shellUI.HidePause();
            State = GameState.Playing;
            shelfTimer.Resume();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Generates and installs a bookcase for the current shelfIndex, with a fresh timer.</summary>
        private void SpawnBookcase()
        {
            // A book destroyed mid-drag (timer expiring while it's in the air) never runs OnEndDrag, so
            // its hover highlight would stay lit on the new bookcase. Clear the board's highlights here.
            foreach (IDropTarget target in DropTargetRegistry.All)
                target.SetHighlight(false);

            ShelfLayoutConfig config = difficultyCurve.GetLayoutConfig(_shelfIndex);
            ShelfType shelfType = ShelfTypeSchedule.ForShelfIndex(_shelfIndex);
            ShelfLayoutResult result = ShelfGenerator.Generate(config, genres, _rng.Next(), shelfType);
            _shuffleSeed = _rng.Next(); // reserved even when unused, so shuffling does not alter later shelves

            shelfController.Populate(result, bookPrefab);
            bookQueue.Populate(result.DealtBooks, bookPrefab);
            infoPanel.SetRowGenres(result.RowGenres);
            hudController.SetBookcaseNumber(_shelfIndex + 1, shelfType);
            _shuffleUsedThisShelf = false;
            _shuffleReadyAt = Time.unscaledTime;

            float duration = difficultyCurve.GetShelfDuration(_shelfIndex, result.RequiredMoves);
            duration = _difficulty switch
            {
                // Fixed 2 minutes per shelf, ignoring the DifficultyCurve entirely - generous and
                // constant regardless of how far the player has gotten. (A duration of 0 here used to
                // mean "no timer", but downstream code doesn't accept a non-positive shelfDuration -
                // that's what caused the ArgumentOutOfRangeException/freeze after clearing a shelf.)
                ShellUI.PlayDifficulty.Easy => 120f,
                ShellUI.PlayDifficulty.Medium => 90f,
                ShellUI.PlayDifficulty.Hard => duration * 0.7f,
                _ => duration,
            };
            shelfTimer.Reset(duration);
        }

        private void HandleShuffleRequested()
        {
            if (!CanUseShuffle()) return;
            if (!bookQueue.ShuffleRemaining(_shuffleSeed)) return;
            _shuffleUsedThisShelf = true;
            shellUI.SetShuffleAvailable(false);
        }

        private bool CanUseShuffle() => State == GameState.Playing && !_shuffleUsedThisShelf
            && Time.unscaledTime >= _shuffleReadyAt && bookQueue.CanShuffle;

        private void HandleSlotBecameCorrect()
        {
            _score.AddSlotFirstCorrect();
        }

        private void HandleBookMoved()
        {
            if (State != GameState.Playing) return;

            // OnAnyBookMoved fires when the pointer is released, before a returned or swapped book
            // finishes its position tween. Keep the visible table stable while it settles.
            var animations = AnimationSettings.Instance;
            var drag = animations.drag;
            _shuffleReadyAt = Time.unscaledTime + (animations.enabled
                ? Mathf.Max(drag.returnDuration, drag.swapDuration) : 0f);

            if (!shelfController.CheckState()) return;

            StartCoroutine(ShelfClearRoutine());
        }

        private IEnumerator ShelfClearRoutine()
        {
            State = GameState.ShelfClearing;
            shelfTimer.Pause();

            hudController.AnnounceBonusIncoming();
            int bonus = _score.AddTimeBonus(shelfTimer.TimeRemaining, shelfTimer.Duration, _shelfIndex);
            SaveService.Submit(_score.Score, _shelfIndex + 1);

            AudioManager.PlayShelfClear();
            // "BONUS n", not "+n" - the letter sheet has no plus sign (see ShellUI).
            shellUI.ShowBanner($"BOOKCASE CLEAR\nBONUS {bonus}");
            yield return new WaitForSeconds(ShelfClearBannerSeconds);
            shellUI.HideBanner();

            _shelfIndex++;
            SpawnBookcase();
            State = GameState.Playing;
        }

        private void HandleTimerExpired()
        {
            _lives.LoseHeart();
            if (_lives.IsGameOver) return; // HandleGameOver already ran off LivesService.OnGameOver

            StartCoroutine(HeartLostRoutine());
        }

        private IEnumerator HeartLostRoutine()
        {
            State = GameState.HeartLost;
            AudioManager.PlayHeartLost();
            shellUI.ShowBanner($"OUT OF TIME\n{_lives.Hearts} LEFT");
            yield return new WaitForSeconds(HeartLostBannerSeconds);
            shellUI.HideBanner();

            // Same shelfIndex, fresh full timer, score retained (plan.md section 8).
            SpawnBookcase();
            State = GameState.Playing;
        }

        private void HandleGameOver()
        {
            State = GameState.GameOver;
            shelfTimer.Pause();
            AudioManager.PlayHeartLost();
            SaveService.Submit(_score.Score, _shelfIndex + 1);
            SubmitRunToLeaderboard();
            shellUI.ShowGameOver(_score.Score, _shelfIndex);
        }

        private void SubmitRunToLeaderboard()
        {
            if (!_runStarted || _runSubmitted) return;
            _runSubmitted = true;
            SaveService.SubmitLeaderboardRun(_score.Score, _shelfIndex,
                _difficulty.ToString().ToUpperInvariant(), _playerInitials);
        }
    }
}
