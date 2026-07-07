using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SmarcGUI.WorldSpace;


namespace SmarcGUI
{
    public class FollowDistanceSlider : MonoBehaviour   
    {
        public Slider slider;
        public TMP_Text text;
        public SmoothFollow cam;

        void Start()
        {
            slider.onValueChanged.AddListener(OnSliderValueChanged);
            OnSliderValueChanged(slider.value);
        }

        void OnSliderValueChanged(float value)
        {
            cam.distance = value;
            text.text = value.ToString("F2");
        }


    }
}