using System;
using UnityEngine;

namespace Internal
{
    public static class DirectionUtils
    {
        public static Quaternion RotationTo(this Transform from, Transform to)
        {
            var direction = (Vector2)(to.position - from.position).normalized;
            return Quaternion.Euler(0f, 0f, direction.ToAngle());
        }
        
        public static float ToAngle(this Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (angle < 0)
                angle += 360f;

            return angle;
        }

        public static Horizontal ToHorizontal(this Vector2 direction)
        {
            if (Mathf.Approximately(direction.x, 0f) == true)
                return Horizontal.Right;

            if (direction.x > 0f)
                return Horizontal.Right;

            return Horizontal.Left;
        }


        public static Quaternion ToRotation(this Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (angle < 0)
                angle += 360f;

            return Quaternion.Euler(0f, 0f, angle);
        }

        public static bool IsVertical(this Vector2 direction)
        {
            if (Mathf.Approximately(direction.x, 0f) == false)
                return false;

            if (Mathf.Approximately(direction.y, 0f) == true)
                return false;

            return true;
        }

        public static bool IsZero(this Vector2 direction)
        {
            if (Mathf.Approximately(direction.x, 0f) == false)
                return false;

            if (Mathf.Approximately(direction.y, 0f) == false)
                return false;

            return true;
        }

        public static bool IsAlong(this Vector2 a, Vector2 b)
        {
            if (a.x > 0 && b.x > 0)
                return true;

            if (a.x < 0 && b.x < 0)
                return true;

            if (a.y > 0 && b.y > 0)
                return true;

            if (a.y < 0 && b.y < 0)
                return true;

            return false;
        }

        public static Horizontal ToHorizontal(this float x)
        {
            if (x < 0)
                return Horizontal.Left;

            return Horizontal.Right;
        }

        public static Vector2Int Normalized(this Vector2Int direction)
        {
            return new Vector2Int(Math.Clamp(direction.x, -1, 1), Math.Clamp(direction.y, -1, 1));
        }

        public static Vector2 RotateClamped(this Vector2 source, Vector2 target, float clampAngle)
        {
            var sourceAngle = source.ToAngle();
            var targetAngle = target.ToAngle();

            var delta = targetAngle - sourceAngle;
            delta = Mathf.Clamp(delta, -clampAngle, clampAngle);

            return (sourceAngle + delta).Angle().ToVector2();
        }
    }
}