using UnityEngine;

// Something she presses with her finger (PartnerKeypadUser): a keypad, a slot machine's spin button...
// This transform = the panel, forward = out of it toward her (she stands in front, at StandDistance).
public abstract class ButtonPanel : MonoBehaviour
{
    // her fingertip at the press is this far in front of her (HelenaButton_Pushing): where she stands
    public virtual float StandDistance => 0.43f;

    // she keeps her hand up waiting for the next key this long, then lets it down (s)
    public virtual float HoldTime => 8f;

    // true: the hand stays up between keys (a code, key after key); false: down after each press, she waits in front
    public virtual bool KeepHandUp => true;

    // where her fingertip goes for that key (world); false = no such key
    public abstract bool TryGetKey(char key, out Vector3 position);

    // the key is pressed (her finger touches it)
    public abstract void Press(char key);

    // a game played here (slot machine, roulette): where a close camera shows her and the game while she is at it
    // (GameCameraDirector). False = no such view (a keypad).
    public virtual bool TryGetGameView(out Vector3 from, out Vector3 lookAt)
    {
        from = lookAt = Vector3.zero;
        return false;
    }
}
