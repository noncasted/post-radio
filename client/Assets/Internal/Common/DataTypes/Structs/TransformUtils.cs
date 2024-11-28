using UnityEngine;

namespace Internal
{
    public static class TransformUtils
    {
        public static void MoveLocalX(this Transform transform, float distance)
        {
            var position = transform.localPosition;
            position.x += distance;
            transform.localPosition = position;
        }

        public static void ScaleLocalX(this Transform transform, float distance)
        {
            var localScale = transform.localScale;
            localScale.x += distance;
            transform.localScale = localScale;
        }
    }
}