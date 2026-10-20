using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardBattle
{
    /// <summary>Global setup + scene-level services (restart). Attach once to the "GameSystems" object.</summary>
    [DefaultExecutionOrder(-100)] // runs before every other script so the material references below are ready
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] int targetFrameRate = 60;
        [Tooltip("Transparent material (Sprites/Default) used for particles, trails, rings and bolts. Assigned by the scene builder.")]
        [SerializeField] Material overlayMaterial;
        [Tooltip("Any material that uses the project's normal lit shader (Standard / URP Lit). Referencing it here keeps the shader in builds. Assigned by the scene builder.")]
        [SerializeField] Material litMaterial;

        void Awake()
        {
            Instance = this;
            if (overlayMaterial != null) PlaceholderUtil.OverlayBase = overlayMaterial;
            if (litMaterial != null) PlaceholderUtil.LitBase = litMaterial;
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;
            Time.timeScale = 1f;
        }

        /// <summary>Reloads the scene. This is the safest way to guarantee a completely clean match.</summary>
        public void RestartMatch()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
