using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Amusnet.ChickenEscape
{
    public sealed class MarketEscapeRun : MonoBehaviour
    {
        public enum Phase { MeetDog, Trust, Farm, Won, Lost }
        public enum Finish { Left, Lockdown, FlockAccountedFor, TooManyCaught, DogSecured }

        public const int FlockLimit = 3;
        public const int RescueTarget = 3;
        const float GustCooldown = 8.5f;
        const float CallRange = 6.25f;

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
        [SerializeField] AudioClip barkSound, latchSound, rescueSound, switchSound, alertSound, henSquawk;
        [SerializeField, Min(20f)] float chaseDuration = 48f;

        static bool skipBondOnRetry;
        bool dragging, laundryMoved, hensInPen = true, loading, dogWasCaught;
        float trustedAt, gustAt, dogGrabbedAt, capturedAt = float.NegativeInfinity;
        Vector3 decoy;
        float decoyUntil;

        public Phase Current { get; private set; }
        public Finish FinishedBy { get; private set; }
        public bool IsButterfly { get; private set; }
        public bool IsPaused { get; private set; }
        public bool AlarmActive { get; private set; }
        public int Saved { get; private set; }
        public int Caught { get; private set; }
        public int Following { get; private set; }
        public int Total => flock.Length;
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, chaseDuration - Elapsed);
        public float DogSpeed => dog.Velocity.magnitude;
        public float Stamina => dog.Stamina;
        public float BarkCharge => dog.BarkCharge;
        public float GustRemaining => Mathf.Max(0f, gustAt - Time.time);
        public Vector3 ActorPosition => IsButterfly ? butterfly.transform.position : dog.transform.position;
        bool FramingGrab => DogCaptured && Time.time < dogGrabbedAt + .62f;
        public Vector3 CameraTarget => FramingGrab ? (dog.transform.position + keeper.transform.position) * .5f : ActorPosition;
        public Vector3 LookAhead => FramingGrab ? Vector3.zero : IsButterfly ? Vector3.forward * .38f : dog.Velocity * .19f;
        public bool CanUseLatch => Current == Phase.Farm && IsButterfly && gate.CanUnlatch(ActorPosition);
        public bool CanUntieDog => Current == Phase.MeetDog && (ActorPosition - dog.TetherKnot).sqrMagnitude < 1.9f * 1.9f;
        public bool CanGrabLaundry => dragging || (laundry.position - dog.transform.position).sqrMagnitude < 1.85f * 1.85f;
        public bool Dragging => dragging;
        public bool GateOpen => gate.IsOpen;
        public bool GateNeedsOpening => !gate.IsOpen && hensInPen;
        public bool DogCaptured => dog.IsCaptured;
        public bool CanFreeDog => IsButterfly && DogCaptured &&
            (butterfly.transform.position - dog.Collar).sqrMagnitude < 2.35f * 2.35f &&
            !Physics.Linecast(butterfly.transform.position, dog.Collar,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        public bool JustCaught => Time.time - capturedAt < 1.7f;
        public bool CanLeave => !IsButterfly && Saved > 0 && TryExit(dog.transform.position, out _);
        public EscapeKeeper Keeper => keeper;
        public bool DecoyActive => Time.time < decoyUntil && keeper.IsDistracted;
        public Vector3 DecoyMarker => decoy + Vector3.up * .7f;
        public Vector3 OrchardMarker => orchardExit.bounds.center + Vector3.up * 1.25f;
        public Vector3 LaneMarker => laneExit.bounds.center + Vector3.up * 1.25f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRetry() => skipBondOnRetry = false;

        void Awake()
        {
            IsButterfly = true;
            if (skipBondOnRetry) Current = Phase.Farm;
            skipBondOnRetry = false;
        }

        void Start()
        {
            gate.SetOpen(false);
            if (Current == Phase.MeetDog) dog.Tether();
            butterfly.Board();
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys?.rKey.wasPressedThisFrame == true)
            {
                Restart();
                return;
            }
            if (keys?.escapeKey.wasPressedThisFrame == true)
            {
                if (Current is Phase.Won or Phase.Lost)
                {
                    QuitToWorkspace();
                    return;
                }
                Pause(!IsPaused);
            }
            if (IsPaused || loading || Current is Phase.Won or Phase.Lost) return;

            if (Current == Phase.Trust && Time.time >= trustedAt + 1.4f)
            {
                Current = Phase.Farm;
                butterfly.Board();
            }

            var movement = ReadMovement(keys);
            if (Current == Phase.Farm)
            {
                if (!DogCaptured && keys?.fKey.wasPressedThisFrame == true) Switch();
                if (keys?.eKey.wasPressedThisFrame == true)
                {
                    if (IsButterfly) UseGust();
                    else if (CanLeave)
                    {
                        End(Finish.Left);
                        return;
                    }
                }
                if (!IsButterfly)
                {
                    if (keys?.qKey.wasPressedThisFrame == true && CanGrabLaundry) SetDragging(!dragging);
                    if (keys?.spaceKey.wasPressedThisFrame == true && dog.Bark()) Bark();
                }
            }
            else if (keys?.eKey.wasPressedThisFrame == true && CanUntieDog) UntieDog();

            dog.Step(IsButterfly ? Vector2.zero : movement, keys?.leftShiftKey.isPressed == true, !IsButterfly, dragging);
            if (Current == Phase.Trust) butterfly.PerchAt(dog.Nose);
            else
            {
                float lift = IsButterfly && keys != null ? (keys.spaceKey.isPressed ? 1f : 0f) - (keys.leftShiftKey.isPressed ? 1f : 0f) : 0f;
                butterfly.Step(IsButterfly ? movement : Vector2.zero, lift, IsButterfly);
            }
            if (dragging) DragLaundry();

            MoveFlock();
            if (Current != Phase.Farm) return;

            if (keeper.DogSecured)
            {
                End(Finish.DogSecured);
                return;
            }
            if (AlarmActive)
            {
                Elapsed = Mathf.Min(chaseDuration, Elapsed + Time.deltaTime);
                if (Elapsed >= chaseDuration)
                {
                    End(DogCaptured ? Finish.DogSecured : Finish.Lockdown);
                    return;
                }
            }
            if (!DogCaptured && !keeper.IsHandlingHen)
            {
                if (Saved + Caught == Total) End(Finish.FlockAccountedFor);
                else if (Total - Caught < RescueTarget) End(Finish.TooManyCaught);
            }
        }

        static Vector2 ReadMovement(Keyboard keys)
        {
            if (keys == null) return Vector2.zero;
            var input = new Vector2(
                (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0));
            return Vector2.ClampMagnitude(input, 1f);
        }

        void MoveFlock()
        {
            Vector3 lead = dog.transform.position;
            bool leadingOut = TryExit(lead, out bool dogInOrchard);
            Vector3 followGoal = leadingOut ? (dogInOrchard ? orchardExit.bounds.center : laneExit.bounds.center) : lead;
            followGoal.y = lead.y;

            hensInPen = false;
            Following = 0;
            bool passageBlocked = gate.InPassage(lead, .28f);
            foreach (var hen in flock)
            {
                if (hen.Saved || hen.InHands) continue;
                bool pathOpen = gate.IsOpen || InPen(hen.transform.position) == InPen(lead);
                hen.Step(followGoal, flock, pathOpen, leadingOut);
                Vector3 position = hen.transform.position;
                passageBlocked |= gate.InPassage(position, .13f);
                if (hen.Caught) continue;
                if (TryExit(position, out bool orchard)) Rescue(hen, orchard);
                else
                {
                    hensInPen |= InPen(position);
                    if (hen.Following) Following++;
                }
            }

            var taken = keeper.Step(dog, gate, flock, Current == Phase.Farm, hensInPen, passageBlocked);
            if (keeper.HasSpottedSomeone) RaiseAlarm();
            if (DogCaptured && !dogWasCaught)
            {
                dogGrabbedAt = Time.time;
                if (!IsButterfly) Switch();
                foreach (var hen in flock) hen.ScatterFrom(dog.transform.position);
                Following = 0;
                audioSource.PlayOneShot(barkSound, .82f);
            }
            dogWasCaught = DogCaptured;
            if (!taken) return;

            Following--;
            Caught++;
            capturedAt = Time.time;
            if (henSquawk) audioSource.PlayOneShot(henSquawk, .8f);
        }

        void UntieDog()
        {
            butterfly.GustAt(dog.TetherKnot);
            dog.Befriend(butterfly.transform.position);
            audioSource.PlayOneShot(rescueSound, .52f);
            trustedAt = Time.time;
            Current = Phase.Trust;
        }

        void Switch()
        {
            IsButterfly = !IsButterfly;
            audioSource.PlayOneShot(switchSound, .58f);
            if (!IsButterfly) return;
            if (dragging) SetDragging(false);
            butterfly.Board();
        }

        void UseGust()
        {
            if (CanFreeDog)
            {
                butterfly.GustAt(dog.Collar);
                keeper.ReleaseDog();
                audioSource.PlayOneShot(latchSound, .73f);
            }
            else if (CanUseLatch)
            {
                butterfly.GustAt(gate.Latch);
                gate.SetOpen(true);
                audioSource.PlayOneShot(latchSound, .68f);
                RaiseAlarm();
            }
            else if (GustRemaining <= 0f)
            {
                Vector3 point = butterfly.transform.position;
                point.y = .04f;
                if (!keeper.TryDistract(point)) return;
                butterfly.GustAt(point);
                decoy = point;
                decoyUntil = Time.time + 5.3f;
                gustAt = Time.time + GustCooldown;
                audioSource.PlayOneShot(switchSound, .83f);
            }
        }

        void Bark()
        {
            Vector3 origin = dog.transform.position;
            if (keeper.HearBark(origin)) RaiseAlarm();
            for (int i = Following; i < FlockLimit; i++)
            {
                EscapeHen closest = null;
                float best = CallRange * CallRange;
                foreach (var hen in flock)
                {
                    if (hen.Saved || hen.Caught || hen.Following) continue;
                    float distance = (hen.transform.position - origin).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    closest = hen;
                }
                if (!closest) break;
                closest.HearBark();
            }
            barkWave.transform.SetPositionAndRotation(origin + Vector3.up * .28f,
                Quaternion.LookRotation(dog.transform.forward + Vector3.up * .19f));
            barkWave.Emit(22);
            audioSource.PlayOneShot(barkSound, .88f);
        }

        void RaiseAlarm()
        {
            if (AlarmActive) return;
            AlarmActive = true;
            audioSource.PlayOneShot(alertSound, .67f);
        }

        public static bool InPen(Vector3 position) =>
            Mathf.Abs(position.x) < 3.51f && position.z > 2.15f && position.z < 7.5f;

        bool TryExit(Vector3 position, out bool orchard)
        {
            Vector3 point = position + Vector3.up * .12f;
            orchard = orchardExit.bounds.Contains(point);
            return orchard || laneExit.bounds.Contains(point);
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
            rescueBurst.transform.position = point + Vector3.up * .2f;
            rescueBurst.Emit(27);
            audioSource.PlayOneShot(rescueSound, .76f);
        }

        public void Pause(bool state)
        {
            IsPaused = state;
            Time.timeScale = state ? 0f : 1f;
        }

        void End(Finish reason)
        {
            FinishedBy = reason;
            Current = reason != Finish.DogSecured && Saved >= RescueTarget ? Phase.Won : Phase.Lost;
            Time.timeScale = 0f;
        }

        void OnDestroy() => Time.timeScale = 1f;

        public void Restart()
        {
            skipBondOnRetry = Current is Phase.Farm or Phase.Won or Phase.Lost;
            Load(gameObject.scene.name);
        }

        public void QuitToWorkspace()
        {
            skipBondOnRetry = false;
            Load("MainMenuWorkspace");
        }

        void Load(string scene)
        {
            if (loading) return;
            loading = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        public Vector3 PrimaryMarker()
        {
            if (Current == Phase.MeetDog) return dog.TetherKnot + Vector3.up * .45f;
            if (DogCaptured) return dog.Collar + Vector3.up * .52f;
            if (Current != Phase.Farm || !GateNeedsOpening || !IsButterfly) return Vector3.zero;
            return (ActorPosition.z > 2.24f ? gate.Latch : gate.Access) + Vector3.up * .28f;
        }
    }
}
