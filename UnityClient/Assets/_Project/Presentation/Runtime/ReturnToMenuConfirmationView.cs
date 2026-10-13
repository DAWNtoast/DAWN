using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed class ReturnToMenuConfirmationView : MonoBehaviour
    {
        public Text Message;
        public Button Cancel, Confirm;
        public bool IsComplete => Message != null && Cancel != null && Confirm != null;

        public void CaptureBindings()
        {
            Transform sheet = transform.Find("Return To Main Menu Sheet");
            Message = sheet.Find("Return To Main Menu Message").GetComponent<Text>();
            Cancel = sheet.Find("Cancel Return To Main Menu").GetComponent<Button>();
            Confirm = sheet.Find("Confirm Return To Main Menu").GetComponent<Button>();
        }
    }
}
