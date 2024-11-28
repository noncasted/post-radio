using System.Threading;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.Backend
{
    public interface IBackendClient
    {
        UniTask<T> Get<T>(IGetRequest request, IReadOnlyLifetime lifetime);
        UniTask<string> GetRaw(IGetRequest request, IReadOnlyLifetime lifetime);
        UniTask<T> Post<T>(IPostRequest request, IReadOnlyLifetime lifetime);
        UniTask<AudioClip> GetAudio(IGetRequest request, AudioType audioType, IReadOnlyLifetime lifetime);
        UniTask<Texture2D> GetImage(IGetRequest request, IReadOnlyLifetime lifetime);
    }
}