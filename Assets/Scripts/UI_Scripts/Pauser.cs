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

    public static bool PauseLocked = false;

    private bool isPaused;
    private bool instructionPanelLock;
    private bool isApplicationFocused = true;

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
        // Reset the general pause state, but do not clear
        // instructionPanelLock here. PassengerInstructionUI
        // may already have enabled that lock in its Start().
        PauseLocked = false;
        isPaused = false;
        Time.timeScale = 1f;

        UpdatePauseButton();
        UpdateSoundIcon();
    }

    private void Update()
    {
        if (PauseLocked ||
            instructionPanelLock ||
            IsGameFinished() ||
            !isApplicationFocused)
        {
            return;
        }

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
        return GameManager.Instance != null &&
               GameManager.Instance.IsGameFinished;
    }

    public void Pause()
    {
        // Never open the pause screen while instructions
        // or a passenger pickup lock is active.
        if (PauseLocked ||
            instructionPanelLock ||
            IsGameFinished() ||
            isPaused)
        {
            return;
        }

        isPaused = true;
        Time.timeScale = 0f;

        if (LevelObject != null)
            LevelObject.SetActive(false);

        if (PausePannel != null)
            PausePannel.SetActive(true);

        UpdatePauseButton();
        UpdateSoundIcon();
    }

    public void Resume()
    {
        if (PauseLocked ||
            instructionPanelLock ||
            IsGameFinished())
        {
            return;
        }

        if (PausePannel != null)
            PausePannel.SetActive(false);

        if (LevelObject != null)
            LevelObject.SetActive(true);

        isPaused = false;
        Time.timeScale = 1f;

        UpdatePauseButton();
        ApplySoundSetting();
        UpdateSoundIcon();
    }

    // Called by PassengerInstructionUI.
    public void SetInstructionPanelLock(bool locked)
    {
        instructionPanelLock = locked;

        if (locked)
        {
            // Close any pause screen if instructions begin.
            if (PausePannel != null)
                PausePannel.SetActive(false);

            isPaused = false;
            Time.timeScale = 1f;
        }

        // Hides the pause button while instructions are active.
        // Shows it again after the instruction panel closes,
        // unless another pause lock or game-finished state exists.
        UpdatePauseButton();
    }

    public bool IsInstructionPanelLocked =>
        instructionPanelLock;

    public bool IsPaused => isPaused;

    public void MM()
    {
        Time.timeScale = 1f;
        PauseLocked = false;
        instructionPanelLock = false;
        isPaused = false;
        SaveScript.cameFromGameplay = true;
        SceneManager.LoadScene("UI");
    }

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
            !PauseLocked &&
            !instructionPanelLock &&
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
        PauseLocked = false;

        if (instance != null)
            instance.UpdatePauseButton();
    }

    private void OnApplicationFocus(bool focus)
    {
        isApplicationFocused = focus;

        if (!focus)
        {
            if (!PauseLocked &&
                !instructionPanelLock &&
                !isPaused &&
                !IsGameFinished())
            {
                Pause();
            }
        }
        else
        {
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