using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    public bool eqOpen;
    public bool pauseOpen;
    public bool isServerOnly;
    public bool consoleOpen;
    public bool is079;
    public bool scp106;
    public bool roundStarted;
    public bool raOp;
    public bool plOp;
    public bool debuglogopen;
    public bool isNotFacility;
    public bool isApplicationNotFocused;

    public static CursorManager singleton { get; private set; }

    private void Awake()
    {
        if (singleton == null)
        {
            singleton = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        if (!ServerStatic.IsDedicated)
        {
            bool shouldUnlock = eqOpen || pauseOpen || isServerOnly || consoleOpen ||
                              is079 || scp106 || roundStarted || raOp ||
                              plOp || debuglogopen || isNotFacility || isApplicationNotFocused;

            Cursor.lockState = shouldUnlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = shouldUnlock;
        }
    }

    public static bool ShouldBeBlurred()
    {
        return singleton != null && (singleton.eqOpen || singleton.pauseOpen || singleton.plOp);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneWasLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneWasLoaded;
    }

    private void OnSceneWasLoaded(Scene scene, LoadSceneMode mode)
    {
        UnsetAll();
        isNotFacility = true;

        if (NonFacilityCompatibility.singleton != null)
        {
            foreach (var sceneDesc in NonFacilityCompatibility.singleton.allScenes)
            {
                if (sceneDesc.sceneName == scene.name)
                {
                    isNotFacility = false;
                    break;
                }
            }
        }
    }

    private void OnApplicationFocus(bool focus)
    {
        isApplicationNotFocused = !focus;
    }

    public static void UnsetAll()
    {
        if (singleton == null) return;

        singleton.eqOpen = false;
        singleton.pauseOpen = false;
        singleton.isServerOnly = false;
        singleton.consoleOpen = false;
        singleton.is079 = false;
        singleton.scp106 = false;
        singleton.roundStarted = false;
        singleton.raOp = false;
        singleton.plOp = false;
        singleton.debuglogopen = false;
    }
}