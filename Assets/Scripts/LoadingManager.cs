using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;

    public GameObject loadingPanel; 

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowLoading()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);
        else
            Debug.LogWarning("loadingPanel no asignado en LoadingManager");
    }
}