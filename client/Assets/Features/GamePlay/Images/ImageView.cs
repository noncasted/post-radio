using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GamePlay.Images
{
    [DisallowMultipleComponent]
    public class ImageView : MonoBehaviour, IImageView, ISceneService
    {
        [SerializeField] private Image _first;
        [SerializeField] private Image _second;
        
        private ImagesOptions _options;

        [Inject]
        private void Construct(ImagesOptions options)
        {
            _options = options;
        }
        
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IImageView>();
        }
        
        public async UniTask SetImage(Sprite image, IReadOnlyLifetime lifetime)
        {
            var timer = 0f;
            var nextColor = Color.white;
            nextColor.a = 0f;

            _second.sprite = image;

            while (timer < _options.TransitionTime)
            {
                nextColor.a = timer / _options.TransitionTime;
                timer += Time.deltaTime;

                _second.color = nextColor;

                await UniTask.Yield(lifetime.Token);
            }

            _first.sprite = image;
            nextColor.a = 1f;
            _first.color = nextColor;

            nextColor.a = 0f;
            _second.color = nextColor;
        }
    }
}