using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class SplashScreen : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image logoImage;          // Assign your logo Image here
    [SerializeField] private Sprite logoSprite;        // Optional: assign sprite here instead of on the Image
    [SerializeField] private GameObject pressAnyText;  // Optional: "Press any button" text object

    [Header("Settings")]
    [SerializeField] private float fadeInTime = 1.5f;
    [SerializeField] private string nextScene = "MainMenu";

    private bool canContinue = false;
    private bool isLoading = false;

    private void Awake()
    {
        Application.targetFrameRate = 60; // Set a target frame rate for smoother animations
    }

    private void Start()
    {
        if (logoSprite != null)
            logoImage.sprite = logoSprite;

        // Start fully transparent
        SetLogoAlpha(0f);
        if (pressAnyText != null) pressAnyText.SetActive(false);

        StartCoroutine(FadeInLogo());
    }

    private IEnumerator FadeInLogo()
    {
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            SetLogoAlpha(Mathf.Clamp01(t / fadeInTime));
            yield return null;
        }

        SetLogoAlpha(1f);
        canContinue = true;
        if (pressAnyText != null) pressAnyText.SetActive(true);
    }

    private void Update()
    {
        if (!canContinue || isLoading) return;

        if (AnyInputPressed())
        {
            isLoading = true;
            SceneManager.LoadScene(nextScene);
        }
    }

    private bool AnyInputPressed()
    {
        // Keyboard: any key
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        // Mouse buttons
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
            return true;

        // Gamepad: any button
        if (Gamepad.current != null)
        {
            foreach (var control in Gamepad.current.allControls)
            {
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button
                    && !control.synthetic
                    && button.wasPressedThisFrame)
                    return true;
            }
        }

        // Touchscreen tap
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        return false;
    }

    private void SetLogoAlpha(float a)
    {
        Color c = logoImage.color;
        c.a = a;
        logoImage.color = c;
    }
}