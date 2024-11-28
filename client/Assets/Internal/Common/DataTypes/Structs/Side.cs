using UnityEngine;

namespace Internal
{
    public enum Side
    {
        Up,
        Right,
        Down,
        Left
    }

    public static class SideExtensions
    {
        public static Side ToSide(this Vector2 vector)
        {
            if (vector.x > 0)
            {
                return Side.Right;
            }

            if (vector.x < 0)
            {
                return Side.Left;
            }

            if (vector.y > 0)
            {
                return Side.Up;
            }

            return Side.Down;
        }
    }
}