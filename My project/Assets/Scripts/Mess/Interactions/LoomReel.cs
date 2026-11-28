using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class LoomReel : ChainMechanism
    {
        [SerializeField] private Transform spindle, shuttle;
        [SerializeField] private LineRenderer yarn;
        [SerializeField] private Interactable takeup;

        private void Start()
        {
            yarn.useWorldSpace = true;
            yarn.positionCount = 3;
            DrawThread(.34f);
        }
        private void DrawThread(float sag)
        {
            Vector3 a = shuttle.position, b = takeup.transform.position + Vector3.up * 1.08f;
            yarn.SetPosition(0, a);
            yarn.SetPosition(1, (a + b) * .5f - Vector3.up * sag);
            yarn.SetPosition(2, b);
        }
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Unwind(manager));
        private IEnumerator Unwind(MessManager manager)
        {
            Vector3 start = shuttle.position;
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / 1.04f);
                spindle.localRotation = Quaternion.Euler(0,0,645f * u);
                shuttle.position = Vector3.Lerp(start, takeup.transform.position + Vector3.up * 1.08f, u * u)
                    - Vector3.up * (.23f * Mathf.Sin(Mathf.PI * u));
                shuttle.Rotate(Vector3.right, Time.deltaTime * 440f, Space.Self);
                DrawThread(.34f * (1f - u));
                if (u >= 1f) break;
                yield return null;
            }
            yarn.enabled = false;
            takeup.TryInteract(manager, this);
        }
    }
}
