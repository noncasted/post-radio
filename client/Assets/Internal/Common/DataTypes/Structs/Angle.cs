using UnityEngine;

namespace Internal
{
    public readonly struct Angle
    {
        public Angle(float value)
        {
            Value = value;
        }

        public readonly float Value;

        public Horizontal ToHorizontal()
        {
            return Value is > 90f and < 270f ? Horizontal.Left : Horizontal.Right;
        }

        public Vertical ToVertical()
        {
            return Value is > 0f and < 180f ? Vertical.Up : Vertical.Down;
        }

        public Vector2 ToVector2()
        {
            var radians = Value * Mathf.Deg2Rad;
            var x = Mathf.Cos(radians);
            var y = Mathf.Sin(radians);
            var direction = new Vector2(x, y);

            return direction;
        }

        public Quaternion ToRotation()
        {
            return Quaternion.Euler(0f, 0f, Value);
        }

        public static Angle Zero => new(0f);
    }

    public static class AngleExtensions
    {
        public static Angle Angle(this float value)
        {
            if (value < 0)
                value += 360;

            if (value > 360)
                value %= value / 360f;

            return new Angle(value);
        }
    }
}