using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class TimberDomino : ChainMechanism
    {
        [SerializeField] private Transform timber;
        [SerializeField] private Interactable[] receivers;
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Fall(manager));
        private IEnumerator Fall(MessManager manager)
        {
            bool struck = false;
            for(float t=0f; ; t+=Time.deltaTime)
            {
                float u=Mathf.Clamp01(t/.53f);
                float tilt= u<.83f ? 87f*(u/.83f)*(u/.83f) : 87f-5f*Mathf.Sin((u-.83f)/.17f*Mathf.PI);
                timber.localRotation=Quaternion.Euler(tilt,0,0);
                if(!struck && tilt>=47f){
                    struck=true;
                    foreach(var next in receivers) next.TryInteract(manager,this);
                }
                if(u>=1f)break;
                yield return null;
            }
        }
    }
}
