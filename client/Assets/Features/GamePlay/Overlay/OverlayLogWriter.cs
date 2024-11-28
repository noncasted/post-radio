using TMPro;
using UnityEngine;

namespace GamePlay.Overlay
{
    [DisallowMultipleComponent]
    public class OverlayLogWriter : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            _text.text += $"{logString}" + "\n";
        }
    }
}