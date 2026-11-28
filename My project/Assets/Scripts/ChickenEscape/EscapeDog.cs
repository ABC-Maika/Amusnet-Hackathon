using UnityEngine;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EscapeDog : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform model;

        static readonly int Speed = Animator.StringToHash("Speed");
        CharacterController motor;
        Vector3 velocity;
        Vector3 bondDirection;
        GameObject leash;
        float vertical;
        float barkAt;
        float heldUntil;
        float bondedAt = float.NegativeInfinity;

        public Vector3 Velocity => velocity;
        public float BarkCharge => Mathf.Clamp01(1f - (barkAt - Time.time) / 1.65f);
        public bool IsStopped => Time.time < heldUntil;
        public Vector3 TetherKnot { get; private set; }
        public Vector3 Nose => transform.position + transform.forward * .61f + Vector3.up * .52f;

        void Awake() => motor = GetComponent<CharacterController>();

        public void Tether()
        {
            var stake = transform.position + new Vector3(1.27f, 0f, -.64f);
            TetherKnot = stake + Vector3.up * .38f;
            leash = new GameObject("Dog tether");
            leash.transform.SetParent(transform, true);
            var material = model.GetComponentInChildren<Renderer>().sharedMaterial;
            TetherPiece(stake, TetherKnot + Vector3.up * .12f, .15f, new Color(.31f, .17f, .085f), material);
            Vector3 collar = transform.position - transform.forward * .04f + Vector3.up * .39f;
            Vector3 sag = Vector3.Lerp(collar, TetherKnot, .65f) - Vector3.up * .15f;
            var rope = new Color(.75f, .47f, .21f);
            TetherPiece(collar, sag, .065f, rope, material);
            TetherPiece(sag, TetherKnot, .065f, rope, material);
            TetherPiece(TetherKnot - Vector3.up * .055f, TetherKnot + Vector3.up * .056f, .22f, rope, material);
        }

        void TetherPiece(Vector3 from, Vector3 to, float diameter, Color shade, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.transform.SetParent(leash.transform, true);
            piece.transform.SetPositionAndRotation((from + to) * .5f, Quaternion.FromToRotation(Vector3.up, to - from));
            piece.transform.localScale = new Vector3(diameter, (to - from).magnitude * .5f, diameter);
            piece.GetComponent<Collider>().enabled = false;
            var renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var color = new MaterialPropertyBlock();
            color.SetColor("_BaseColor", shade);
            renderer.SetPropertyBlock(color);
        }

        public void Befriend(Vector3 visitor)
        {
            if (leash) leash.SetActive(false);
            bondDirection = Vector3.ProjectOnPlane(visitor - transform.position, Vector3.up);
            bondedAt = Time.time;
        }

        public void Step(Vector2 input, bool sprint, bool controlled, bool dragging)
        {
            if (IsStopped) input = Vector2.zero;
            Vector3 direction = new(input.x, 0f, input.y);
            float pace = dragging ? 3.9f : sprint ? 7.6f : 5.25f;
            float response = controlled ? direction.sqrMagnitude > .01f ? 27f : 32f : 5.8f;
            velocity = Vector3.MoveTowards(velocity, direction * pace, response * Time.deltaTime);
            if (velocity.sqrMagnitude > .04f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(velocity), 840f * Time.deltaTime);
            float bond = (Time.time - bondedAt) / 1.36f;
            if (bond < 1f && bondDirection.sqrMagnitude > .02f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(bondDirection), 425f * Time.deltaTime);

            vertical = motor.isGrounded ? -2f : vertical - 19f * Time.deltaTime;
            motor.Move((velocity + Vector3.up * vertical) * Time.deltaTime);
            float actual = Vector3.ProjectOnPlane(motor.velocity, Vector3.up).magnitude;
            animator.SetFloat(Speed, Mathf.Min(2f, actual / 3.8f), .095f, Time.deltaTime);
            float bank = Vector3.Dot(direction, transform.right) * -11f * Mathf.Clamp01(actual / 5f);
            float bow = Mathf.Sin(Mathf.PI * Mathf.Clamp01(bond));
            model.localRotation = Quaternion.Slerp(model.localRotation,
                Quaternion.Euler(13f * bow, 0f, bank + 6f * bow * Mathf.Sin(bond < 1f ? bond * 19f : 0f)),
                1f - Mathf.Exp(-13f * Time.deltaTime));
        }

        public bool Bark()
        {
            if (Time.time < barkAt || IsStopped) return false;
            barkAt = Time.time + 1.65f;
            return true;
        }

        public void Hesitate()
        {
            if (IsStopped) return;
            heldUntil = Time.time + .75f;
            velocity *= .18f;
        }
    }
}
