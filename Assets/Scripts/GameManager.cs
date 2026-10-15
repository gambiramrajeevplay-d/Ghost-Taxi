using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Win / Lose Panels")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Result Settings")]
    [SerializeField] private float resultPanelDelay = 2f;

    [Header("Win Reward")]
    [SerializeField] private int winCoinsReward = 100;
    [SerializeField] private TMP_Text winCoinsText;

    [Header("Lose Reward")]
    [SerializeField] private int loseCoinsReward = 0;
    [SerializeField] private TMP_Text loseCoinsText;

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

        if (winPanel != null)
            winPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(false);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    // Call this when the passenger finishes dropping.
    public void PassengerDropped()
    {
        WinGame();
    }

    // Call this when the player wins.
    public void WinGame()
    {
        if (resultStarted)
            return;

        resultStarted = true;

        Time.timeScale = 1f;

        GiveCoins(winCoinsReward);
        UpdateCoinsText(winCoinsText, winCoinsReward);

        PlayResultAudio(winAudio);

        StartCoroutine(ShowResultPanel(winPanel));
    }

    // Call this when the player loses.
    public void LoseGame()
    {
        if (resultStarted)
            return;

        resultStarted = true;

        Time.timeScale = 1f;

        if (loseCoinsReward > 0)
            GiveCoins(loseCoinsReward);

        UpdateCoinsText(loseCoinsText, loseCoinsReward);

        PlayResultAudio(loseAudio);

        StartCoroutine(ShowResultPanel(losePanel));
    }

    private IEnumerator ShowResultPanel(GameObject panel)
    {
        yield return new WaitForSeconds(
            Mathf.Max(0f, resultPanelDelay));

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

        int currentCoins = PlayerPrefs.GetInt(
            coinsPlayerPrefsKey, 0);

        PlayerPrefs.SetInt(
            coinsPlayerPrefsKey, currentCoins + amount);

        PlayerPrefs.Save();
    }

    private void UpdateCoinsText(TMP_Text coinsText, int amount)
    {
        if (coinsText != null)
        {
            coinsText.text = "+" + amount + " Coins";
        }
    }

    private void PlayResultAudio(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Connect this method to your result panel's button.
    public void LoadLevelSelection()
    {
        if (isLoadingScene)
            return;

        isLoadingScene = true;

        Time.timeScale = 1f;
        SceneManager.LoadScene("Level_Selection");
    }

}