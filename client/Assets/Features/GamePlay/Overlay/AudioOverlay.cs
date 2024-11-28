using GamePlay.Audio;
using Internal;
using TMPro;
using UnityEngine;

namespace GamePlay.Overlay
{
    [DisallowMultipleComponent]
    public class AudioOverlay : MonoBehaviour, ISceneService, IAudioOverlay
    {
        [SerializeField] private TMP_Text _author;
        [SerializeField] private TMP_Text _title;
        
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IAudioOverlay>();
        }

        public void Show(SongMetadata data)
        {
            _author.text = data.Author;
            _title.text = data.Name;
        }
    }
}