using System.Collections;
using UnityEngine;

namespace Amusnet.Mess
{
    public sealed class OrchardHarvest : ChainMechanism
    {
        [SerializeField] private Transform bough, basket;
        [SerializeField] private Transform[] fruit;
        [SerializeField] private Interactable delivery;
        private void Update()
        {
            if(bough && !HasInteracted)bough.localRotation=Quaternion.Euler(0,0,2.1f*Mathf.Sin(Time.time*1.7f));
        }
        protected override void OnInteracted(MessManager manager) => StartCoroutine(Harvest(manager));
        private IEnumerator Harvest(MessManager manager)
        {
            bool picking=bough!=null;
            Transform pivot=picking?bough:basket;
            float t=0f;
            for(int i=0; i<fruit.Length; i++){
                float release=.26f+i*(picking?.18f:.12f);
                while(t<release){
                    float angle=picking?16f*Mathf.Sin(t*19f)*Mathf.Exp(-t*.65f):49f*Mathf.SmoothStep(0f,1f,t/.52f);
                    pivot.localRotation=Quaternion.Euler(0,0,angle);
                    t+=Time.deltaTime; yield return null;
                }
                // The last apple supplies the weight that tips the picking basket.
                StartCoroutine(FruitFlight(fruit[i],i,i==fruit.Length-1,manager));
            }
            if(picking){
                for(float u=0;u<.65f;u+=Time.deltaTime){
                    pivot.localRotation=Quaternion.Euler(0,0,9f*Mathf.Sin((u+t)*19f)*(1f-u/.65f));
                    yield return null;
                }
                pivot.localRotation=Quaternion.identity;
            }
        }
        private IEnumerator FruitFlight(Transform apple,int index,bool last,MessManager manager)
        {
            Vector3 start=apple.position;apple.SetParent(transform,true);
            Vector3 end=delivery.transform.position+Vector3.up*(bough?1.04f:1.40f)+transform.right*((index%3-1)*.11f);
            for(float t=0; ; t+=Time.deltaTime){
                float u=Mathf.Clamp01(t/(bough?.70f:.88f));
                Vector3 p=Vector3.Lerp(start,end,u);
                p.y=Mathf.Lerp(start.y,end.y,u*u)+(bough?.12f:.64f)*Mathf.Sin(u*Mathf.PI);
                apple.position=p;apple.Rotate(Vector3.right,290f*Time.deltaTime,Space.Self);
                if(u>=1f)break;
                yield return null;
            }
            if(bough)apple.gameObject.SetActive(false);
            if(last)delivery.TryInteract(manager,this);
        }
    }
}
