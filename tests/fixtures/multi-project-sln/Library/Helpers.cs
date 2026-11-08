using Microsoft.Xna.Framework;

namespace MultiProject.Library
{
    public static class Helpers
    {
        public static Vector2 Clamp(Vector2 value, float maxLength)
        {
            float length = value.Length();
            if (length <= maxLength || length == 0f)
            {
                return value;
            }

            return value * (maxLength / length);
        }
    }
}
