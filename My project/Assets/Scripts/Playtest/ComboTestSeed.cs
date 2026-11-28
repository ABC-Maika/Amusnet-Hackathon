using Amusnet.Mess;
using UnityEngine;

namespace Amusnet.Playtest
{
    // Cloning this component clones the whole district and remaps every link to its fresh props.
    public sealed class ComboTestSeed : MonoBehaviour
    {
        public string title;
        [TextArea] public string instruction;
        public Transform arrival;
        public Interactable[] expected;
        public Interactable[] visible;

        public void Prepare()
        {
            foreach (var prop in GetComponentsInChildren<Interactable>(true))
                prop.gameObject.SetActive(System.Array.IndexOf(visible, prop) >= 0);
        }
    }
}
