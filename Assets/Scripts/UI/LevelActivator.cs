using UnityEngine;

public class LevelActivator : MonoBehaviour
{
    public GameObject[] levels;

    private void Awake()
    {
        int levelToLoad = PlayerPrefs.GetInt(StringsData.levelToLoad, 0);

        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("LevelActivator: No levels assigned.", this);
            return;
        }

        if (levelToLoad < 0 || levelToLoad >= levels.Length)
        {
            Debug.LogError(
                "LevelActivator: Invalid level index: " + levelToLoad,
                this);
            return;
        }

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null)
                levels[i].SetActive(i == levelToLoad);
        }
    }

}