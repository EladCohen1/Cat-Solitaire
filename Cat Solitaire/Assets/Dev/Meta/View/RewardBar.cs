using System;
using UnityEngine;
using UnityEngine.UI;

public class RewardBar : MonoBehaviour
{
    [SerializeField] private Slider _rewardSlider;
    [SerializeField] private int _minValue,_maxValue;

    /// <summary>Raised whenever the bar moves, so the window can re-price the level.</summary>
    public event Action<int> Changed;

    public int Value => (int)_rewardSlider.value;

    private void Start()
    {
        _rewardSlider.value = _minValue;
        _rewardSlider.maxValue = _maxValue;
    }

    /// <summary>Sizes the bar to the ladder it is showing, rather than to inspector guesses.</summary>
    public void Configure(int min, int max)
    {
        _minValue = min;
        _maxValue = Mathf.Max(min, max);

        _rewardSlider.minValue = _minValue;
        _rewardSlider.maxValue = _maxValue;
        SetValue(_rewardSlider.value < _minValue ? _minValue : Value);
    }

    public void SetValue(int value)
    {
        var clamped = Mathf.Clamp(value, _minValue, _maxValue);
        if (clamped == Value) return;

        _rewardSlider.value = clamped;
        Changed?.Invoke(clamped);
    }

    public void SetRewardMudifier(int value)
    {
        int currentValue = (int)_rewardSlider.value + value;
        if(currentValue<= _minValue) currentValue = _minValue;
        if (currentValue >= _maxValue) currentValue = _maxValue;
        if (currentValue == Value) return;

        _rewardSlider.value = currentValue;
        Changed?.Invoke(currentValue);
    }
}
