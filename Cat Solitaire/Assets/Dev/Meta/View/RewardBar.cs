using System;
using UnityEngine;
using UnityEngine.UI;

public class RewardBar : MonoBehaviour
{
    [SerializeField] private Slider _rewardSlider;
    [SerializeField] private int _minValue,_maxValue;
    
    public event Action<int> Changed;

    public int Value => (int)_rewardSlider.value;
    
    
    // Sized by the ladder being shown, not by the inspector: a level whose ladder has
    // four rungs would otherwise fill a bar built for three. The floor stays at 0 so
    // that one rung of three reads as a third full rather than empty.
    public void Configure(int min, int max)
    {
        _minValue = min;
        _maxValue = Mathf.Max(min, max);

        _rewardSlider.minValue = _minValue;
        _rewardSlider.maxValue = _maxValue;
        _rewardSlider.value = Mathf.Clamp(Value, _minValue, _maxValue);
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
