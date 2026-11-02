using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardSlotInfo : BaseSlotInfo
{
    public override void SetIcon(Sprite icon)
    {
        _icon.sprite = icon;
    }

    public override void SetInfoText(int value)
    {
        _infoText.text = $"X{value}";
    }

    public override void SetInfoText(string infoText)
    {
        _infoText.text = infoText;
    }

    public override void SetInfoTextColor(Color color)
    {
        _infoText.color = color;
    }

    public override void SetIconColor(Color color)
    {
        _icon.color = color;
    }
}
