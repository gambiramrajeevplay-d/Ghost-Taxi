using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Finish Panel")]
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private float finishPanelDelay = 2f;

    private bool finishSequenceStarted;
    private bool isLoadingScene;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (finishPanel != null)
            finishPanel.SetActive(false);
    }

    // Call this after the passenger finishes dropping.
    public void PassengerDropped()
    {
        if (finishSequenceStarted)
            return;

        finishSequenceStarted = true;
        StartCoroutine(ShowFinishPanelRoutine());
    }

    private IEnumerator ShowFinishPanelRoutine()
    {
        yield return new WaitForSeconds(
            Mathf.Max(0f, finishPanelDelay));

        if (finishPanel != null)
        {
            finishPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "GameManager: Finish Panel is not assigned.",
                this);
        }
    }

    // Connect this method to the Finish Button.
    public void LoadLevelSelection()
    {
        if (isLoadingScene)
            return;

        isLoadingScene = true;
        SceneManager.LoadScene("Level_Selection");
    }

}