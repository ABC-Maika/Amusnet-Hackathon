using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Amusnet.ChickenEscape
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class EscapeMenuEntry : MonoBehaviour
    {
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            var play = root?.Q<Button>("playButton");
            if (play == null) return;
            var menuRow = play.parent.parent;
            var container = menuRow.parent;
            var line = new VisualElement();
            line.AddToClassList("reveal");
            line.AddToClassList("delay-3");
            var frame = new VisualElement();
            frame.AddToClassList("pbtn-wrap");
            frame.AddToClassList("pbtn-wrap--secondary");
            var button = new Button(() => SceneManager.LoadScene("ChickenEscape")) { text = "CHICKEN ESCAPE" };
            button.AddToClassList("pbtn");
            button.AddToClassList("pbtn--secondary");
            frame.Add(button);
            line.Add(frame);
            container.Insert(container.IndexOf(menuRow), line);
        }
    }
}
