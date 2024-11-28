using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Images
{
    public interface IImageView
    {
        UniTask SetImage(Sprite image, IReadOnlyLifetime lifetime);
    }
}