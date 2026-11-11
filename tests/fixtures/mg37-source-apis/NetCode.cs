using Microsoft.Xna.Framework.Net;

namespace Mg37SourceApis
{
    /// <summary>Uses the removed networking namespace. Never built.</summary>
    public class NetCode
    {
        public void Join()
        {
            var sessions = NetworkSession.Find(NetworkSessionType.SystemLink, 1, null);
        }
    }
}
