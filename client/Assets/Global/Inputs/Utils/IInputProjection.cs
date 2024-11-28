using UnityEngine;

namespace Global.Inputs
{
    public interface IInputProjection
    {
        float GetAngleFrom(Vector3 from);
        Vector3 GetDirectionFrom(Vector3 from);
    }
}