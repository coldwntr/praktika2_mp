using UnityEngine;

/// <summary>
/// Ensures screen-space Canvas is visible in builds (fixes zero scale on root Canvas).
/// Attach to the root Canvas object.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameUiCanvasFix : MonoBehaviour
{
    private void Awake()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
            return;

        if (rectTransform.localScale.sqrMagnitude < 0.001f)
        {
            rectTransform.localScale = Vector3.one;
            Debug.Log("[GameUiCanvasFix] Canvas scale was 0 — reset to (1,1,1).");
        }
    }
}
