using Cysharp.Threading.Tasks;
using GamePlay.Common;
using GamePlay.Overlay;
using Global.Backend;
using Internal;

namespace GamePlay.Audio
{
    public class AudioLoop : IScopeSetup
    {
        public AudioLoop(
            IBackendClient backend,
            IAudioPlayer player,
            IAudioOverlay overlay,
            BackendEndpoints endpoints)
        {
            _backend = backend;
            _player = player;
            _overlay = overlay;
            _endpoints = endpoints;
        }

        private readonly IBackendClient _backend;
        private readonly IAudioPlayer _player;
        private readonly IAudioOverlay _overlay;
        private readonly BackendEndpoints _endpoints;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Process(lifetime).Forget();
        }

        private async UniTask Process(IReadOnlyLifetime lifetime)
        {
            var getNextEndpoint = _endpoints.Url + "audio/getNext";
            var index = 0;

            while (lifetime.IsTerminated == false)
            {
                var body = new GetNextTrackRequest()
                {
                    Index = index
                };

                var response = await _backend.Post<TrackData, GetNextTrackRequest>(
                    getNextEndpoint,
                    body,
                    true,
                    lifetime,
                    RequestHeader.Json());

                _overlay.Show(response.Metadata);
                
                var clip = await _backend.GetAudio(response.DownloadUrl, true, lifetime);

                await _player.Play(clip, lifetime);
                index++;
            }
        }
    }
}