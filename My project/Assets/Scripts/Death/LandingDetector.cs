using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>Detects first contact with the world after death. Added by DeathManager.</summary>
    public class LandingDetector : MonoBehaviour
    {
        public bool Landed { get; private set; }
        public Vector3 LandPoint { get; private set; }
        LayerMask mask = ~0;

        public void Init(LayerMask m) { mask = m; Landed = false; }

        void OnCollisionEnter(Collision c)
        {
            if (Landed || ((1 << c.gameObject.layer) & mask) == 0) return;
            Landed = true;
            LandPoint = c.GetContact(0).point;
        }
    }
}
