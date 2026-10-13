using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Serialized bindings for the editable port header prefabs.</summary>
    [DisallowMultipleComponent]
    public sealed class PortHeaderView : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private Text time, port, gold, title;
        [SerializeField] private Button settings, back, previous, next;
        [SerializeField] private Text previousLabel, nextLabel;

        public Image Frame => frame;
        public Text Time => time;
        public Text Port => port;
        public Text Gold => gold;
        public Text Title => title;
        public Button Settings => settings;
        public Button Back => back;
        public Button Previous => previous;
        public Button Next => next;
        public Text PreviousLabel => previousLabel;
        public Text NextLabel => nextLabel;
        public bool IsComplete(bool hub) => frame != null && time != null && port != null && gold != null &&
            settings != null && (hub || (title != null && back != null && previous != null && next != null &&
                previousLabel != null && nextLabel != null));

        // Used only when initially exporting a legacy header, never during a refresh.
        public void CaptureBindings(bool hub)
        {
            string prefix = hub ? "Port Hub " : "Port Facility ";
            frame = transform.Find(prefix + "Frame").GetComponent<Image>();
            time = transform.Find(prefix + "Time").GetComponent<Text>();
            port = transform.Find(prefix + "Port").GetComponent<Text>();
            gold = transform.Find(prefix + "Gold").GetComponent<Text>();
            settings = transform.Find(prefix + "Settings").GetComponent<Button>();
            if (hub) return;
            title = transform.Find("Port Facility Title").GetComponent<Text>();
            back = transform.Find("Return To Port Hub").GetComponent<Button>();
            previous = transform.Find("Previous Port Facility").GetComponent<Button>();
            next = transform.Find("Next Port Facility").GetComponent<Button>();
            previousLabel = previous.GetComponentInChildren<Text>();
            nextLabel = next.GetComponentInChildren<Text>();
        }
    }
}
