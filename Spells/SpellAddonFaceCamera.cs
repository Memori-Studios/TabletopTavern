using UnityEngine;

namespace TJ.Spells
{
    // Turns flat addon art about the vertical axis toward the camera, as squad flags do, so it never shows its edge.
    public class SpellAddonFaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera view = Camera.main;
            if (view == null) return;
            Vector3 forward = view.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}
