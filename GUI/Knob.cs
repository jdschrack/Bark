using Bark.Gestures;
using Bark.Interaction;
using System;
using UnityEngine;

namespace Bark.GUI
{
    public class Knob : BarkInteractable
    {
        public Action<int> OnValueChanged;
        Transform start, end;
        public int divisions;
        private int _value;

        public int Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (value != _value)
                {
                    OnValueChanged?.Invoke(value);
                    if (Selected)
                        GestureTracker.Instance.HapticPulse(this.selectors[0].IsLeft);
                    Sounds.Play(Sounds.Sound.keyboardclick);
                }
                _value = value;
                this.transform.position = Vector3.Lerp(start.position, end.position, (float)Value / divisions);
            }
        }

        public void Initialize(Transform start, Transform end)
        {
            this.priority = MenuController.Instance.priority;
            this.start = start;
            this.end = end;
        }

        void FixedUpdate()
        {
            if (!Selected) return;

            // Get the length of the projection of the start-to-hand vector onto the start-to-end vector
            Vector3 startToHand = selectors[0].transform.position - start.position;
            Vector3 startToEnd = end.position - start.position;
            float projLength = Vector3.Dot(startToEnd, startToHand) / startToEnd.magnitude;

            // Get the ratio of the projection to the length of the start-to-end vector
            projLength = Mathf.Clamp01(projLength / startToEnd.magnitude);
            // Get the index of the division that the hand is closest to
            Value = Mathf.RoundToInt(projLength * divisions);

        }
    }
}
