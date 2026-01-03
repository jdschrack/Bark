using Bark.Tools;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bark.GUI
{
    public class BarkSlider : MonoBehaviour
    {
        Transform knob, sliderStart, sliderEnd;
        Knob _knob;
        Text label;
        object[] values;
        int selectedValue = 0;
        private object _selected;
        public Action<object> OnValueChanged;
        public object Selected
        {
            get
            {
                return _selected;
            }
            set
            {
                _selected = value;
                OnValueChanged?.Invoke(value);
            }
        }


        void Awake()
        {
            try
            {
                sliderStart = transform.Find("Start");
                sliderEnd = transform.Find("End");
                knob = transform.Find("Knob");
                label = GetComponentInChildren<Text>();
                _knob = this.knob.gameObject.AddComponent<Knob>();
                _knob.Initialize(sliderStart, sliderEnd);
                _knob.OnValueChanged += (value) =>
                {
                    selectedValue = value;
                    Selected = values[selectedValue];
                    label.text = Selected.ToString();
                };
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        public void InitializeValues(object[] values, int initialValue)
        {
            try
            {
                this.values = values;
                selectedValue = initialValue;
                Selected = values[initialValue];
                label.text = Selected.ToString();
                _knob.divisions = values.Length - 1;
                _knob.Value = initialValue;
            }
            catch (Exception e) { Logging.Exception(e); }
        }
    }
}
