using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class MarketLauncher : ChainMechanism
    {
        [SerializeField] private Transform lever, sack;
        [SerializeField] private Interactable leftTarget, rightTarget;
        private Interactable landing;
        protected override void OnInteracted(MessManager manager)
        {
            // The side the butterfly approaches selects a landing. A relay has a fixed exit.
            landing = Vector3.Dot(manager.transform.position - transform.position, transform.right) < 0
                ? leftTarget : rightTarget;
            StartCoroutine(Launch(manager));
        }
        private IEnumerator Launch(MessManager manager)
        {
            bool airborne = false;
            for (float t=0f; t<1.5f; t+=Time.deltaTime)
            {
                float angle = t < .22f ? -19f * Mathf.Sin(t / .22f * Mathf.PI * .5f) :
                    -19f + 71f * Mathf.Sin(Mathf.Min(1f,(t-.22f)/.20f) * Mathf.PI * .5f);
                if(t>.42f) angle=14f+38f*Mathf.Cos((t-.42f)*9f)*Mathf.Exp(-(t-.42f)*3.2f);
                lever.localRotation=Quaternion.Euler(0,0,angle);
                if(!airborne && t>=.34f){
                    airborne=true;
                    // Preserve the launch pose while the board recoils beneath it.
                    sack.SetParent(transform,true);
                    StartCoroutine(Carry(sack,landing,manager,1.02f,2.8f,1.0f,true));
                }
                yield return null;
            }
        }
    }
}
