using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RebindResult
{
    Success,
    Cancelled,
    Conflict
}

/// <summary>
/// Key rebinding helper. Works on any InputActionAsset, so it behaves the same in the
/// main menu and in game. Overrides are saved to PlayerPrefs as JSON and loaded by InputManager.
/// </summary>
public static class InputRebinder
{
    private const string SaveKey = "input.bindingOverrides";

    /// <summary>If true, a key already used by another action is rejected.</summary>
    public static bool BlockConflicts = true;

    private static InputActionRebindingExtensions.RebindingOperation operation;
    private static int endFrame = -1;

    /// <summary>Fired after a rebind succeeds or bindings are reset.</summary>
    public static event Action BindingsChanged;

    /// <summary>
    /// True while waiting for a key, and for the rest of the frame it ended. Check this in
    /// your pause logic so the Escape that cancels a rebind doesn't also toggle pause.
    /// </summary>
    public static bool IsRebinding => operation != null || Time.frameCount <= endFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        operation = null;
        endFrame = -1;
        BindingsChanged = null;
    }

    // ---------- Save / load ----------

    public static void LoadSaved(InputActionAsset asset)
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(json);
    }

    public static void Save(InputActionAsset asset)
    {
        PlayerPrefs.SetString(SaveKey, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void ResetAll(InputActionAsset asset)
    {
        Cancel();
        asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(SaveKey);
        BindingsChanged?.Invoke();
    }

    public static void ResetBinding(InputAction action, int bindingIndex)
    {
        Cancel();
        action.RemoveBindingOverride(bindingIndex);
        Save(action.actionMap.asset);
        BindingsChanged?.Invoke();
    }

    // ---------- Lookup ----------

    /// <summary>
    /// Finds the binding to edit. bindingGroup is e.g. "Keyboard&amp;Mouse" or "Gamepad" (empty = any).
    /// compositePart is "up", "down", "left" or "right" for composites like Move, empty otherwise.
    /// </summary>
    public static int FindBindingIndex(InputAction action, string bindingGroup, string compositePart)
    {
        var bindings = action.bindings;
        string compositeGroups = string.Empty;

        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding b = bindings[i];

            if (b.isComposite)
            {
                compositeGroups = b.groups ?? string.Empty;
                continue;
            }

            // Composite parts may have no groups of their own, so inherit from the composite.
            string groups = (b.isPartOfComposite && string.IsNullOrEmpty(b.groups))
                ? compositeGroups
                : (b.groups ?? string.Empty);

            if (!string.IsNullOrEmpty(bindingGroup) && !groups.Contains(bindingGroup)) continue;

            if (string.IsNullOrEmpty(compositePart))
            {
                if (b.isPartOfComposite) continue;
            }
            else if (!b.isPartOfComposite || !string.Equals(b.name, compositePart, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    public static string GetDisplayString(InputAction action, int bindingIndex)
    {
        return action.GetBindingDisplayString(bindingIndex);
    }

    // ---------- Rebinding ----------

    public static void Cancel()
    {
        operation?.Cancel();
    }

    /// <param name="onFinished">Called with the result, plus the name of the conflicting action (Conflict only).</param>
    public static void StartRebind(InputAction action, int bindingIndex, string bindingGroup, Action<RebindResult, string> onFinished)
    {
        if (action == null || bindingIndex < 0) return;

        Cancel(); // only one rebind at a time

        string previousOverride = action.bindings[bindingIndex].overridePath;
        bool wasEnabled = action.enabled;
        action.Disable(); // an action can't be rebound while it is enabled

        InputActionRebindingExtensions.RebindingOperation op = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .OnMatchWaitForAnother(0.1f);

        if (!string.IsNullOrEmpty(bindingGroup) && bindingGroup.Contains("Gamepad"))
        {
            op.WithControlsHavingToMatchPath("<Gamepad>");
        }
        else
        {
            op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
        }

        operation = op
            .OnCancel(o => Finish(o, RebindResult.Cancelled, null))
            .OnComplete(o =>
            {
                if (BlockConflicts && TryFindConflict(action, bindingIndex, out string otherName))
                {
                    // Put the old key back.
                    action.ApplyBindingOverride(bindingIndex, previousOverride);
                    Finish(o, RebindResult.Conflict, otherName);
                }
                else
                {
                    Save(action.actionMap.asset);
                    Finish(o, RebindResult.Success, null);
                }
            })
            .Start();

        void Finish(InputActionRebindingExtensions.RebindingOperation o, RebindResult result, string info)
        {
            o.Dispose();
            if (operation == o) operation = null;
            endFrame = Time.frameCount;

            if (wasEnabled) action.Enable();
            if (result == RebindResult.Success) BindingsChanged?.Invoke();

            onFinished?.Invoke(result, info);
        }
    }

    private static bool TryFindConflict(InputAction action, int bindingIndex, out string otherName)
    {
        string path = action.bindings[bindingIndex].effectivePath;

        foreach (InputAction other in action.actionMap.actions)
        {
            var bindings = other.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (other == action && i == bindingIndex) continue;

                InputBinding b = bindings[i];
                if (b.isComposite) continue;
                if (!string.Equals(b.effectivePath, path, StringComparison.OrdinalIgnoreCase)) continue;

                otherName = b.isPartOfComposite ? $"{other.name} ({b.name})" : other.name;
                return true;
            }
        }

        otherName = null;
        return false;
    }
}