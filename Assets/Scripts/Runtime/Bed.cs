using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>
/// Bed interactable that disables player controls, starts the timeline cutscene placed inside the Bed,
/// animates the player entering and exiting the bed, skips time to morning, and restores controls when finished.
/// </summary>
public class Bed : InteractableBase
{
    [Header("Timeline Cutscene")]
    [Tooltip("PlayableDirector on or inside Bed. If unassigned, automatically found in children.")]
    [SerializeField] private PlayableDirector director;

    [Tooltip("Optional preview dummy character inside the timeline (e.g. character@olia) to keep hidden during gameplay.")]
    [SerializeField] private GameObject previewDummy;

    [Header("Alignment")]
    [Tooltip("Transform where the player is placed before the cutscene starts. If null, uses this Bed's transform.")]
    [SerializeField] private Transform sleepPivot;

    [Header("Cutscene Camera")]
    [Tooltip("Optional cutscene camera (CinemachineCamera or Camera) to activate during sleep. If null, an automatic camera is created.")]
    [SerializeField] private GameObject cutsceneCamera;

    [Header("Sleep System & Time Skip")]
    [Tooltip("Hour to wake up at when sleeping (e.g. 7 = 7:00 AM).")]
    [Range(0f, 24f)]
    [SerializeField] private float wakeUpHour = 7f;

    [Tooltip("Seconds after timeline start to advance in-game time (during the black screen).")]
    [SerializeField] private float timeSkipDelay = 4.0f;

    [Header("Screen Fade")]
    [Tooltip("Optional CanvasGroup for screen fading. If null, a runtime fade overlay is automatically created.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Tooltip("Seconds after timeline start to begin fading out to black.")]
    [SerializeField] private float fadeOutStart = 2.5f;

    [Tooltip("Duration of the fade to black.")]
    [SerializeField] private float fadeOutDuration = 0.8f;

    [Tooltip("Seconds after timeline start to begin fading back in.")]
    [SerializeField] private float fadeInStart = 4.6f;

    [Tooltip("Duration of the fade back in.")]
    [SerializeField] private float fadeInDuration = 0.8f;

    private bool isSleeping;
    private Coroutine sleepCoroutine;

    // Cached player components while cutscene is active
    private PlayerInteractor cachedInteractor;
    private PlayerControls cachedPlayerControls;
    private CameraControls cachedCameraControls;
    private PlayerAnimation cachedPlayerAnimation;
    private FirstPersonBody cachedFirstPersonBody;
    private CharacterController cachedCharacterController;
    private UnityEngine.Animations.Rigging.Rig cachedRig;
    private MonoBehaviour cachedFootIK;

    private void Awake()
    {
        prompt = "Sleep";

        if (director == null)
            director = GetComponentInChildren<PlayableDirector>(true);

        if (director != null)
        {
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
        }

        if (previewDummy == null && director != null)
        {
            Transform dummy = director.transform.Find("character@olia");
            if (dummy != null) previewDummy = dummy.gameObject;
        }

        if (previewDummy != null)
            previewDummy.SetActive(false);

        if (cutsceneCamera != null)
            cutsceneCamera.SetActive(false);
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        return base.CanInteract(interactor) && !isSleeping;
    }

    public override string GetPrompt(PlayerInteractor interactor)
    {
        return isSleeping ? string.Empty : prompt;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (isSleeping || interactor == null) return;

        base.Interact(interactor);
        StartSleepSequence(interactor);
    }

    private void StartSleepSequence(PlayerInteractor interactor)
    {
        isSleeping = true;
        cachedInteractor = interactor;

        Transform playerRoot = interactor.transform;
        // Search root or parents/children for player components
        cachedPlayerControls = playerRoot.GetComponentInParent<PlayerControls>() ?? playerRoot.GetComponentInChildren<PlayerControls>();
        cachedCameraControls = playerRoot.GetComponentInParent<CameraControls>() ?? playerRoot.GetComponentInChildren<CameraControls>();
        cachedPlayerAnimation = playerRoot.GetComponentInParent<PlayerAnimation>() ?? playerRoot.GetComponentInChildren<PlayerAnimation>();
        cachedFirstPersonBody = playerRoot.GetComponentInParent<FirstPersonBody>() ?? playerRoot.GetComponentInChildren<FirstPersonBody>();
        cachedCharacterController = playerRoot.GetComponentInParent<CharacterController>() ?? playerRoot.GetComponentInChildren<CharacterController>();
        cachedRig = playerRoot.GetComponentInChildren<UnityEngine.Animations.Rigging.Rig>();

        // Find FootIK dynamically if present
        var monoBehaviours = playerRoot.GetComponentsInChildren<MonoBehaviour>();
        foreach (var mb in monoBehaviours)
        {
            if (mb != null && mb.GetType().Name.Contains("FootIK"))
            {
                cachedFootIK = mb;
                break;
            }
        }

        Animator playerAnimator = playerRoot.GetComponentInChildren<Animator>();

        // 1. Lock interaction
        interactor.InputLocked = true;

        // 2. Change GameState to Cutscene
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameState(GameState.Cutscene);

        // 3. Disable player controls and helpers
        if (cachedPlayerControls != null) cachedPlayerControls.enabled = false;
        if (cachedCameraControls != null) cachedCameraControls.enabled = false;
        if (cachedPlayerAnimation != null) cachedPlayerAnimation.enabled = false;
        if (cachedFirstPersonBody != null) cachedFirstPersonBody.enabled = false;
        if (cachedCharacterController != null) cachedCharacterController.enabled = false;
        if (cachedRig != null) cachedRig.weight = 0f;
        if (cachedFootIK != null) cachedFootIK.enabled = false;

        // 4. Align player to bed sleep pivot
        Transform alignTarget = sleepPivot != null ? sleepPivot : transform;
        Transform moveRoot = cachedCharacterController != null ? cachedCharacterController.transform : playerRoot;
        moveRoot.position = alignTarget.position;
        moveRoot.rotation = alignTarget.rotation;

        // 5. Ensure preview dummy is inactive
        if (previewDummy != null) previewDummy.SetActive(false);

        // 6. Bind Player Animator to Director's Animation Tracks
        if (director != null && director.playableAsset != null && playerAnimator != null)
        {
            foreach (var output in director.playableAsset.outputs)
            {
                if (output.sourceObject is AnimationTrack)
                {
                    director.SetGenericBinding(output.sourceObject, playerAnimator);
                }
            }
        }

        // 7. Ensure and activate cutscene camera
        EnsureCutsceneCamera();
        if (cutsceneCamera != null)
            cutsceneCamera.SetActive(true);

        // 8. Play timeline and start sequence coroutine
        if (director != null)
        {
            director.time = 0;
            director.Play();
        }

        EnsureFadeOverlay();

        if (sleepCoroutine != null) StopCoroutine(sleepCoroutine);
        sleepCoroutine = StartCoroutine(SleepRoutine());
    }

    private IEnumerator SleepRoutine()
    {
        float duration = director != null ? (float)director.duration : 8.0f;
        if (duration <= 0.1f) duration = 8.0f;

        float timer = 0f;
        bool hasSkippedTime = false;

        while (timer < duration)
        {
            float dt = Time.deltaTime;
            timer += dt;

            // Handle fade out to black
            if (timer >= fadeOutStart && timer < fadeOutStart + fadeOutDuration)
            {
                if (fadeCanvasGroup != null)
                {
                    float t = Mathf.Clamp01((timer - fadeOutStart) / fadeOutDuration);
                    fadeCanvasGroup.alpha = t;
                    fadeCanvasGroup.blocksRaycasts = true;
                }
            }
            else if (timer >= fadeOutStart + fadeOutDuration && timer < fadeInStart)
            {
                if (fadeCanvasGroup != null)
                {
                    fadeCanvasGroup.alpha = 1f;
                    fadeCanvasGroup.blocksRaycasts = true;
                }
            }

            // Handle time of day skip
            if (!hasSkippedTime && timer >= timeSkipDelay)
            {
                hasSkippedTime = true;
                AdvanceTimeOfDay();
            }

            // Handle fade in from black
            if (timer >= fadeInStart && timer < fadeInStart + fadeInDuration)
            {
                if (fadeCanvasGroup != null)
                {
                    float t = Mathf.Clamp01((timer - fadeInStart) / fadeInDuration);
                    fadeCanvasGroup.alpha = 1f - t;
                }
            }
            else if (timer >= fadeInStart + fadeInDuration)
            {
                if (fadeCanvasGroup != null)
                {
                    fadeCanvasGroup.alpha = 0f;
                    fadeCanvasGroup.blocksRaycasts = false;
                }
            }

            yield return null;
        }

        EndSleepSequence();
    }

    private void AdvanceTimeOfDay()
    {
        // Check for LightingManager
        if (Assets.Scripts.Runtime.DayNightCycle.LightingManager.Instance != null)
        {
            Assets.Scripts.Runtime.DayNightCycle.LightingManager.Instance.SkipToHour(wakeUpHour);
            return;
        }

        // Check for legacy DayNightCycle
        var legacy = FindFirstObjectByType<DayNightCycle>();
        if (legacy != null)
        {
            legacy.timeOfDay = Mathf.Repeat(wakeUpHour / 24f, 1f);
        }
    }

    private void EndSleepSequence()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        // Deactivate cutscene camera
        if (cutsceneCamera != null)
            cutsceneCamera.SetActive(false);

        // Stop director cleanly
        if (director != null && director.state == PlayState.Playing)
            director.Stop();

        // Re-enable player components
        if (cachedCharacterController != null)
        {
            cachedCharacterController.enabled = true;
            cachedCharacterController.Move(Vector3.down * 0.05f); // snap to ground
        }

        if (cachedPlayerAnimation != null) cachedPlayerAnimation.enabled = true;
        if (cachedFirstPersonBody != null) cachedFirstPersonBody.enabled = true;
        if (cachedFootIK != null) cachedFootIK.enabled = true;

        if (cachedCameraControls != null)
        {
            cachedCameraControls.enabled = true;
        }

        if (cachedPlayerControls != null) cachedPlayerControls.enabled = true;

        if (cachedInteractor != null)
        {
            cachedInteractor.InputLocked = false;
        }

        // Restore GameState to Playing
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameState(GameState.Playing);

        isSleeping = false;
        sleepCoroutine = null;
    }

    private void EnsureCutsceneCamera()
    {
        if (cutsceneCamera != null) return;

        // Check if an existing CutsceneCamera child is present
        Transform existing = transform.Find("CutsceneCamera");
        if (existing != null)
        {
            cutsceneCamera = existing.gameObject;
            return;
        }

        // Create a dedicated cutscene camera framing the bed
        GameObject camObj = new GameObject("CutsceneCamera");
        camObj.transform.SetParent(transform, false);
        camObj.transform.localPosition = new Vector3(-2.2f, 1.6f, 0.5f);
        camObj.transform.localRotation = Quaternion.Euler(20f, 65f, 0f);

        // Add CinemachineCamera if Unity.Cinemachine is present
        var cmType = System.Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine");
        if (cmType != null)
        {
            var cm = camObj.AddComponent(cmType);
            var priorityProp = cmType.GetProperty("Priority");
            if (priorityProp != null)
            {
                object priorityObj = priorityProp.GetValue(cm);
                if (priorityObj != null)
                {
                    var valProp = priorityObj.GetType().GetProperty("Value");
                    if (valProp != null && valProp.CanWrite)
                    {
                        valProp.SetValue(priorityObj, 25);
                        priorityProp.SetValue(cm, priorityObj);
                    }
                    else
                    {
                        var valField = priorityObj.GetType().GetField("Value");
                        if (valField != null)
                        {
                            valField.SetValue(priorityObj, 25);
                            priorityProp.SetValue(cm, priorityObj);
                        }
                    }
                }
            }
        }
        else
        {
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 60f;
        }

        cutsceneCamera = camObj;
    }

    private void EnsureFadeOverlay()
    {
        if (fadeCanvasGroup != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        GameObject overlayObj = new GameObject("SleepFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        if (canvas != null)
        {
            overlayObj.transform.SetParent(canvas.transform, false);
        }
        else
        {
            Canvas newCanvas = overlayObj.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 999;
            overlayObj.AddComponent<CanvasScaler>();
        }

        RectTransform rt = overlayObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Image img = overlayObj.GetComponent<Image>();
        img.color = Color.black;

        fadeCanvasGroup = overlayObj.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }

    private void OnDisable()
    {
        if (isSleeping)
        {
            EndSleepSequence();
        }
    }
}
