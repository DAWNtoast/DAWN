using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static void FitMaintenanceBoardText(RectTransform supply)
        {
            foreach (Text text in supply.GetComponentsInChildren<Text>(true))
            {
                if (text.text == "+" || text.text == "−") continue;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Mathf.Max(9, text.fontSize - 2);
                text.resizeTextMaxSize = text.fontSize;
            }
        }
    }
}
