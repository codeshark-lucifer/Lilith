using UnityEngine;

[RequireComponent(typeof(PlayerControls))]
public class PlayerFootStep : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource actionSource; // jump/land; auto-created if empty
    [SerializeField] private AudioClip[] footsteps;
    [SerializeField] private AudioClip[] jumpClips;
    [SerializeField] private AudioClip[] landClips;

    [Header("Timing")]
    [SerializeField] private float stepInterval = 0.6f;
    [SerializeField] private float walkMultiplier = 0.65f;
    [SerializeField] private float sprintMultiplier = 0.45f;
    [SerializeField] private float crouchMultiplier = 0.8f;

    [Header("Sound")]
    [Range(0f, 1f)][SerializeField] private float stepWidth = 0.3f;
    [Range(0f, 1f)][SerializeField] private float crouchVolume = 0.4f;
    [Range(0.5f, 1.5f)][SerializeField] private float leftPitch = 0.97f;
    [Range(0.5f, 1.5f)][SerializeField] private float rightPitch = 1.03f;
    [Range(0f, 0.3f)][SerializeField] private float pitchVariation = 0.08f;

    [Header("Jump / Land")]
    [Range(0f, 1f)][SerializeField] private float jumpVolume = 0.8f;
    [Tooltip("Impacts slower than this (m/s) make no landing sound, so steps and slopes stay quiet.")]
    [SerializeField] private float minLandSpeed = 3f;
    [Tooltip("Impact speed (m/s) at which the landing sound reaches full volume.")]
    [SerializeField] private float hardLandSpeed = 10f;
    [Range(0f, 1f)][SerializeField] private float softLandVolume = 0.4f;

    private PlayerControls controls;
    private int lastStepIndex = -1;
    private int lastJumpIndex = -1;
    private int lastLandIndex = -1;
    private bool rightStep;
    private float nextStepTime;

    private void Awake()
    {
        controls = GetComponent<PlayerControls>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (audioSource == null || footsteps == null || footsteps.Length == 0)
        {
            Debug.LogError($"{nameof(PlayerFootStep)} on {name} is missing an AudioSource or footstep clips.", this);
            enabled = false;
            return;
        }

        if (actionSource == null)
        {
            actionSource = gameObject.AddComponent<AudioSource>();
            actionSource.playOnAwake = false;
            actionSource.spatialBlend = audioSource.spatialBlend;
            actionSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        }
    }

    private void OnEnable()
    {
        controls.Jumped += OnJumped;
        controls.Landed += OnLanded;
    }

    private void OnDisable()
    {
        controls.Jumped -= OnJumped;
        controls.Landed -= OnLanded;
    }

    private void Update()
    {
        bool moving = controls.MoveDirection.sqrMagnitude > 0.01f && controls.IsGrounded;
        if (!moving) return;

        if (Time.time < nextStepTime) return;

        float multiplier = controls.IsSprinting ? sprintMultiplier
                         : controls.IsCrouching ? crouchMultiplier
                         : walkMultiplier;

        TakeStep();
        nextStepTime = Time.time + stepInterval * multiplier;
    }

    private void OnJumped()
    {
        PlayAction(jumpClips, ref lastJumpIndex, jumpVolume);
    }

    private void OnLanded(float impactSpeed)
    {
        if (impactSpeed < minLandSpeed) return;

        float t = Mathf.InverseLerp(minLandSpeed, hardLandSpeed, impactSpeed);
        float volume = Mathf.Lerp(softLandVolume, 1f, t);
        PlayAction(landClips, ref lastLandIndex, volume);

        // Don't let a regular footstep fire right on top of the landing sound.
        nextStepTime = Time.time + stepInterval * 0.5f;
    }

    private void PlayAction(AudioClip[] clips, ref int lastIndex, float volume)
    {
        if (clips == null || clips.Length == 0) return;

        int index = PickIndex(clips.Length, lastIndex);
        lastIndex = index;

        actionSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        actionSource.PlayOneShot(clips[index], volume);
    }

    private void TakeStep()
    {
        int index = PickIndex(footsteps.Length, lastStepIndex);
        lastStepIndex = index;

        // Alternate feet: right foot -> right ear, left foot -> left ear.
        rightStep = !rightStep;
        audioSource.panStereo = rightStep ? stepWidth : -stepWidth;

        float basePitch = rightStep ? rightPitch : leftPitch;
        audioSource.pitch = basePitch + Random.Range(-pitchVariation, pitchVariation);

        float volume = controls.IsCrouching ? crouchVolume : 1f;
        audioSource.PlayOneShot(footsteps[index], volume);
    }

    // Random index that differs from the previous one.
    private static int PickIndex(int length, int lastIndex)
    {
        if (length <= 1) return 0;

        int index = Random.Range(0, length - 1);
        if (lastIndex >= 0 && index >= lastIndex) index++;
        return index;
    }
}