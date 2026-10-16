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
            PauseLocked = true;

        UpdatePauseButton();
        UpdateSoundIcon();
    }

    private void Update()
    {
        if (PauseLocked || instructionPanelLock || IsGameFinished())
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
        return GameManager.Instance != null &&
               GameManager.Instance.IsGameFinished;
    }

    public void Pause()
    {
        if (PauseLocked || instructionPanelLock ||
            IsGameFinished() || isPaused)
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
        if (instructionPanelLock || PauseLocked || IsGameFinished())
            return;

        if (PausePannel != null)
            PausePannel.SetActive(false);

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
            // Close any pause menu and prevent pausing during instructions.
            if (PausePannel != null)
                PausePannel.SetActive(false);

            isPaused = false;
            Time.timeScale = 1f;
        }

        UpdatePauseButton();
    }

    public bool IsInstructionPanelLocked => instructionPanelLock;

    public void MM()
    {
        Time.timeScale = 1f;
        PauseLocked = false;
        instructionPanelLock = false;
        isPaused = false;

        SceneManager.LoadScene("UI");
    }

    public void ToggleSound()
    {
        AudioManagerPause.IsMuted = !AudioManagerPause.IsMuted;

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
                AudioManagerPause.IsMuted ? sound_off : sound_on;
        }
    }

    private void UpdatePauseButton()
    {
        if (PauseButton == null)
            return;

        bool shouldShow =
            !AndroidTV.IsAndroidOrFireTv() &&
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
        // Keep the Tutorial scene's existing restriction.
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