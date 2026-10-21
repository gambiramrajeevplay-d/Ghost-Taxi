using Script;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Win / Lose Panels")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    [Header("Level GameObject")]
    [SerializeField] private string levelGameObjectName = "Level";
    private GameObject levelGameObject;

    [Header("Result Settings")]
    [SerializeField] private float resultPanelDelay = 2f;

    [Header("Win Reward")]
    [SerializeField] private int winCoinsReward = 100;
    [SerializeField] private TMP_Text winCoinsText;

    [Header("Lose Reward")]
    [SerializeField] private int loseCoinsReward = 0;
    [SerializeField] private TMP_Text loseCoinsText;

    [Header("Cinematic Camera")]
    [SerializeField] private RCC_Camera rccCamera;
    [SerializeField] private bool playCinematicOnWin = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip winAudio;
    [SerializeField] private AudioClip loseAudio;

    [Header("Coin Settings")]
    [SerializeField] private string coinsPlayerPrefsKey = "TotalCoins";

    private bool resultStarted;
    private bool isLoadingScene;

    public bool IsGameFinished => resultStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Find the gameplay level.
        levelGameObject = GameObject.Find(levelGameObjectName);

        if (levelGameObject == null)
        {
            Debug.LogWarning(
                $"GameManager: GameObject '{levelGameObjectName}' was not found.",
                this);
        }

        // Set up audio.
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            audioSource.playOnAwake = false;

        // Find the RCC camera.
        if (rccCamera == null)
            rccCamera = FindObjectOfType<RCC_Camera>();

        // Hide result panels initially.
        if (winPanel != null)
            winPanel.SetActive(false);
        else
            Debug.LogWarning("GameManager: Win Panel is not assigned.", this);

        if (losePanel != null)
            losePanel.SetActive(false);
        else
            Debug.LogWarning("GameManager: Lose Panel is not assigned.", this);

        Debug.Log("GameManager: Initialized successfully.", this);
    }

    // Called when the passenger has been dropped off.
    public void PassengerDropped()
    {
        PassengerDropped(null);
    }

    public void PassengerDropped(RCC_CarControllerV3 playerCar)
    {
        if (resultStarted)
        {
            Debug.Log("GameManager: PassengerDropped ignored; result already started.", this);
            return;
        }

        Debug.Log("GameManager: Passenger dropped. Starting win sequence.", this);

        ActivateCinematicCamera(playerCar);
        WinGame();
    }

    public void ActivateCinematicCamera(RCC_CarControllerV3 playerCar)
    {
        if (!playCinematicOnWin)
            return;

        if (rccCamera == null)
            rccCamera = FindObjectOfType<RCC_Camera>();

        if (rccCamera == null)
        {
            Debug.LogWarning("GameManager: RCC_Camera was not found.", this);
            return;
        }

        RCC_CinematicCamera cinematicCamera =
            FindObjectOfType<RCC_CinematicCamera>();

        if (cinematicCamera == null)
        {
            Debug.LogWarning(
                "GameManager: RCC_CinematicCamera was not found. " +
                "Check the RCC cinematic camera setup.",
                this);
            return;
        }

        if (playerCar != null)
            rccCamera.SetTarget(playerCar.gameObject);

        rccCamera.ChangeCamera(RCC_Camera.CameraMode.CINEMATIC);

        Debug.Log("GameManager: Cinematic camera activated.", this);
    }

    public void WinGame()
    {
        if (resultStarted)
            return;

        resultStarted = true;
        Time.timeScale = 1f;

        Debug.Log("GameManager: WinGame() called.", this);

        // Award win coins.
        GiveCoins(winCoinsReward);
        UpdateCoinsText(winCoinsText, winCoinsReward);

        // Unlock the next level.
        UnlockNextLevel();

        // Play win audio.
        PlayResultAudio(winAudio);

        // Display the win panel after the delay.
        StartCoroutine(ShowResultPanel(winPanel, "WIN"));
    }

    public void LoseGame()
    {
        if (resultStarted)
            return;

        resultStarted = true;
        Time.timeScale = 1f;

        Debug.Log("GameManager: LoseGame() called.", this);

        // Award lose coins if configured.
        if (loseCoinsReward > 0)
            GiveCoins(loseCoinsReward);

        UpdateCoinsText(loseCoinsText, loseCoinsReward);

        // Play lose audio.
        PlayResultAudio(loseAudio);

        // Display the lose panel after the delay.
        StartCoroutine(ShowResultPanel(losePanel, "LOSE"));
    }

    private void UnlockNextLevel()
    {
        int currentLevel = PlayerPrefs.GetInt(
            StringsData.playerLevel, 1);

        int nextLevel = currentLevel + 1;

        PlayerPrefs.SetInt(StringsData.playerLevel, nextLevel);
        PlayerPrefs.Save();

        Debug.Log(
            $"GameManager: Level {currentLevel} completed. " +
            $"Level {nextLevel} is now unlocked.",
            this);
    }

    private IEnumerator ShowResultPanel(GameObject panel, string resultName)
    {
        Debug.Log(
            $"GameManager: Preparing {resultName} panel.", this);

        // Use unscaled time so the delay does not depend on timeScale.
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, resultPanelDelay));

        // Disable gameplay without accidentally disabling a result
        // panel that is nested inside the Level hierarchy.
        DisableLevelExceptResultPanels();

        // Ensure the game is not paused when displaying results.
        Time.timeScale = 1f;

        if (panel == null)
        {
            Debug.LogError(
                $"GameManager: {resultName} panel is not assigned. " +
                "Assign it in the Inspector.",
                this);
            yield break;
        }

        // Explicitly activate the result panel.
        panel.SetActive(true);

        Canvas panelCanvas = panel.GetComponentInParent<Canvas>(true);

        if (panelCanvas != null)
        {
            // Enable the Canvas in case it was disabled.
            panelCanvas.enabled = true;

            // Ensure the panel is drawn after other UI in its Canvas.
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = Mathf.Max(
                panelCanvas.sortingOrder, 100);
        }

        Debug.Log(
            $"GameManager: {resultName} panel activated. " +
            $"Active in hierarchy: {panel.activeInHierarchy}.",
            this);

        if (!panel.activeInHierarchy)
        {
            Debug.LogError(
                $"GameManager: {resultName} panel is still inactive " +
                "in the hierarchy. Check its parent objects.",
                this);
        }
    }

    private void DisableLevelExceptResultPanels()
    {
        if (levelGameObject == null)
        {
            Debug.LogWarning(
                "GameManager: Cannot disable Level because it was not found.",
                this);
            return;
        }

        Transform levelTransform = levelGameObject.transform;

        bool winPanelInsideLevel =
            winPanel != null &&
            winPanel.transform.IsChildOf(levelTransform);

        bool losePanelInsideLevel =
            losePanel != null &&
            losePanel.transform.IsChildOf(levelTransform);

        // If neither result panel is inside Level, disable the
        // entire gameplay object as usual.
        if (!winPanelInsideLevel && !losePanelInsideLevel)
        {
            levelGameObject.SetActive(false);

            Debug.Log(
                "GameManager: Level disabled successfully.", this);
            return;
        }

        // A result panel is nested inside Level. Disabling Level itself
        // would hide that panel. Disable gameplay branches individually,
        // keeping the parent branches of the result panels active.
        Debug.LogWarning(
            "GameManager: A result panel is inside Level. " +
            "Disabling gameplay children while preserving result UI.",
            this);

        for (int i = 0; i < levelTransform.childCount; i++)
        {
            Transform child = levelTransform.GetChild(i);

            bool containsWinPanel =
                winPanelInsideLevel &&
                IsSameOrAncestorOf(child, winPanel.transform);

            bool containsLosePanel =
                losePanelInsideLevel &&
                IsSameOrAncestorOf(child, losePanel.transform);

            if (containsWinPanel || containsLosePanel)
            {
                Debug.Log(
                    $"GameManager: Preserving result UI branch '{child.name}'.",
                    child);
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private bool IsSameOrAncestorOf(
        Transform possibleAncestor,
        Transform target)
    {
        return target == possibleAncestor ||
               target.IsChildOf(possibleAncestor);
    }

    private void GiveCoins(int amount)
    {
        if (amount <= 0)
            return;

        // Prefer the project's currency manager.
        if (CurrecnyManager.instance != null)
        {
            CurrecnyManager.instance.AddCurrency(amount);
        }
        else
        {
            int currentCoins = PlayerPrefs.GetInt(
                coinsPlayerPrefsKey, 0);

            PlayerPrefs.SetInt(
                coinsPlayerPrefsKey, currentCoins + amount);

            PlayerPrefs.Save();
        }
    }

    private void UpdateCoinsText(TMP_Text coinsText, int amount)
    {
        if (coinsText != null)
        {
            coinsText.text = amount.ToString();
        }
        else
        {
            Debug.LogWarning(
                "GameManager: Reward TMP_Text is not assigned.", this);
        }
    }

    private void PlayResultAudio(AudioClip clip)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    public void LoadLevelSelection()
    {
        if (isLoadingScene)
            return;

        isLoadingScene = true;
        Time.timeScale = 1f;

        SceneManager.LoadScene("Level_Selection");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

}