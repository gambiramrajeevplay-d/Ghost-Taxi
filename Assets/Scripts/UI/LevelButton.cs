using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelButton : MonoBehaviour
{
    public int levelToLoad;

    [Header("Scene Settings")]
    public string sceneName = "Tram Simulation";

    public void LoadLevel()
    {
        PlayerPrefs.SetInt(StringsData.levelToLoad, levelToLoad);
        PlayerPrefs.Save();

        SceneManager.LoadScene(sceneName);
    }

}