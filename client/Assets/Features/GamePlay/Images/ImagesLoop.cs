using System;
using Cysharp.Threading.Tasks;
using GamePlay.Common;
using Global.Backend;
using Internal;
using UnityEngine;

namespace GamePlay.Images
{
    public class ImagesLoop : IScopeSetup
    {
        public ImagesLoop(
            IBackendClient backend,
            IImageView view,
            BackendEndpoints endpoints,
            ImagesOptions options)
        {
            _backend = backend;
            _view = view;
            _endpoints = endpoints;
            _options = options;
        }

        private readonly IBackendClient _backend;
        private readonly IImageView _view;
        private readonly BackendEndpoints _endpoints;
        private readonly ImagesOptions _options;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Process(lifetime).Forget();
        }

        private async UniTask Process(IReadOnlyLifetime lifetime)
        {
            var getNextEndpoint = _endpoints.Url + "image/getNext";
            var index = 0;

            while (lifetime.IsTerminated == false)
            {
                var body = new ImageRequest()
                {
                    Index = index
                };

                var response = await _backend.Post<ImageData, ImageRequest>(
                    getNextEndpoint,
                    body,
                    true,
                    lifetime,
                    RequestHeader.Json());

                var texture = await _backend.GetImage(response.Url, true, lifetime);

                var sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));

                await _view.SetImage(sprite, lifetime);

                index++;

                await UniTask.Delay(TimeSpan.FromSeconds(_options.SwitchDelay), cancellationToken: lifetime.Token);
            }
        }
    }
}