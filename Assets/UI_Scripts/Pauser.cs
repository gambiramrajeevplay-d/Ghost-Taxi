
using Script;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Pauser : MonoBehaviour
{
    public static Pauser instance;

    [Header("UI")]
    public GameObject PausePannel;
    public GameObject LevelObject;
    public GameObject PauseButton;

    [Header("Sound UI")]
    public Image soundIcon;
    public Sprite sound_on;
    public Sprite sound_off;

    // Global pause lock
    public static bool PauseLocked = false;

    private bool isPaused;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        AudioManagerPause.Initialize();
        ApplySoundSetting();
    }

    private void OnEnable()
    {
        UpdatePauseButton();
        UpdateSoundIcon();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Tutorial")
        {
            PauseLocked = true;
        }

        UpdatePauseButton();
        UpdateSoundIcon();
    }

    private void Update()
    {
        if (PauseLocked || IsGameFinished())
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    private bool IsGameFinished()
    {
        if (GameManager.Instance == null)
            return false;

        return GameManager.Instance.IsGameFinished;
    }

    public void Pause()
    {
        if (PauseLocked || IsGameFinished() || isPaused)
            return;

        isPaused = true;

        if (PausePannel != null)
            PausePannel.SetActive(true);

        if (PauseButton != null &&
            !AndroidTV.IsAndroidOrFireTv())
        {
            PauseButton.SetActive(false);
        }

        Time.timeScale = 0f;
        UpdateSoundIcon();
    }

    public void Resume()
    {
        if (PausePannel != null)
            PausePannel.SetActive(false);

        isPaused = false;

        Time.timeScale = 1f;

        UpdatePauseButton();
        ApplySoundSetting();
        UpdateSoundIcon();
    }

    // Main menu button
    public void MM()
    {
        Time.timeScale = 1f;
        PauseLocked = false;
        isPaused = false;

        SceneManager.LoadScene("UI");
    }

    // Sound toggle button
    public void ToggleSound()
    {
        AudioManagerPause.IsMuted =
            !AudioManagerPause.IsMuted;

        ApplySoundSetting();
        UpdateSoundIcon();
    }

    private void ApplySoundSetting()
    {
        AudioListener.volume =
            AudioManagerPause.IsMuted ? 0f : 1f;
    }

    private void UpdateSoundIcon()
    {
        if (soundIcon != null)
        {
            soundIcon.sprite =
                AudioManagerPause.IsMuted
                ? sound_off
                : sound_on;
        }
    }

    private void UpdatePauseButton()
    {
        if (PauseButton == null)
            return;

        bool shouldShow =
            !AndroidTV.IsAndroidOrFireTv() &&
            !PauseLocked &&
            !IsGameFinished() &&
            !isPaused;

        PauseButton.SetActive(shouldShow);
    }

    public static void LockPause()
    {
        PauseLocked = true;

        if (instance != null)
        {
            if (instance.PausePannel != null)
                instance.PausePannel.SetActive(false);

            if (instance.LevelObject != null)
                instance.LevelObject.SetActive(true);

            instance.isPaused = false;
            instance.UpdatePauseButton();
        }

        Time.timeScale = 1f;
    }

    public static void UnlockPause()
    {
        // Never unlock pause in the Tutorial scene.
        if (SceneManager.GetActiveScene().name == "Tutorial")
            return;

        PauseLocked = false;

        if (instance != null)
            instance.UpdatePauseButton();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            if (!PauseLocked &&
                !isPaused &&
                !IsGameFinished())
            {
                Pause();
            }
        }
        else
        {
            // Restore the saved sound preference.
            ApplySoundSetting();
            UpdateSoundIcon();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
