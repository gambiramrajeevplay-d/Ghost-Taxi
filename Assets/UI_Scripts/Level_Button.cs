using Script;
using System.Collections;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class Level_Button : MonoBehaviour
{
    [Header("Level Details")]
    [SerializeField] private string sceneNameToLoad = "MainGame";
    [SerializeField] private int levelToLoad = 1; // 1-based
    [SerializeField] private bool isPaidLevel = false;

    [Header("Visuals")]
    public GameObject lockImage;

    [Header("Menus")]
    [SerializeField] private LevelSelectionUI levelSelectionUI;

    [Header("Loading Screen")]
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private GameObject loadingScreentab;

    private Button levelButton;

    private void Awake()
    {
        EnsureInitialized();
        levelButton.interactable = false;
    }

    private void Start()
    {
        EnsureInitialized();

        levelButton.onClick.RemoveAllListeners();
       
        levelButton.onClick.AddListener(LoadLevel);
    }

    private void EnsureInitialized()
    {
        if (levelButton == null)
            levelButton = GetComponent<Button>();
    }

    // 🔑 CALLED FROM LevelSelectionUI
    public void SetUnlocked(bool unlocked)
    {
        EnsureInitialized();

        int playerLevel = PlayerPrefs.GetInt(StringsData.playerLevel, 1); // ✅ FIX
        int unlockedAll = PlayerPrefs.GetInt(StringsData.unlockedAllLevels, 0);

        levelButton.onClick.RemoveAllListeners();

        // 🚨 LEVEL 1 ALWAYS FREE
        if (levelToLoad == 1)
        {
            levelButton.interactable = true;
            if (lockImage) lockImage.SetActive(false);
            levelButton.onClick.AddListener(LoadLevel);
            return;
        }

        // ⭐ SPECIAL CASE: LEVEL 5 AFTER LEVEL 4 WIN
        if (isPaidLevel && levelToLoad == 5)
        {
            if (playerLevel >= 5)
            {
                levelButton.interactable = true;
                if (lockImage) lockImage.SetActive(false);

                levelButton.onClick.AddListener(() =>
                {
                    if (unlockedAll == 1)
                    {
                        LoadLevel(); // Purchased
                    }
                    else
                    {
                        levelSelectionUI.ShowUnlockAllPanel(); // Not purchased
                    }
                });

                return;
            }
        }

        // 🔒 PAID LEVEL (NOT PURCHASED)
        if (isPaidLevel && unlockedAll == 0)
        {
            levelButton.interactable = true;
            if (lockImage) lockImage.SetActive(true);

            levelButton.onClick.AddListener(() =>
            {
                levelSelectionUI.ShowUnlockAllPanel();
            });

            return;
        }

        // 🔓 NORMAL LEVEL
        levelButton.interactable = unlocked;
        if (lockImage) lockImage.SetActive(!unlocked);

        if (unlocked)
            levelButton.onClick.AddListener(LoadLevel);
    }


    // ⚠️ COMPATIBILITY METHOD
    public void UnLockLevel()
    {
        int playerLevel = PlayerPrefs.GetInt(StringsData.playerLevel, 1);
        bool unlocked = playerLevel >= levelToLoad;
        SetUnlocked(unlocked);
    }

    private void LoadLevel()
    {
        int unlockedAll = PlayerPrefs.GetInt(StringsData.unlockedAllLevels, 0);

        if (isPaidLevel && unlockedAll == 0)
        {
            if (levelSelectionUI != null)
                levelSelectionUI.ShowUnlockAllPanel();

            return;
        }

        // SAVE LEVEL
        PlayerPrefs.SetInt(StringsData.levelToLoad, levelToLoad);

        // ✅ USE TEMP VARIABLE
        string targetScene = sceneNameToLoad;

        // ✅ FIRST TIME LEVEL 1
        if (levelToLoad == 1)
        {
            int tutorialPlayed = PlayerPrefs.GetInt("Level1TutorialPlayed", 0);

            if (tutorialPlayed == 0)
            {
                PlayerPrefs.SetInt("Level1TutorialPlayed", 1);
                PlayerPrefs.Save();

                targetScene = "Tutorial";
            }
        }

        // ✅ LOADING SCREEN STILL WORKS FOR TUTORIAL
        if (AndroidTV.IsAndroidOrFireTv())
        {
            if (loadingScreen) loadingScreen.SetActive(true);
            if (loadingScreentab) loadingScreentab.SetActive(false);
        }
        else
        {
            if (loadingScreen) loadingScreen.SetActive(false);
            if (loadingScreentab) loadingScreentab.SetActive(true);
        }

        StartCoroutine(LoadLevelWithDelay(targetScene));
    }
    private IEnumerator LoadLevelWithDelay(string sceneName)
    {
        yield return new WaitForSeconds(3f);

        SceneManager.LoadScene(sceneName);
    }

    public int GetLevelNumberSafe()
    {
        return Mathf.Max(1, levelToLoad);
    }
}
