using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class BaseSlotInfo : MonoBehaviour
{
    [SerializeField] protected Image _icon;
    [SerializeField] protected TMP_Text _infoText;

    public abstract void SetIcon(Sprite icon);
    public abstract void SetInfoText(int value);
    public abstract void SetInfoText(string infoText);
    public abstract void SetInfoTextColor(Color color);
    public abstract void SetIconColor(Color color);
}
