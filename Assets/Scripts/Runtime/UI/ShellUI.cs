using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BookBound
{
    /// <summary>
    /// Title / pause / game over / transition banner (plan.md sections 6 and 12 phase 8). Owns no game
    /// state: it raises intent events and GameManager decides what they mean. Each panel is a
    /// full-screen raycast blocker, which is also what stops drags while the game isn't in Playing.
    /// Open/close motion (backdrop fade, pop-in, staggered contents) lives on each panel's
    /// AnimatedPanel component, not here.
    ///
    /// All text renders through SpriteText off the BookBound letter sheet. That sheet is uppercase
    /// with no punctuation, so copy here is written to fit it - see the two reworded strings below.
    /// </summary>
    public class ShellUI : MonoBehaviour
    {
        /// <summary>Chosen on the title screen before a run starts. See GameManager.SpawnBookcase
        /// for the three timer budgets.</summary>
        public enum PlayDifficulty { Easy, Medium, Hard }

        [SerializeField] private AnimatedPanel titlePanel;
        [SerializeField] private AnimatedPanel pausePanel;
        [SerializeField] private AnimatedPanel gameOverPanel;
        [SerializeField] private AnimatedPanel leaderboardPanel;
        [SerializeField] private AnimatedPanel banner;

        [SerializeField] private SpriteText titleBestText;
        [SerializeField] private SpriteText gameOverText;
        [SerializeField] private SpriteText leaderboardRowsText;
        [SerializeField] private SpriteText bannerText;

        [Tooltip("Three separate Play buttons on the title screen, one per difficulty. Duplicate the " +
                 "existing Play button in the scene twice and relabel each - no new art needed.")]
        [SerializeField] private Button playEasyButton;
        [SerializeField] private Button playMediumButton;
        [SerializeField] private Button playHardButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button titleLeaderboardButton;
        [SerializeField] private Button gameOverLeaderboardButton;
        [SerializeField] private Button gameOverMainMenuButton;
        [SerializeField] private Button leaderboardBackButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button shuffleButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button titleOptionsButton;
        [SerializeField] private Button pauseOptionsButton;
        [SerializeField] private Button exitToMenuButton;

        /// <summary>Fired when any of the three title-screen Play buttons is clicked, carrying which
        /// difficulty was chosen.</summary>
        public event Action<PlayDifficulty> OnPlay;
        public event Action OnResume;
        public event Action OnRestart;
        public event Action OnPauseRequested;
        public event Action OnShuffleRequested;
        public event Action OnQuit;

        /// <summary>Raised with true when opened from the pause menu, false from the title - the
        /// caller has to know which screen to put back afterwards.</summary>
        public event Action<bool> OnOptionsRequested;

        public event Action OnExitToMenu;

        private string _playerInitials;
        private bool _leaderboardOpen;
        private bool _leaderboardFromGameOver;

        public string PlayerInitials => string.IsNullOrEmpty(_playerInitials) ? "YOU" : _playerInitials;

        private void Awake()
        {
            _playerInitials = SaveService.LastInitials;
            if (string.IsNullOrEmpty(_playerInitials)) _playerInitials = "YOU";

            // The committed scene predates the generated Shuffle button. Reuse an existing art plate
            // there until SceneSetup next rebuilds it; generated scenes wire the button directly.
            EnsureShuffleButton();
            NormalizeDifficultyButtons();
            EnsureDifficultyLabels();
            EnsureLeaderboardUI();

            playEasyButton.onClick.AddListener(() => ClickPlay(PlayDifficulty.Easy));
            playMediumButton.onClick.AddListener(() => ClickPlay(PlayDifficulty.Medium));
            playHardButton.onClick.AddListener(() => ClickPlay(PlayDifficulty.Hard));
            resumeButton.onClick.AddListener(() => Click(OnResume));
            restartButton.onClick.AddListener(() => Click(OnRestart));
            titleLeaderboardButton.onClick.AddListener(() => OpenLeaderboard(false));
            gameOverLeaderboardButton.onClick.AddListener(() => OpenLeaderboard(true));
            gameOverMainMenuButton.onClick.AddListener(() => Click(OnExitToMenu));
            leaderboardBackButton.onClick.AddListener(BackFromLeaderboard);
            pauseButton.onClick.AddListener(() => Click(OnPauseRequested));
            shuffleButton.onClick.AddListener(() => Click(OnShuffleRequested));
            quitButton.onClick.AddListener(() => Click(OnQuit));
            exitToMenuButton.onClick.AddListener(() => Click(OnExitToMenu));

            titleOptionsButton.onClick.AddListener(() =>
            {
                AudioManager.PlayUiClick();
                OnOptionsRequested?.Invoke(false);
            });
            pauseOptionsButton.onClick.AddListener(() =>
            {
                AudioManager.PlayUiClick();
                OnOptionsRequested?.Invoke(true);
            });

            HideAll();
            SetShuffleAvailable(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_leaderboardOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                BackFromLeaderboard();
                return;
            }
        }

        public void SetShuffleAvailable(bool available)
        {
            if (shuffleButton != null) shuffleButton.interactable = available;
        }

        private void EnsureShuffleButton()
        {
            if (shuffleButton != null) return;

            var go = Instantiate(resumeButton.gameObject, transform);
            go.name = "ShuffleButton";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(260f, 230f);
            rt.sizeDelta = new Vector2(440f, 88f);
            rt.SetAsFirstSibling(); // title, pause, and game-over panels cover it

            SpriteText label = go.GetComponentInChildren<SpriteText>(true);
            if (label != null)
            {
                ((RectTransform)label.transform).sizeDelta = new Vector2(310f, 88f);
                label.SetText("SHUFFLE TABLE");
            }
            foreach (Transform child in rt)
            {
                if (child.name != "Sparkle") continue;
                var sparkle = (RectTransform)child;
                sparkle.anchoredPosition = new Vector2(Mathf.Sign(sparkle.anchoredPosition.x) * 174f, 0f);
            }

            shuffleButton = go.GetComponent<Button>();
            shuffleButton.onClick.RemoveAllListeners();
        }

        private void EnsureDifficultyLabels()
        {
            // Older committed scenes have three START plates but no visible mode names. A newly
            // generated scene already includes these labels beside each plate.
            SpriteText source = resumeButton.GetComponentInChildren<SpriteText>(true);
            if (source == null) return;
            EnsureDifficultyLabel(playEasyButton, "EASY", source);
            EnsureDifficultyLabel(playMediumButton, "MEDIUM", source);
            EnsureDifficultyLabel(playHardButton, "HARD", source);
        }

        private void NormalizeDifficultyButtons()
        {
            // Normalize the menu rhythm before adding labels. START gets a small emphasis while the
            // remaining rows share one size; all controls stay centered and clear of adjacent rows.
            NormalizeDifficultyButton(playEasyButton, new Vector2(0f, 90f), new Vector2(500f, 78f));
            NormalizeDifficultyButton(playMediumButton, Vector2.zero);
            NormalizeDifficultyButton(playHardButton, new Vector2(0f, -90f));
            NormalizeDifficultyButton(titleOptionsButton, new Vector2(0f, -180f));
            NormalizeDifficultyButton(quitButton, new Vector2(0f, -270f));
        }

        private static void NormalizeDifficultyButton(Button button, Vector2 position, Vector2? size = null)
        {
            if (button == null) return;
            RectTransform rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            // The title rows are 90 px apart. Keep the artwork shorter than that rhythm so every
            // plate has a real gap instead of touching or overlapping its neighbours.
            rt.sizeDelta = size ?? new Vector2(460f, 72f);

            if (button.targetGraphic is Image image) image.preserveAspect = false;
        }

        private static void EnsureDifficultyLabel(Button button, string label, SpriteText source)
        {
            if (button.GetComponentInChildren<SpriteText>(true) != null) return;
            var go = Instantiate(source.gameObject, button.transform);
            go.name = "DifficultyLabel";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Use one shared x coordinate. Legacy scenes have different button widths; deriving this
            // from each width puts EASY farther right than MEDIUM and HARD.
            rt.anchoredPosition = new Vector2(340f, 0f);
            rt.sizeDelta = new Vector2(160f, 60f);
            go.GetComponent<SpriteText>().SetText(label);
        }

        private void EnsureLeaderboardUI()
        {
            // The committed scene is older than these controls. Clone its existing art and panel
            // components so this feature works immediately, without hand-editing scene YAML.
            Transform titleContent = titleBestText.transform.parent;
            Transform gameOverContent = gameOverText.transform.parent;

            RemoveTitleInitialsUI(titleContent);

            if (leaderboardPanel == null)
            {
                var panelGo = Instantiate(gameOverPanel.gameObject, transform);
                panelGo.name = "LeaderboardPanel";
                panelGo.transform.SetAsLastSibling();
                leaderboardPanel = panelGo.GetComponent<AnimatedPanel>();
                leaderboardRowsText = panelGo.transform.Find("Content/Results").GetComponent<SpriteText>();
                leaderboardBackButton = panelGo.transform.Find("Content/RestartButton").GetComponent<Button>();

                RectTransform rowsRt = (RectTransform)leaderboardRowsText.transform;
                rowsRt.anchoredPosition = new Vector2(0f, 65f);
                rowsRt.sizeDelta = new Vector2(1600f, 680f);
                leaderboardRowsText.CapHeight = 23f;
                leaderboardRowsText.SetText("LEADERBOARD");

                RectTransform backRt = (RectTransform)leaderboardBackButton.transform;
                backRt.anchoredPosition = new Vector2(0f, -350f);
                leaderboardBackButton.GetComponentInChildren<SpriteText>(true).SetText("BACK");
                leaderboardBackButton.onClick.RemoveAllListeners();
            }

            if (titleLeaderboardButton == null)
                titleLeaderboardButton = ClonePlate(resumeButton, titleContent, "TitleLeaderboardButton",
                    "LEADERBOARD", new Vector2(0f, -360f), 440f);

            if (gameOverLeaderboardButton == null || gameOverMainMenuButton == null)
            {
                ((RectTransform)restartButton.transform).anchoredPosition = new Vector2(0f, -160f);
                if (gameOverLeaderboardButton == null)
                    gameOverLeaderboardButton = ClonePlate(restartButton, gameOverContent,
                        "GameOverLeaderboardButton", "LEADERBOARD", new Vector2(-250f, -280f), 440f);
                if (gameOverMainMenuButton == null)
                    gameOverMainMenuButton = ClonePlate(restartButton, gameOverContent,
                        "GameOverMainMenuButton", "MAIN MENU", new Vector2(250f, -280f), 440f);
            }
        }

        private static void RemoveTitleInitialsUI(Transform titleContent)
        {
            string[] names = { "Initials", "InitialsHint", "InitialsInput" };
            foreach (string name in names)
            {
                Transform item = titleContent.Find(name);
                if (item == null) continue;
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
        }

        private static Button ClonePlate(Button source, Transform parent, string name, string value,
            Vector2 position, float width)
        {
            var go = Instantiate(source.gameObject, parent);
            go.name = name;
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, 88f);
            var label = go.GetComponentInChildren<SpriteText>(true);
            ((RectTransform)label.transform).sizeDelta = new Vector2(width - 130f, 88f);
            label.CapHeight = value == "LEADERBOARD" ? 19f : 23f;
            label.SetText(value);
            foreach (Transform child in rt)
            {
                if (child.name != "Sparkle") continue;
                var sparkle = (RectTransform)child;
                sparkle.anchoredPosition = new Vector2(Mathf.Sign(sparkle.anchoredPosition.x)
                    * (width * 0.5f - 46f), 0f);
            }
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            return button;
        }

        private void OpenLeaderboard(bool fromGameOver)
        {
            AudioManager.PlayUiClick();
            _leaderboardFromGameOver = fromGameOver;
            _leaderboardOpen = true;
            titlePanel.Hide();
            gameOverPanel.Hide();
            RefreshLeaderboardRows();
            leaderboardPanel.Show();
        }

        private void BackFromLeaderboard()
        {
            if (!_leaderboardOpen) return;
            AudioManager.PlayUiClick();
            _leaderboardOpen = false;
            leaderboardPanel.Hide();
            if (_leaderboardFromGameOver)
            {
                gameOverPanel.Show();
            }
            else
            {
                titlePanel.Show();
            }
        }

        private void RefreshLeaderboardRows()
        {
            var entries = SaveService.GetLeaderboard();
            var text = new StringBuilder("LEADERBOARD");
            if (entries.Count == 0)
            {
                text.Append("\n\nNO SCORES YET");
            }
            else
            {
                int count = Math.Min(10, entries.Count);
                for (int i = 0; i < count; i++)
                {
                    LeaderboardEntry entry = entries[i];
                    text.Append('\n').Append(i + 1).Append("  ").Append(entry.Initials)
                        .Append("  SCORE ").Append(entry.Score).Append("  CLEARED ")
                        .Append(entry.Initials == "OLD" && entry.Difficulty == "UNKNOWN"
                            ? "UNKNOWN" : entry.BookcasesCleared.ToString())
                        .Append("  ").Append(entry.Difficulty);
                }
            }
            leaderboardRowsText.SetText(text.ToString());
        }

        private static void Click(Action action)
        {
            AudioManager.PlayUiClick();
            action?.Invoke();
        }

        private void ClickPlay(PlayDifficulty difficulty)
        {
            AudioManager.PlayUiClick();
            SaveService.SetLastInitials(PlayerInitials);
            OnPlay?.Invoke(difficulty);
        }

        public void ShowTitle()
        {
            HideAll();
            // Blank until there's an actual best to show - no placeholder flavor text. Two lines
            // instead of one separated by a middle dot: the letter sheet has no punctuation.
            titleBestText.SetText(SaveService.HighScore > 0
                ? $"BEST {SaveService.HighScore}\nBOOKCASE {SaveService.BestShelf}"
                : string.Empty);
            titlePanel.Show();
        }

        public void ShowPause() => pausePanel.Show();

        public void HidePause() => pausePanel.Hide();

        public void ShowGameOver(int score, int bookcasesCleared)
        {
            HideAll();
            gameOverText.SetText(
                $"GAME OVER\n\nSCORE {score}\nBOOKCASES CLEARED {bookcasesCleared}\n\nBEST {SaveService.HighScore}");
            gameOverPanel.Show();
        }

        public void ShowBanner(string text)
        {
            bannerText.SetText(text);
            banner.Show();
        }

        public void HideBanner() => banner.Hide();

        public void HideAll()
        {
            _leaderboardOpen = false;
            titlePanel.Hide();
            pausePanel.Hide();
            gameOverPanel.Hide();
            if (leaderboardPanel != null) leaderboardPanel.Hide();
            banner.Hide();
        }
    }
}
