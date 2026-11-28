using UnityEngine;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(Camera))]
    public sealed class EscapeCamera : MonoBehaviour
    {
        [SerializeField] MarketEscapeRun run;
        Camera output;
        Vector3 follow, ease;
        float widen = .92f;

        void Start()
        {
            output = GetComponent<Camera>();
            follow = run.ActorPosition;
            Frame();
        }

        void LateUpdate()
        {
            follow = Vector3.SmoothDamp(follow, run.ActorPosition + run.LookAhead, ref ease,
                run.IsButterfly ? .24f : .17f, 20f, Time.unscaledDeltaTime);
            Frame();
        }

        void Frame()
        {
            Vector3 size = new(0f, 6.6f, -7.6f);
            bool inYard = run.Current is MarketEscapeRun.Phase.Farm or MarketEscapeRun.Phase.Won or MarketEscapeRun.Phase.Lost;
            float target = inYard ? 1.13f : .92f;
            widen = Mathf.Lerp(widen, target, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            Vector3 anchor = new(follow.x, Mathf.Clamp(follow.y * .44f, .05f, 1.9f), follow.z);
            transform.position = anchor + size * widen;
            transform.LookAt(anchor + Vector3.forward * .75f);
            output.fieldOfView = Mathf.Lerp(output.fieldOfView, run.IsButterfly ? 48f : 50f + run.DogSpeed * .45f,
                1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
        }
    }
}
