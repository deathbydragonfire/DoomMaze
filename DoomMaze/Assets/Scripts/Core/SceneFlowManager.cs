using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton that handles all scene loading — single and additive —
/// with async support and loading-screen hooks via EventBus.
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    private bool _isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // When Bootstrap is the entry point (no other scene loaded yet), drive to MainMenu.
        // When Bootstrap was loaded additively by RuntimeBootstrapper, a gameplay scene is
        // already active, so we skip the MainMenu transition entirely.
        if (SceneManager.sceneCount == 1)
        {
            GameManager.Instance?.SetState(GameState.MainMenu);
            LoadScene("MainMenu");
        }
    }

    /// <summary>
    /// Loads a scene asynchronously in single or additive mode.
    /// Silently ignores the request if a load is already in progress.
    /// </summary>
    public void LoadScene(string sceneName, bool additive = false)
    {
        if (_isLoading)
        {
            Debug.LogWarning($"[SceneFlowManager] Load request for '{sceneName}' ignored — already loading.");
            return;
        }

        StartCoroutine(LoadSceneCoroutine(sceneName, additive));
    }

    /// <summary>Loads the next scene by build index.</summary>
    public void LoadNextScene()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("[SceneFlowManager] LoadNextScene called but there is no next scene in Build Settings.");
            return;
        }

        LoadScene(SceneManager.GetSceneByBuildIndex(nextIndex).name);
    }

    /// <summary>Reloads the currently active scene.</summary>
    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    private IEnumerator LoadSceneCoroutine(string sceneName, bool additive)
    {
        _isLoading = true;

        GameManager.Instance?.SetState(GameState.Loading);

        EventBus<SceneLoadRequestEvent>.Raise(new SceneLoadRequestEvent
        {
            SceneName = sceneName,
            Additive = additive
        });

        LoadSceneMode mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);

        while (!operation.isDone)
        {
            yield return null;
        }

        _isLoading = false;

        if (!additive)
        {
            GameManager.Instance?.SetState(GameState.Playing);
        }
    }
}
