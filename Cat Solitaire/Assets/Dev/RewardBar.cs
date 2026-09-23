using System;
using UnityEngine;
using UnityEngine.UI;

public class RewardBar : MonoBehaviour
{
    [SerializeField] private Slider _rewardSlider;
    [SerializeField] private int _minValue,_maxValue;

    private void Start()
    {
        _rewardSlider.value = _minValue;
        _rewardSlider.maxValue = _maxValue;
    }

    public void SetRewardMudifier(int value)
    {
        int currentValue = (int)_rewardSlider.value + value;
        if(currentValue<= _minValue) currentValue = _minValue;
        if (currentValue >= _maxValue) currentValue = _maxValue;
        _rewardSlider.value = currentValue;
    }
}
