using UnityEngine;

/// <summary>
/// Esc frees the mouse cursor, clicking back on the game grabs it again.
/// Browsers also drop pointer lock on their own, so the click-to-relock path
/// matters more in a WebGL build than it does in the editor.
/// </summary>
public class CursorRelease : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            FirstPersonController.LockCursor(false);
            return;
        }

        if (!MatchManager.IsMatchRunning) return;

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            FirstPersonController.LockCursor(true);
    }
}
