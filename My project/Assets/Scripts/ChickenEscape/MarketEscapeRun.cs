using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Amusnet.ChickenEscape
{
    public sealed class MarketEscapeRun : MonoBehaviour
    {
        public enum Phase { FirstHen, OpenPen, BackToDog, Chase, Won, Lost }

        [SerializeField] EscapeDog dog;
        [SerializeField] EscapeButterfly butterfly;
        [SerializeField] EscapeHen[] flock;
        [SerializeField] EscapeGate gate;
        [SerializeField] EscapeKeeper keeper;
        [SerializeField] BoxCollider orchardExit, laneExit;
        [SerializeField] Transform laundry;
        [SerializeField] NavMeshObstacle laundryObstacle;
        [SerializeField] ParticleSystem barkWave, rescueBurst;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip barkSound, latchSound, rescueSound, switchSound, alertSound;
        [SerializeField, Min(20f)] float chaseDuration = 90f;

        public Phase Current { get; private set; }
        public bool IsButterfly { get; private set; }
        public bool IsPaused { get; private set; }
        public int Saved { get; private set; }
        public int Total => flock.Length;
        public int OrchardSaved { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, chaseDuration - Elapsed);
        public float DogSpeed => dog.Velocity.magnitude;
        public float BarkCharge => dog.BarkCharge;
        public Vector3 ActorPosition => IsButterfly ? butterfly.transform.position : dog.transform.position;
        public Vector3 LookAhead => !IsButterfly ? dog.Velocity * .17f : Vector3.forward * .38f;
        public bool CanUseLatch => IsButterfly && gate.CanUnlatch(ActorPosition);
        public bool CanGrabLaundry => dragging || (laundry.position - dog.transform.position).sqrMagnitude < 1.85f * 1.85f;
        public bool Dragging => dragging;
        public bool GateOpen => gate.IsOpen;
        public bool GateNeedsOpening => !gate.IsOpen && hensInPen;
        public bool KeeperClosing => keeper.IsClosing;
        public bool DogStopped => dog.IsStopped;

        bool dragging, laundryMoved, hensInPen = true;
        bool isLoading;
        float lastRescueAt = -1f;

        void Start() => gate.SetOpen(false);

        void Update()
        {
            var keys = Keyboard.current;
            if (keys?.rKey.wasPressedThisFrame == true)
            {
                Restart();
                return;
            }
            if (keys?.escapeKey.wasPressedThisFrame == true && Current is not (Phase.Won or Phase.Lost))
                Pause(!IsPaused);
            if (IsPaused || isLoading || Current is Phase.Won or Phase.Lost) return;

            if (Current != Phase.FirstHen && keys?.fKey.wasPressedThisFrame == true) Switch();
            Vector2 move = keys == null ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(
                (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0)), 1f);

            if (!IsButterfly)
            {
                if (Current == Phase.Chase && keys?.qKey.wasPressedThisFrame == true && CanGrabLaundry)
                    SetDragging(!dragging);
                if (Current == Phase.Chase && keys?.spaceKey.wasPressedThisFrame == true && dog.Bark()) Bark();
            }
            else if (keys?.eKey.wasPressedThisFrame == true && CanUseLatch)
            {
                butterfly.GustAt(gate.Latch);
                gate.SetOpen(true);
                audioSource.PlayOneShot(latchSound, .68f);
                if (Current == Phase.OpenPen) Current = Phase.BackToDog;
            }

            dog.Step(IsButterfly ? Vector2.zero : move, keys?.leftShiftKey.isPressed == true, !IsButterfly, dragging);
            float lift = IsButterfly && keys != null ? (keys.spaceKey.isPressed ? 1f : 0f) - (keys.leftShiftKey.isPressed ? 1f : 0f) : 0f;
            butterfly.Step(IsButterfly ? move : Vector2.zero, lift, IsButterfly);
            if (dragging) DragLaundry();

            bool stillInPen = false;
            bool passageBlocked = gate.InPassage(dog.transform.position, .23f);
            foreach (var hen in flock)
            {
                if (hen.Saved) continue;
                hen.Step(dog, flock, HenRoute(hen.transform.position));
                var where = hen.transform.position + Vector3.up * .12f;
                bool orchard = orchardExit.bounds.Contains(where);
                if (orchard || laneExit.bounds.Contains(where))
                {
                    Rescue(hen, orchard);
                    if (Current == Phase.Won) return;
                }
                else
                {
                    passageBlocked |= gate.InPassage(where, .08f);
                    stillInPen |= Mathf.Abs(where.x) < 3.51f && where.z > 2.47f;
                }
            }

            if (Current == Phase.Chase)
            {
                Elapsed += Time.deltaTime;
                if (Elapsed >= chaseDuration) End(Phase.Lost);
            }
            hensInPen = stillInPen;
            keeper.Step(dog, gate, Current == Phase.Chase, stillInPen, passageBlocked, !IsButterfly);
        }

        void Switch()
        {
            IsButterfly = !IsButterfly;
            audioSource.PlayOneShot(switchSound, .58f);
            if (IsButterfly)
            {
                if (dragging) SetDragging(false);
                butterfly.Board();
            }
            else if (Current == Phase.BackToDog)
            {
                Current = Phase.Chase;
                keeper.StartChase();
                audioSource.PlayOneShot(alertSound, .64f);
            }
        }

        void Bark()
        {
            keeper.Distract(dog.transform.position);
            foreach (var hen in flock)
                hen.Startle(dog.transform.position, dog.transform.forward, HenRoute(hen.transform.position));
            barkWave.transform.SetPositionAndRotation(dog.transform.position + Vector3.up * .28f,
                Quaternion.LookRotation(dog.transform.forward + Vector3.up * .19f));
            barkWave.Emit(22);
            audioSource.PlayOneShot(barkSound, .88f);
        }

        Vector3? HenRoute(Vector3 position)
        {
            if (Mathf.Abs(position.x) < 3.51f && position.z > 2.24f)
            {
                if (!gate.IsOpen) return null;
                if (Mathf.Abs(position.x) < 1.04f) return gate.Approach;
                return gate.transform.position + Vector3.forward * (position.z > 3f ? 1.05f : .52f);
            }

            Vector3 orchard = orchardExit.bounds.center;
            Vector3 lane = laneExit.bounds.center;
            orchard.y = lane.y = position.y;
            float orchardDistance = (orchard - position).sqrMagnitude;
            float laneDistance = (lane - position).sqrMagnitude;
            if (orchardDistance < 19f && orchardDistance < laneDistance) return orchard;
            return laneDistance < 19f ? lane : null;
        }

        void SetDragging(bool state)
        {
            dragging = state;
            if (state) laundryMoved = true;
            laundryObstacle.enabled = laundryMoved && !state;
        }

        void DragLaundry()
        {
            var to = dog.transform.position - dog.transform.forward * .96f;
            to.y = .045f;
            laundry.position = Vector3.MoveTowards(laundry.position, to, 9f * Time.deltaTime);
            laundry.rotation = Quaternion.RotateTowards(laundry.rotation, dog.transform.rotation, 630f * Time.deltaTime);
        }

        void Rescue(EscapeHen hen, bool orchard)
        {
            Vector3 point = hen.transform.position;
            hen.Rescue(orchard ? Vector3.left : Vector3.back);
            Saved++;
            if (orchard) OrchardSaved++;
            rescueBurst.transform.position = point + Vector3.up * .2f;
            rescueBurst.Emit(27);
            audioSource.PlayOneShot(rescueSound, .76f);
            lastRescueAt = Time.time;
            if (Saved >= Total) End(Phase.Won);
            else if (Current == Phase.FirstHen) Current = Phase.OpenPen;
        }

        public bool JustRescued => Time.time - lastRescueAt is >= 0f and < .8f && Saved > 0;

        public void Pause(bool state)
        {
            IsPaused = state;
            Time.timeScale = state ? 0f : 1f;
        }

        void End(Phase finish)
        {
            Current = finish;
            Time.timeScale = 0f;
        }

        void OnDestroy() => Time.timeScale = 1f;

        public void Restart() => Load(gameObject.scene.name);

        public void QuitToWorkspace() => Load("MainMenuWorkspace");

        void Load(string scene)
        {
            if (isLoading) return;
            isLoading = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        public Vector3 PrimaryMarker()
        {
            if (Current == Phase.FirstHen) return flock[0].transform.position + Vector3.up * .65f;
            if (Current == Phase.OpenPen)
            {
                if (!IsButterfly) return butterfly.transform.position + Vector3.up * .38f;
                return (ActorPosition.z > 2.24f ? gate.Latch : gate.Access) + Vector3.up * .28f;
            }
            if (Current == Phase.BackToDog) return dog.transform.position + Vector3.up;
            return GateNeedsOpening ? gate.Latch + Vector3.up * .22f : Vector3.zero;
        }

        public Vector3 OrchardMarker => orchardExit.bounds.center + Vector3.up * 1.25f;
        public Vector3 LaneMarker => laneExit.bounds.center + Vector3.up * 1.25f;
    }
}
