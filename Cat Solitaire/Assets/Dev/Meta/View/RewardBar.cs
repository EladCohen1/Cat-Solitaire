using System;
using UnityEngine;
using UnityEngine.UI;

public class RewardBar : MonoBehaviour
{
    [SerializeField] private Slider _rewardSlider;
    [SerializeField] private int _minValue,_maxValue;
    
    public event Action<int> Changed;

    public int Value => (int)_rewardSlider.value;
    
    
    public void Configure(int min, int max)
    {
        _rewardSlider.value = _minValue;
        _rewardSlider.maxValue = _maxValue;
        SetValue(_rewardSlider.value <= _minValue ? _minValue : Value);
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
