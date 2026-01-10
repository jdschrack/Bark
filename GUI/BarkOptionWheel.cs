using Bark.Extensions;
using Bark.Tools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bark.GUI
{
    public class BarkOptionWheel : MonoBehaviour
    {
        Transform cylinder;
        Text[] labels;
        int selectedValue = 0, selectedLabel = 0;
        ButtonController upButton, downButton;
        List<string> values;
        public Action<string> OnValueChanged;
        private string _selected;
        public string Selected
        {
            get
            {
                return _selected;
            }
            private set
            {
                _selected = value;
                OnValueChanged?.Invoke(value);
            }
        }

        void Awake()
        {
            try
            {
                cylinder = transform.Find("Cylinder");
                labels = cylinder.GetComponentsInChildren<Text>();
                upButton = transform.Find("Arrow Up").gameObject.AddComponent<ButtonController>();
                downButton = transform.Find("Arrow Down").gameObject.AddComponent<ButtonController>();
                upButton.buttonPushDistance = .01f;
                downButton.buttonPushDistance = .01f;

                upButton.OnPressed += (button, pressed) =>
                {
                    Cycle(-1);
                    button.IsPressed = false;
                };

                downButton.OnPressed += (button, pressed) =>
                {
                    Cycle(1);
                    button.IsPressed = false;
                };
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        void Cycle(int direction)
        {
            try
            {
                selectedValue = MathExtensions.Wrap(selectedValue + direction, 0, values.Count);
                selectedLabel = MathExtensions.Wrap(selectedLabel + direction, 0, labels.Length);
                Selected = values[selectedValue];
                int labelToUpdate = MathExtensions.Wrap(selectedLabel + (2 * direction), 0, labels.Length);
                string newLabel = values[MathExtensions.Wrap(selectedValue + (2 * direction), 0, values.Count)];
                labels[labelToUpdate].text = newLabel;
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        public void InitializeValues(List<string> values)
        {
            try
            {
                this.selectedLabel = 0;
                this.selectedValue = 0;
                this.values = values;
                Selected = values[selectedValue];
                for (int i = 0; i < labels.Length; i++)
                {
                    int value;
                    if (i < labels.Length / 2)
                        value = MathExtensions.Wrap(selectedValue + i, 0, values.Count);
                    else
                        value = MathExtensions.Wrap(values.Count - labels.Length + i, 0, values.Count);

                    int label = MathExtensions.Wrap(selectedLabel + i, 0, labels.Length);
                    labels[label].text = values[value];
                }
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        void FixedUpdate()
        {
            try
            {
                float angle = (selectedLabel % 6) * 60f;
                cylinder.localRotation = Quaternion.Slerp(
                    cylinder.localRotation,
                    Quaternion.Euler(angle, 0, 0),
                    Time.fixedDeltaTime * 10f
                );
            }
            catch (Exception e) { Logging.Exception(e); }
        }
    }
}
