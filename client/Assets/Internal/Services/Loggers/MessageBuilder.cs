using System.Text;

namespace Internal
{
    public class MessageBuilder
    {
        public string Build(string message, ILogParameters parameters)
        {
            var stringBuilder = new StringBuilder();

            var headers = parameters.Headers;

            foreach (var header in headers)
            {
                if (header.IsColored == true)
                    stringBuilder.Append(ApplyColor($"[{header.Name}]", header.Color));
                else
                    stringBuilder.Append($"[{header.Name}]");
            }

            stringBuilder.Append(": ");

            if (parameters.BodyParameters.IsColored == true)
                stringBuilder.Append(ApplyColor(message, parameters.BodyParameters.Color));
            else
                stringBuilder.Append(message);

            return stringBuilder.ToString();
        }

        private string ApplyColor(string log, string color)
        {
#if !UNITY_EDITOR
            return log;
#endif
            return $"<color=#{color}>{log}</color>";
        }
    }
}