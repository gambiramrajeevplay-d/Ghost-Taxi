using System.Collections;
using TMPro;
using UnityEngine;

public class PassengerInstructionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private TMP_Text instructionText;

    [Header("Player Car")]
    [SerializeField] private RCC_CarControllerV3 playerCar;

    [Header("Display Settings")]
    [SerializeField] private float displayDuration = 10f;

    [Header("Typewriter Effect")]
    [SerializeField] private float typingSpeed = 0.04f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Ghost Instructions")]
    [TextArea(2, 4)]
    [SerializeField]
    private string ghostInstruction =
        "GHOST MISSION!\nPick up the Ghost and deliver it to the drop-off point. Follow the arrow!";

    [Header("Witch Instructions")]
    [TextArea(2, 4)]
    [SerializeField]
    private string witchInstruction =
        "WITCH MISSION!\nPick up the Witch and deliver her to the drop-off point. Follow the arrow!";

    [Header("Vampire Instructions")]
    [TextArea(2, 4)]
    [SerializeField]
    private string vampireInstruction =
        "VAMPIRE MISSION!\nPick up the Vampire and deliver it to the drop-off point. Follow the arrow!";

    [Header("Zombie Instructions")]
    [TextArea(2, 4)]
    [SerializeField]
    private string zombieInstruction =
        "ZOMBIE MISSION!\nPick up the Zombie and deliver it to the drop-off point. Follow the arrow!";

    [Header("Skeleton Instructions")]
    [TextArea(2, 4)]
    [SerializeField]
    private string skeletonInstruction =
        "SKELETON MISSION!\nPick up the Skeleton and deliver it to the drop-off point. Follow the arrow!";

    private Coroutine displayCoroutine;
    private bool controlsWereEnabled;
    private bool panelLockActive;

    private void Awake()
    {
        if (instructionPanel != null)
            instructionPanel.SetActive(false);
    }

    private void Start()
    {
        if (playerCar == null)
            playerCar = FindObjectOfType<RCC_CarControllerV3>();

        GhostPickupTrigger pickupTrigger =
            FindObjectOfType<GhostPickupTrigger>(true);

        if (pickupTrigger == null)
        {
            Debug.LogWarning(
                "PassengerInstructionUI: GhostPickupTrigger not found.",
                this);
            return;
        }

        ShowInstruction(pickupTrigger.CurrentPassengerType);
    }

    public void ShowInstruction(
        GhostPickupTrigger.PassengerType passengerType)
    {
        if (instructionPanel == null || instructionText == null)
        {
            Debug.LogWarning(
                "PassengerInstructionUI: Assign the panel and text.",
                this);
            return;
        }

        if (playerCar == null)
            playerCar = FindObjectOfType<RCC_CarControllerV3>();

        if (playerCar != null && displayCoroutine == null)
            controlsWereEnabled = playerCar.canControl;

        // Lock pause while instructions are displayed.
        if (!panelLockActive)
        {
            panelLockActive = true;

            if (Pauser.instance != null)
                Pauser.instance.SetInstructionPanelLock(true);
        }

        if (playerCar != null)
            playerCar.canControl = false;

        switch (passengerType)
        {
            case GhostPickupTrigger.PassengerType.Ghost:
                instructionText.text = ghostInstruction;
                break;

            case GhostPickupTrigger.PassengerType.Witch:
                instructionText.text = witchInstruction;
                break;

            case GhostPickupTrigger.PassengerType.Vampire:
                instructionText.text = vampireInstruction;
                break;

            case GhostPickupTrigger.PassengerType.Zombie:
                instructionText.text = zombieInstruction;
                break;

            case GhostPickupTrigger.PassengerType.Skeleton:
                instructionText.text = skeletonInstruction;
                break;

            default:
                instructionText.text =
                    "MISSION START!\nFollow the arrow!";
                break;
        }

        if (displayCoroutine != null)
            StopCoroutine(displayCoroutine);

        displayCoroutine = StartCoroutine(ShowAndTypeInstruction());
    }

    private IEnumerator ShowAndTypeInstruction()
    {
        instructionPanel.SetActive(true);

        string fullText = instructionText.text;

        instructionText.maxVisibleCharacters = 0;
        instructionText.ForceMeshUpdate();

        int totalCharacters =
            instructionText.textInfo.characterCount;

        for (int i = 0; i < totalCharacters; i++)
        {
            instructionText.maxVisibleCharacters = i + 1;

            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(0.001f, typingSpeed));
            else
                yield return new WaitForSeconds(
                    Mathf.Max(0.001f, typingSpeed));
        }

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0f, displayDuration));
        else
            yield return new WaitForSeconds(
                Mathf.Max(0f, displayDuration));

        CloseInstructionPanel();

        displayCoroutine = null;
    }

    public void SkipTyping()
    {
        if (displayCoroutine == null)
            return;

        StopCoroutine(displayCoroutine);
        displayCoroutine = StartCoroutine(HideAfterDuration());
    }

    private IEnumerator HideAfterDuration()
    {
        instructionText.maxVisibleCharacters = int.MaxValue;

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0f, displayDuration));
        else
            yield return new WaitForSeconds(
                Mathf.Max(0f, displayDuration));

        CloseInstructionPanel();

        displayCoroutine = null;
    }

    private void CloseInstructionPanel()
    {
        if (instructionPanel != null)
            instructionPanel.SetActive(false);

        if (instructionText != null)
            instructionText.maxVisibleCharacters = int.MaxValue;

        RestorePlayerControls();

        // Unlock only the instruction panel's pause lock.
        if (panelLockActive)
        {
            panelLockActive = false;

            if (Pauser.instance != null)
                Pauser.instance.SetInstructionPanelLock(false);
        }
    }

    private void RestorePlayerControls()
    {
        if (playerCar != null)
            playerCar.canControl = controlsWereEnabled;
    }

    private void OnDisable()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        RestorePlayerControls();

        if (panelLockActive)
        {
            panelLockActive = false;

            if (Pauser.instance != null)
                Pauser.instance.SetInstructionPanelLock(false);
        }
    }
}