using UnityEngine;

/// <summary>
/// Procedural talking animation using a facial blend shape.
/// Works with the DialogueManager's existing events.
/// </summary>
public class NPCMouthAnimation : MonoBehaviour
{
    [Header("Face")]
    [SerializeField] private SkinnedMeshRenderer faceRenderer;

    [Tooltip("Exact blend shape name in the mesh.")]
    [SerializeField] private string mouthShapeName = "Mouth_O";

    [Header("Animation")]
    [Tooltip("Minimum mouth opening weight.")]
    [SerializeField, Range(0f, 100f)]
    private float closedWeight = 0f;

    [Tooltip("Maximum mouth opening weight.")]
    [SerializeField, Range(0f, 100f)]
    private float openWeight = 80f;

    [Tooltip("How quickly the mouth changes shape.")]
    [SerializeField, Min(0.1f)]
    private float animationSpeed = 12f;

    [Tooltip("How quickly the mouth opens and closes.")]
    [SerializeField, Min(0.1f)]
    private float talkingSpeed = 7f;

    [Header("Behaviour")]
    [SerializeField] private bool animateOnDialogueLine = true;
    [SerializeField] private bool useUnscaledTime = true;

    private DialogueManager manager;
    private int mouthShapeIndex = -1;

    private bool isTalking;
    private float currentWeight;
    private float targetWeight;
    private float talkingTimer;

    private void Awake()
    {
        FindMouthShape();

        currentWeight = closedWeight;
        targetWeight = closedWeight;

        ApplyWeight(closedWeight);
    }

    private void OnEnable()
    {
        manager = DialogueManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning(
                $"[{nameof(NPCMouthAnimation)}] " +
                "No DialogueManager was found.",
                this
            );

            return;
        }

        manager.OnDialogueStarted += HandleDialogueStarted;
        manager.OnLine += HandleDialogueLine;
        manager.OnDialogueEnded += HandleDialogueEnded;

        // Support enabling this component during a conversation.
        if (manager.IsRunning)
            isTalking = true;
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.OnDialogueStarted -= HandleDialogueStarted;
            manager.OnLine -= HandleDialogueLine;
            manager.OnDialogueEnded -= HandleDialogueEnded;
        }

        isTalking = false;
        targetWeight = closedWeight;
        currentWeight = closedWeight;

        ApplyWeight(closedWeight);
    }

    private void Update()
    {
        if (faceRenderer == null || mouthShapeIndex < 0)
            return;

        float deltaTime = useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (isTalking)
        {
            talkingTimer += deltaTime * talkingSpeed;

            // Smoothly vary the mouth between closed and open.
            float wave = (Mathf.Sin(talkingTimer) + 1f) * 0.5f;

            // Occasionally close the mouth between syllables.
            float syllable = Mathf.Sin(talkingTimer * 0.47f);

            if (syllable < -0.75f)
                wave *= 0.15f;

            targetWeight = Mathf.Lerp(
                closedWeight,
                openWeight,
                wave
            );
        }
        else
        {
            targetWeight = closedWeight;
        }

        // Smooth transitions prevent abrupt blend shape changes.
        float t = 1f - Mathf.Exp(-animationSpeed * deltaTime);

        currentWeight = Mathf.Lerp(
            currentWeight,
            targetWeight,
            t
        );

        ApplyWeight(currentWeight);
    }

    private void FindMouthShape()
    {
        if (faceRenderer == null)
            faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        if (faceRenderer == null || faceRenderer.sharedMesh == null)
        {
            Debug.LogWarning(
                $"[{nameof(NPCMouthAnimation)}] " +
                "No SkinnedMeshRenderer was found.",
                this
            );

            return;
        }

        mouthShapeIndex = faceRenderer.sharedMesh
            .GetBlendShapeIndex(mouthShapeName);

        if (mouthShapeIndex < 0)
        {
            Debug.LogWarning(
                $"[{nameof(NPCMouthAnimation)}] " +
                $"Blend shape '{mouthShapeName}' was not found " +
                $"on '{faceRenderer.name}'.",
                this
            );
        }
    }

    private void HandleDialogueStarted(DialogueGraphSO graph)
    {
        isTalking = true;
        talkingTimer = Random.Range(0f, Mathf.PI * 2f);
    }

    private void HandleDialogueLine(DialogueLine line)
    {
        if (animateOnDialogueLine)
        {
            isTalking = true;
            talkingTimer = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void HandleDialogueEnded()
    {
        isTalking = false;
        targetWeight = closedWeight;
    }

    private void ApplyWeight(float weight)
    {
        if (faceRenderer == null || mouthShapeIndex < 0)
            return;

        faceRenderer.SetBlendShapeWeight(
            mouthShapeIndex,
            weight
        );
    }

    /// <summary>Manually start the talking animation.</summary>
    public void StartTalking()
    {
        isTalking = true;
    }

    /// <summary>Manually stop talking and close the mouth.</summary>
    public void StopTalking()
    {
        isTalking = false;
        targetWeight = closedWeight;
    }
}