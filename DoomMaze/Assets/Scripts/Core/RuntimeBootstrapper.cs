using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Placed in every non-Bootstrap scene. On Start it checks whether the manager
/// singletons are present (indicating Bootstrap already ran); if not, it loads
/// Bootstrap additively and waits for it to complete before signalling Playing.
/// This lets any scene be launched directly from the editor during development.
/// </summary>
public class RuntimeBootstrapper : MonoBehaviour
{
    private const string BOOTSTRAP_SCENE_NAME = "Bootstrap";

    private IEnumerator Start()
    {
        if (GameManager.Instance == null)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(BOOTSTRAP_SCENE_NAME, LoadSceneMode.Additive);
            while (!load.isDone)
                yield return null;
        }

        yield return null;

        PauseManager.Instance?.TryBindInput();
        EnsurePlayerDecayComponent();

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.SetState(GameState.Playing);
    }

    private static void EnsurePlayerDecayComponent()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        if (player.GetComponentInParent<PlayerDecayComponent>() == null)
            player.AddComponent<PlayerDecayComponent>();
    }
}
