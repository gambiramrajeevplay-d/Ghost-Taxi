using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button storeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button moreGamesButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button storeCloseButton;
    [SerializeField] private Button settingsCloseButton;
    [SerializeField] private Button carSelectionClose;
    [SerializeField] private Button unlock4CloseButton;

    [Header("Menus")]
    [SerializeField] private GameObject storeMenu;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject subscriptionPanel;
    [SerializeField] private GameObject carSelectionMenu;
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject unlock4CarsPanel;
    [SerializeField] private GameObject levelselectionPanel;

    [Header("Currency")]
    [SerializeField] private TextMeshProUGUI currencyText;

    [Header("Sound")]
    public Image sound_;
    public Sprite sound_on, sound_off;

    private Animator animator;

    public static MainMenu instance;
    private void Awake()
    {
        instance = this;
        Time.timeScale = 1f;

        AudioManagerPause.Initialize();

    }
    private void OnEnable()
    {
        if (CurrecnyManager.instance != null)
        {
            CurrecnyManager.instance.OnCurrencyChanged += UpdateCoins;
            UpdateCoins(CurrecnyManager.instance.GetCurrency());
        }
    }

    private void OnDisable()
    {
        if (CurrecnyManager.instance != null)
        {
            CurrecnyManager.instance.OnCurrencyChanged -= UpdateCoins;
        }
    }

    void UpdateCoins(int amount)
    {
        currencyText.text = amount.ToString();
    }
    private void Start()
    {
        animator = GetComponent<Animator>();
        UpdateSoundIcon();
        // Buttons
        startButton.onClick.AddListener(OnStartButtonPressed);
        storeButton.onClick.AddListener(OpenStoreMenu);
        settingsButton.onClick.AddListener(OpenSettingsMenu);

        storeCloseButton.onClick.AddListener(OpenMainMenu);
        settingsCloseButton.onClick.AddListener(OnSettingClose);
        carSelectionClose.onClick.AddListener(OnCarSelectionClose);
        backButton.onClick.AddListener(OpenSubscriptionPanel);

        if (unlock4CloseButton != null)
            unlock4CloseButton.onClick.AddListener(OnUnlock4Close);

        UpdateCurrencyText();

        // 🔥 CORE ENTRY FLOW
        HandleSubscriptionEntryFlow();
    }

    // =====================
    // 🔑 SUBSCRIPTION ENTRY LOGIC
    // =====================
    void HandleSubscriptionEntryFlow()
    {
        DisableAllMenus();

        bool comingFromGame =
            PlayerPrefs.GetInt("ShowSubscriptionPanel", 0) == 1;

        bool hasSubscription =
            PlayerPrefs.GetInt(StringsData.hasSubscription, 0) == 1;

        if (comingFromGame)
        {
            // Clear immediately
            PlayerPrefs.DeleteKey("ShowSubscriptionPanel");
            PlayerPrefs.Save();

            SaveScript.cameFromGameplay = true;

            if (!hasSubscription)
            {
                subscriptionPanel.SetActive(true);
            }
            else
            {
                carSelectionMenu.SetActive(true);
            }

            return; // ⛔ skip main menu
        }

        // 🟢 Normal fresh launch
        SaveScript.cameFromGameplay = false;
        mainMenu.SetActive(true);
        SaveScript.cameFromGameplay = false;
        mainMenu.SetActive(true);
    }

    // =====================
    // MAIN FLOW
    // =====================
    public void OnStartButtonPressed()
    {
        SaveScript.openedGameforFirstTime = true;
        unlock4CarsPanel.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void OpenMainMenu()
    {
        mainMenu.SetActive(true);
        storeMenu.SetActive(false);
        settingsMenu.SetActive(false);
        subscriptionPanel.SetActive(false);

        PlayMainMenuMusic();

    }

    // =====================
    // STORE / SETTINGS
    // =====================
    public void OpenStoreMenu()
    {
        storeMenu.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void OpenSettingsMenu()
    {
        settingsMenu.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void OnSettingClose()
    {
        settingsMenu.SetActive(false);
        mainMenu.SetActive(true);
        PlayMainMenuMusic();
    }

    // =====================
    // UNLOCK PANEL
    // =====================
    public void OnUnlock4Close()
    {
        unlock4CarsPanel.SetActive(false);
        carSelectionMenu.SetActive(true);
    }

    // =====================
    // CAR SELECTION
    // =====================
    public void OnCarSelectionClose()
    {
        carSelectionMenu.SetActive(false);
        mainMenu.SetActive(true);
        PlayMainMenuMusic();
    }

    // =====================
    // SUBSCRIPTION
    // =====================
    public void OpenSubscriptionPanel()
    {
        subscriptionPanel.SetActive(true);
        mainMenu.SetActive(false);
        storeMenu.SetActive(false);
        settingsMenu.SetActive(false);
    }

    // =====================
    // CURRENCY
    // =====================
    public void UpdateCurrencyText(int amount = 0)
    {
        if (currencyText != null && CurrecnyManager.instance != null)
        {
            currencyText.text =
                CurrecnyManager.instance.GetCurrency().ToString();
        }
    }

    // =====================
    // EXTRA
    // =====================
    public void MoreGames()
    {
        Application.OpenURL(
            "https://www.amazon.com/Games-PlayD-Game-Studio-Private-Limited/s?rh=n%3A9209902011%2Cp_4%3APlayD+Game+Studio+Private+Limited"
        );
    }


    public void Sound_on()
    {
        AudioManagerPause.IsMuted = !AudioManagerPause.IsMuted;
        UpdateSoundIcon();
        UpdateMainMenuMusic();
    }

    private void UpdateSoundIcon()
    {
        if (sound_ != null)
        {
            sound_.sprite = AudioManagerPause.IsMuted
                ? sound_off
                : sound_on;
        }
    }

    public void UpdateMainMenuMusic()
    {
        if (AudioManagerPause.IsMuted)
            return;

        // Do not play main-menu music behind the subscription panel.
        if (subscriptionPanel != null &&
            subscriptionPanel.activeInHierarchy)
            return;

        AudioSource music = null;

        // Find the main-menu music source from the subscription UI.
        SubscriptionPanel_UI subscriptionUI =
            subscriptionPanel != null
                ? subscriptionPanel.GetComponent<SubscriptionPanel_UI>()
                : null;

        if (subscriptionUI != null)
            music = subscriptionUI.mainMenuAudioPlayer;

        if (music != null && !music.isPlaying)
            music.Play();
    }


    public void PlayMainMenuMusic()
    {
        AudioManagerPause.Initialize();

        if (AudioManagerPause.IsMuted)
            return;

        // Never play menu music behind the subscription panel.
        if (subscriptionPanel != null &&
            subscriptionPanel.activeInHierarchy)
            return;

        if (mainMenu == null || !mainMenu.activeInHierarchy)
            return;

        SubscriptionPanel_UI subscriptionUI =
            subscriptionPanel != null
                ? subscriptionPanel.GetComponent<SubscriptionPanel_UI>()
                : null;

        if (subscriptionUI == null)
            return;

        AudioSource music = subscriptionUI.mainMenuAudioPlayer;

        if (music != null && !music.isPlaying)
            music.Play();
    }


    void DisableAllMenus()
    {
        mainMenu.SetActive(false);
        storeMenu.SetActive(false);
        settingsMenu.SetActive(false);
        subscriptionPanel.SetActive(false);
        carSelectionMenu.SetActive(false);
        unlock4CarsPanel.SetActive(false);
        levelselectionPanel.SetActive(false);
    }
}
