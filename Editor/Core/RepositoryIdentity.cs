using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Olivecrow.UnityAgentCoordination
{
    public static class RepositoryIdentity
    {
        public static string ResolveRepositoryId(string projectPath)
        {
            string commonGitDirectory = ResolveCommonGitDirectory(projectPath);
            string identityPath = NormalizePath(commonGitDirectory ?? Path.GetFullPath(projectPath));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(identityPath));
                return string.Concat(digest.Take(10).Select(value => value.ToString("x2")));
            }
        }

        public static string ResolveStateRoot(string projectPath)
        {
            string overriddenRoot = Environment.GetEnvironmentVariable("UNITY_AGENT_COORDINATOR_ROOT");
            string baseRoot = string.IsNullOrWhiteSpace(overriddenRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Olivecrow", "UnityAgentCoordination")
                : Path.GetFullPath(overriddenRoot);
            return Path.Combine(baseRoot, ResolveRepositoryId(projectPath));
        }

        public static string TryReadHeadSha(string worktreePath)
        {
            string gitDirectory = ResolveGitDirectory(worktreePath);
            if (gitDirectory == null)
            {
                return null;
            }

            string headPath = Path.Combine(gitDirectory, "HEAD");
            if (!File.Exists(headPath))
            {
                return null;
            }

            string head = File.ReadAllText(headPath).Trim();
            if (!head.StartsWith("ref: ", StringComparison.Ordinal))
            {
                return IsSha(head) ? head.ToLowerInvariant() : null;
            }

            string reference = head.Substring(5).Trim().Replace('/', Path.DirectorySeparatorChar);
            string commonGitDirectory = ResolveCommonGitDirectory(worktreePath);
            foreach (string candidate in new[]
                     {
                         Path.Combine(gitDirectory, reference),
                         commonGitDirectory == null ? null : Path.Combine(commonGitDirectory, reference)
                     }.Where(path => path != null))
            {
                if (File.Exists(candidate))
                {
                    string value = File.ReadAllText(candidate).Trim();
                    if (IsSha(value))
                    {
                        return value.ToLowerInvariant();
                    }
                }
            }

            string packedRefsPath = commonGitDirectory == null
                ? null
                : Path.Combine(commonGitDirectory, "packed-refs");
            if (!string.IsNullOrEmpty(packedRefsPath) && File.Exists(packedRefsPath))
            {
                string slashReference = reference.Replace(Path.DirectorySeparatorChar, '/');
                foreach (string line in File.ReadLines(packedRefsPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed[0] == '^')
                    {
                        continue;
                    }

                    string[] parts = trimmed.Split(new[] { ' ' }, 2);
                    if (parts.Length == 2 && parts[1] == slashReference && IsSha(parts[0]))
                    {
                        return parts[0].ToLowerInvariant();
                    }
                }
            }

            return null;
        }

        public static string ResolveGitDirectory(string worktreePath)
        {
            string root = Path.GetFullPath(worktreePath);
            string dotGit = Path.Combine(root, ".git");
            if (Directory.Exists(dotGit))
            {
                return Path.GetFullPath(dotGit);
            }

            if (!File.Exists(dotGit))
            {
                return null;
            }

            string pointer = File.ReadAllText(dotGit).Trim();
            const string prefix = "gitdir:";
            if (!pointer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string value = pointer.Substring(prefix.Length).Trim();
            return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(root, value));
        }

        public static string ResolveCommonGitDirectory(string worktreePath)
        {
            string gitDirectory = ResolveGitDirectory(worktreePath);
            if (gitDirectory == null)
            {
                return null;
            }

            string commonPointer = Path.Combine(gitDirectory, "commondir");
            if (!File.Exists(commonPointer))
            {
                return gitDirectory;
            }

            string value = File.ReadAllText(commonPointer).Trim();
            return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(gitDirectory, value));
        }

        public static string NormalizePath(string path)
        {
            string normalized = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return Environment.OSVersion.Platform == PlatformID.Win32NT
                ? normalized.ToLowerInvariant()
                : normalized;
        }

        private static bool IsSha(string value)
        {
            return value != null && value.Length >= 40 && value.All(character =>
                character >= '0' && character <= '9' ||
                character >= 'a' && character <= 'f' ||
                character >= 'A' && character <= 'F');
        }
    }
}
