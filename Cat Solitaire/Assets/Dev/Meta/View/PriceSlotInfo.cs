using UnityEngine;

public class PriceSlotInfo : BaseSlotInfo
{
    public override void SetIcon(Sprite icon)
    {
        _icon.sprite = icon;
    }

    public override void SetInfoText(int value)
    {
        _infoText.text = $"{value}";
    }

    public override void SetInfoText(string infoText)
    {
        _infoText.text = infoText;
    }

    public override void SetInfoTextColor(Color color)
    {
    }

    public override void SetIconColor(Color color)
    {
    }
}
