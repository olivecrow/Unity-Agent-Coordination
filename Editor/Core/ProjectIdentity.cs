using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Olivecrow.UnityAgentCoordination
{
    internal static class ProjectIdentity
    {
        internal static string LockName(string projectPath)
        {
            string path = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            if (windows) path = path.ToLowerInvariant();
            using (SHA256 hash = SHA256.Create())
            {
                string id = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(path)).Take(16)
                    .Select(value => value.ToString("x2")));
                return (windows ? "Local\\" : "") + "OlivecrowUnityEditorTickets_" + id;
            }
        }
    }
}
