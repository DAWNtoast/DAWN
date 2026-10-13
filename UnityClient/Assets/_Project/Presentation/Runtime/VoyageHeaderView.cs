using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Fixed navigation header hierarchy; current voyage data is supplied by its controller.</summary>
    [DisallowMultipleComponent]
    public sealed class VoyageHeaderView : MonoBehaviour
    {
        [SerializeField] private Image frame, compass;
        [SerializeField] private Text title, status, time;
        [SerializeField] private VoyageTimeRibbonGraphic timeline;
        [SerializeField] private Button cargo, settings;

        public Image Frame => frame;
        public Image Compass => compass;
        public Text Title => title;
        public Text Status => status;
        public Text Time => time;
        public VoyageTimeRibbonGraphic Timeline => timeline;
        public Button Cargo => cargo;
        public Button Settings => settings;
        public bool IsComplete => frame != null && compass != null && title != null && status != null && time != null &&
            timeline != null && cargo != null && settings != null;

#if UNITY_EDITOR
        public void CaptureBindings()
        {
            frame = transform.Find("Voyage Header Frame").GetComponent<Image>();
            compass = transform.Find("Voyage Header Compass").GetComponent<Image>();
            title = transform.Find("Voyage Header Title").GetComponent<Text>();
            status = transform.Find("Voyage Header Status").GetComponent<Text>();
            time = transform.Find("Lookout Time Ribbon/Lookout Time Readout").GetComponent<Text>();
            timeline = transform.Find("Lookout Time Ribbon/Lookout Time Track").GetComponent<VoyageTimeRibbonGraphic>();
            cargo = transform.Find("Voyage Cargo").GetComponent<Button>();
            settings = transform.Find("Voyage Settings").GetComponent<Button>();
            if (!IsComplete) throw new InvalidOperationException("Incomplete voyage header prefab.");
        }
#endif
    }
}
