using UnityEngine;

namespace Global.Systems
{
    public class ApplicationProxy : IApplicationFlow, IScreen
    {
        public Vector2 Resolution => new(Screen.width, Screen.height);

        public ScreenMode ScreenMode => GetScreenMode();

        public void Quit()
        {
            Application.Quit();
        }

        private ScreenMode GetScreenMode()
        {
            if (Screen.height > Screen.width)
                return ScreenMode.Vertical;

            return ScreenMode.Horizontal;
        }
    }
}