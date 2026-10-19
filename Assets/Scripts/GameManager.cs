
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
    public GameObject winPanel;
    public GameObject losePanel;

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

        // Find the level GameObject by name.
        levelGameObject = GameObject.Find(levelGameObjectName);

        if (levelGameObject == null)
        {
            Debug.LogWarning(
                $"GameManager: GameObject named '{levelGameObjectName}' was not found.",
                this);
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;

        if (rccCamera == null)
            rccCamera = FindObjectOfType<RCC_Camera>();

        if (winPanel != null)
            winPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(false);
    }

    // Called when the passenger has been dropped off.
    public void PassengerDropped()
    {
        PassengerDropped(null);
    }

    public void PassengerDropped(RCC_CarControllerV3 playerCar)
    {
        if (resultStarted)
            return;

        PlayWinCinematic(playerCar);
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
            Debug.LogWarning(
                "GameManager: RCC_Camera was not found.", this);
            return;
        }

        if (playerCar != null)
            rccCamera.SetTarget(playerCar.gameObject);

        rccCamera.ChangeCamera(RCC_Camera.CameraMode.CINEMATIC);
    }

    public void WinGame()
    {
        if (resultStarted)
            return;

        resultStarted = true;
        Time.timeScale = 1f;

        // Award coins.
        GiveCoins(winCoinsReward);
        UpdateCoinsText(winCoinsText, winCoinsReward);

        // Unlock the next level.
        UnlockNextLevel();

        // Play win audio.
        PlayResultAudio(winAudio);

        // Show the win panel after the delay.
        StartCoroutine(ShowResultPanel(winPanel));
    }

    public void LoseGame()
    {
        if (resultStarted)
            return;

        resultStarted = true;
        Time.timeScale = 1f;

        // Award lose coins if configured.
        if (loseCoinsReward > 0)
            GiveCoins(loseCoinsReward);

        UpdateCoinsText(loseCoinsText, loseCoinsReward);

        // Play lose audio.
        PlayResultAudio(loseAudio);

        // Show the lose panel after the delay.
        StartCoroutine(ShowResultPanel(losePanel));
    }

    private void UnlockNextLevel()
    {
        int currentLevel =
            PlayerPrefs.GetInt(StringsData.playerLevel, 1);

        int nextLevel = currentLevel + 1;

        PlayerPrefs.SetInt(StringsData.playerLevel, nextLevel);
        PlayerPrefs.Save();

        Debug.Log(
            $"GameManager: Level {currentLevel} completed. " +
            $"Level {nextLevel} is now unlocked.");
    }

    private IEnumerator ShowResultPanel(GameObject panel)
    {
        yield return new WaitForSeconds(
            Mathf.Max(0f, resultPanelDelay));

        // Disable the level before showing the result panel.
        if (levelGameObject != null)
        {
            levelGameObject.SetActive(false);
        }

        if (panel != null)
        {
            panel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "GameManager: Result panel is not assigned.", this);
        }
    }

    private void GiveCoins(int amount)
    {
        if (amount <= 0)
            return;

        // Prefer the currency manager.
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
            coinsText.text = amount.ToString();
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

    private void PlayWinCinematic(RCC_CarControllerV3 playerCar)
    {
        if (!playCinematicOnWin)
            return;

        if (rccCamera == null)
            rccCamera = FindObjectOfType<RCC_Camera>();

        if (rccCamera == null)
        {
            Debug.LogWarning(
                "GameManager: RCC_Camera was not found.", this);
            return;
        }

        RCC_CinematicCamera cinematicCamera =
            FindObjectOfType<RCC_CinematicCamera>();

        if (cinematicCamera == null)
        {
            Debug.LogWarning(
                "GameManager: RCC_CinematicCamera was not found. " +
                "Check that the RCC cinematic camera is configured.",
                this);
            return;
        }

        if (playerCar != null)
            rccCamera.SetTarget(playerCar.gameObject);

        rccCamera.ChangeCamera(RCC_Camera.CameraMode.CINEMATIC);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
