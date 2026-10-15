using System.Collections;
using System.Collections.Generic;
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

    private void OnEnable()
    {
        if (animator != null)
        {
            animator.SetTrigger("Entry");
        }
        mainMenuAudioPlayer.Stop();
        subaudio.loop = true;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        animator = GetComponent<Animator>();

        closeButton.onClick.AddListener(CloseUI);
        if (!SaveScript.canShowSubScriptionPanel)
        {
            CloseUI();
            return;
        }
        mainMenuAudioPlayer.Stop();

    }


    public void CloseUI()
    {
        gameObject.SetActive(false);

        // Mark as shown
        SaveScript.canShowSubScriptionPanel = false;

        if (SaveScript.cameFromGameplay)
        {
            // Coming from gameplay → go to car selection
            carSelectionMenu.SetActive(true);
            SaveScript.cameFromGameplay = false;
        }
        else
        {
            // Coming from main menu → always go back to main menu
            mainMenu.SetActive(true);
        }

        if (!mainMenuAudioPlayer.isPlaying)
            mainMenuAudioPlayer.Play();
    }
}
