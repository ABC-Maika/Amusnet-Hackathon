using UnityEngine;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EscapeDog : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform model;

        const float BarkCooldown = 1.8f;
        static readonly int Speed = Animator.StringToHash("Speed");
        CharacterController motor;
        Vector3 velocity;
        Vector3 bondDirection, gripDirection;
        GameObject leash;
        float vertical;
        float barkAt;
        float bondedAt = float.NegativeInfinity;
        float recoverAt;
        float safeUntil;
        bool exhausted;

        public Vector3 Velocity => velocity;
        public float BarkCharge => Mathf.Clamp01(1f - (barkAt - Time.time) / BarkCooldown);
        public float Stamina { get; private set; } = 1f;
        public bool IsCaptured { get; private set; }
        public bool CanBeCaught => !IsCaptured && Time.time >= safeUntil;
        public Vector3 TetherKnot { get; private set; }
        public Vector3 Nose => transform.position + transform.forward * .61f + Vector3.up * .52f;
        public Vector3 Collar => transform.position + transform.forward * .34f + Vector3.up * .54f;

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
            if (IsCaptured)
            {
                float struggle = Time.time * 15f;
                model.localRotation = Quaternion.Euler(11f + 5f * Mathf.Sin(struggle), 0f, 14f * Mathf.Sin(struggle * .8f));
                animator.SetFloat(Speed, .22f, .09f, Time.deltaTime);
                return;
            }
            Vector3 direction = new(input.x, 0f, input.y);
            bool moving = direction.sqrMagnitude > .01f;
            if (!sprint && Stamina > .18f) exhausted = false;
            bool sprinting = sprint && moving && !dragging && !exhausted;
            if (sprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - .31f * Time.deltaTime);
                exhausted = Stamina <= 0f;
                recoverAt = Time.time + .6f;
            }
            else if (Time.time >= recoverAt)
                Stamina = Mathf.Min(1f, Stamina + (moving ? .23f : .4f) * Time.deltaTime);
            float pace = dragging ? 3.55f : sprinting ? 7.45f : 4.8f;
            float response = controlled ? moving ? 27f : 32f : 21f;
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
            if (Time.time < barkAt || IsCaptured) return false;
            barkAt = Time.time + BarkCooldown;
            return true;
        }

        public void Grab(Vector3 keeper)
        {
            IsCaptured = true;
            velocity = Vector3.zero;
            Stamina = Mathf.Max(0f, Stamina - .32f);
            Vector3 toKeeper = Vector3.ProjectOnPlane(keeper - transform.position, Vector3.up);
            Vector3 sideways = Vector3.Cross(Vector3.up, toKeeper.normalized);
            if (Vector3.Dot(transform.forward, sideways) < 0f) sideways = -sideways;
            gripDirection = sideways.sqrMagnitude > .01f ? sideways : transform.forward;
        }

        public void PullToward(Vector3 keeper)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - keeper, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(gripDirection), 395f * Time.deltaTime);
            if (away.magnitude <= .60f) return;
            Vector3 pull = Vector3.ClampMagnitude(-away.normalized * (away.magnitude - .60f), 2.6f * Time.deltaTime);
            motor.Move(pull + Vector3.down * (2f * Time.deltaTime));
        }

        public void BreakLoose()
        {
            IsCaptured = false;
            safeUntil = Time.time + 1.75f;
            Stamina = Mathf.Max(.38f, Stamina);
            exhausted = false;
        }
    }
}
