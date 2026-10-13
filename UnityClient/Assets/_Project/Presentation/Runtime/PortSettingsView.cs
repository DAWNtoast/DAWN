using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored settings hierarchy; preferences and callbacks belong to its controller.</summary>
    public sealed class PortSettingsView : MonoBehaviour
    {
        public CanvasGroup Sheet;
        public RawImage Parchment;
        public Slider Master, Music, Effects;
        public Text MasterValue, MusicValue, EffectsValue, Hint;
        public Toggle FullScreen;
        public Button Apply, Close, ReturnToMenu;

        public bool IsComplete => Sheet != null && Parchment != null && Master != null && Music != null &&
            Effects != null && MasterValue != null && MusicValue != null && EffectsValue != null &&
            Hint != null && FullScreen != null && Apply != null && Close != null && ReturnToMenu != null;

        public void CaptureBindings()
        {
            Transform sheet = transform.Find("Port Settings Sheet");
            Sheet = sheet.GetComponent<CanvasGroup>();
            Parchment = sheet.Find("Port Settings Parchment").GetComponent<RawImage>();
            Master = sheet.Find("Port Settings Master Slider").GetComponent<Slider>();
            Music = sheet.Find("Port Settings Music Slider").GetComponent<Slider>();
            Effects = sheet.Find("Port Settings Effects Slider").GetComponent<Slider>();
            MasterValue = sheet.Find("Port Settings Master Value").GetComponent<Text>();
            MusicValue = sheet.Find("Port Settings Music Value").GetComponent<Text>();
            EffectsValue = sheet.Find("Port Settings Effects Value").GetComponent<Text>();
            Hint = sheet.Find("Port Settings Hint").GetComponent<Text>();
            FullScreen = sheet.Find("Port Settings Full Screen Toggle").GetComponent<Toggle>();
            Apply = sheet.Find("Apply Port Settings").GetComponent<Button>();
            Close = sheet.Find("Close Port Settings").GetComponent<Button>();
            ReturnToMenu = sheet.Find("Return To Main Menu").GetComponent<Button>();
        }
    }
}
