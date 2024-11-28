using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Global.Backend
{
    public class BackendClient : IBackendClient
    {
        public async UniTask<T> Get<T>(IGetRequest request, IReadOnlyLifetime lifetime)
        {
            var responseContent = await GetRaw(request, lifetime);
            var result = JsonConvert.DeserializeObject<T>(responseContent);

            return result;
        }

        public async UniTask<string> GetRaw(IGetRequest request, IReadOnlyLifetime lifetime)
        {
            using var downloadHandlerBuffer = new DownloadHandlerBuffer();
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandlerBuffer, null);

            foreach (var header in request.Headers)
                webRequest.SetRequestHeader(header.Type, header.Value);

            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("GET request failed");

            var responseContent = downloadHandlerBuffer.text;

            return responseContent;
        }

        public async UniTask<T> Post<T>(IPostRequest request, IReadOnlyLifetime lifetime)
        {
            using var downloadHandlerBuffer = new DownloadHandlerBuffer();
            UploadHandlerRaw uploadHandler = null;

            if (request.Body != null)
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));

            using var webRequest = new UnityWebRequest(request.Uri, "POST", downloadHandlerBuffer, uploadHandler);

            foreach (var header in request.Headers)
                webRequest.SetRequestHeader(header.Type, header.Value);

            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("POST request failed");

            var responseContent = downloadHandlerBuffer.text;
            var result = JsonConvert.DeserializeObject<T>(responseContent);

            uploadHandler?.Dispose();

            return result;
        }

        public async UniTask<AudioClip> GetAudio(IGetRequest request, AudioType audioType, IReadOnlyLifetime lifetime)
        {
            using var downloadHandlerAudioClip = new DownloadHandlerAudioClip(request.Uri, audioType);
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandlerAudioClip, null);

            foreach (var header in request.Headers)
                webRequest.SetRequestHeader(header.Type, header.Value);

            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("GET request failed");

            return downloadHandlerAudioClip.audioClip;
        }

        public async UniTask<Texture2D> GetImage(IGetRequest request, IReadOnlyLifetime lifetime)
        {
            using var downloadHandler = new DownloadHandlerTexture(true);
            using var webRequest = new UnityWebRequest(request.Uri, "GET", downloadHandler, null);
            await webRequest.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);

            if (webRequest.result != UnityWebRequest.Result.Success)
                throw new Exception("GET request failed");

            return downloadHandler.texture;
        }
    }
}