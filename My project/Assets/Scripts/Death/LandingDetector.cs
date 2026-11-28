using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>Detects the first supporting contact and settles a falling butterfly there.</summary>
    public class LandingDetector : MonoBehaviour
    {
        public bool Landed { get; private set; }
        public Vector3 LandPoint { get; private set; }

        LayerMask mask;
        Rigidbody body;
        SphereCollider sphere;
        Terrain terrain;
        Vector3? lastSurface;

        public void Init(LayerMask ground, Rigidbody fallingBody, SphereCollider collider, Vector3? lastPosition)
        {
            mask = ground;
            body = fallingBody;
            sphere = collider;
            terrain = Terrain.activeTerrain;
            lastSurface = lastPosition;
            Landed = false;
        }

        void FixedUpdate()
        {
            if (Landed || !body || !terrain || ((1 << terrain.gameObject.layer) & mask) == 0)
                return;

            Vector3 center = sphere.bounds.center;
            Vector3 foot = center - Vector3.up * sphere.bounds.extents.y;
            Vector3 point = foot;
            if (ButterflyController.TryGetTerrainFloor(terrain, foot, out float floor))
            {
                lastSurface = foot;
                if (foot.y >= floor - .025f)
                    return;
            }
            else
            {
                if (foot.y >= terrain.transform.position.y - 1f || !lastSurface.HasValue)
                    return;
                point = lastSurface.Value;
                if (!ButterflyController.TryGetTerrainFloor(terrain, point, out floor))
                    return;
            }

            point.y = floor;
            body.position += point - foot + Vector3.up * .01f;
            Land(point);
        }

        void OnCollisionEnter(Collision collision) => CheckSupport(collision);
        void OnCollisionStay(Collision collision) => CheckSupport(collision);

        void CheckSupport(Collision collision)
        {
            if (Landed || !body || ((1 << collision.gameObject.layer) & mask) == 0)
                return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y > .2f)
                {
                    Land(contact.point);
                    return;
                }
            }
        }

        void Land(Vector3 point)
        {
            Landed = true;
            LandPoint = point;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
        }
    }
}
