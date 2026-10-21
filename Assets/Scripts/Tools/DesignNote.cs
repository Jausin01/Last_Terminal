using UnityEngine;

// A note on an object of the scene for whoever reads the scene report (Last Terminal/15): what this thing is, what it
// should do ("PC: he sits here and opens the camera program", "this door goes to the corridor, locked until...").
// Only a note: it does nothing in the game. Shown as a yellow circle + its first line in the Scene view.
public class DesignNote : MonoBehaviour
{
    [TextArea(2, 8)] public string note = "";

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.82f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.12f);
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(note))
        {
            string first = note.Split('\n')[0];
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.18f, first.Length > 40 ? first.Substring(0, 40) + "..." : first);
        }
#endif
    }
}
