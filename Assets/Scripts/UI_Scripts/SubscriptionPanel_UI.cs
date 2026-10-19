using UnityEngine;
using UnityEngine.UI;

public class SubscriptionPanel_UI : MonoBehaviour
{
    public Button closeButton;
    public AudioSource mainMenuAudioPlayer;
    public GameObject mainMenu;
    public GameObject carSelectionMenu;
    public AudioSource subaudio;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (animator != null)
            animator.SetTrigger("Entry");

        if (mainMenuAudioPlayer != null)
            mainMenuAudioPlayer.Stop();

        if (subaudio != null)
            subaudio.loop = true;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseUI);
            closeButton.onClick.AddListener(CloseUI);
        }

        // Skip the subscription panel only when it is not
        // supposed to be shown and gameplay did not request it.
        if (!SaveScript.canShowSubScriptionPanel &&
            !SaveScript.cameFromGameplay)
        {
            CloseUI();
        }
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);

        SaveScript.canShowSubScriptionPanel = false;

        if (SaveScript.cameFromGameplay)
        {
            // Gameplay Home -> Subscription -> Car Selection.
            if (mainMenu != null)
                mainMenu.SetActive(false);

            if (carSelectionMenu != null)
                carSelectionMenu.SetActive(true);

            SaveScript.cameFromGameplay = false;
        }
        else
        {
            // Normal main-menu subscription -> return to main menu.
            if (mainMenu != null)
                mainMenu.SetActive(true);
        }

        if (mainMenuAudioPlayer != null &&
            !mainMenuAudioPlayer.isPlaying)
        {
            mainMenuAudioPlayer.Play();
        }
    }
}