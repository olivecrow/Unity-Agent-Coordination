using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    internal static class ProjectSkillInstaller
    {
        internal const string PackageName = "com.olivecrow.unity-agent-coordination";
        internal const string SkillName = "unity-agent-coordination";
        internal const string OwnershipFile = ".package-owned";
        internal static readonly string[] Files =
        {
            "SKILL.md", "agents/openai.yaml", "references/mcp-protocol.md"
        };

        internal static bool Install(string projectPath, string packagePath, Action<string> report)
        {
            string target = GetSkillPath(projectPath);
            // Read the complete source before creating or changing project files.
            byte[][] contents = Files.Select(file => File.ReadAllBytes(
                Path.Combine(packagePath, "Skills~", SkillName, file))).ToArray();
            string marker = Path.Combine(target, OwnershipFile);
            if (Directory.Exists(target) && !CanChange(target, marker, report))
            {
                return false;
            }

            for (int i = 0; i < Files.Length; i++)
            {
                string destination = Path.Combine(target, Files[i]);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (!File.Exists(destination) || !File.ReadAllBytes(destination).SequenceEqual(contents[i]))
                {
                    File.WriteAllBytes(destination, contents[i]);
                }
            }

            byte[] ownership = Encoding.UTF8.GetBytes(PackageName + "\n" +
                string.Join("\n", contents.Select(Hash)) + "\n");
            if (!File.Exists(marker) || !File.ReadAllBytes(marker).SequenceEqual(ownership))
            {
                File.WriteAllBytes(marker, ownership);
            }

            return true;
        }

        internal static bool Remove(string projectPath, Action<string> report)
        {
            string target = GetSkillPath(projectPath);
            string marker = Path.Combine(target, OwnershipFile);
            if (!Directory.Exists(target) || !CanChange(target, marker, report))
            {
                return false;
            }

            foreach (string file in Files)
            {
                File.Delete(Path.Combine(target, file));
            }

            File.Delete(marker);
            foreach (string directory in new[] { "agents", "references", "" })
            {
                string path = Path.Combine(target, directory);
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                {
                    Directory.Delete(path);
                }
            }

            return true;
        }

        private static bool CanChange(string target, string marker, Action<string> report)
        {
            string[] ownership = File.Exists(marker) ? File.ReadAllLines(marker) : Array.Empty<string>();
            if (ownership.Length != Files.Length + 1 || ownership[0] != PackageName ||
                ownership.Skip(1).Any(hash => hash.Length != 64 || !hash.All(Uri.IsHexDigit)))
            {
                report?.Invoke($"Project skill at {target} has no valid package ownership record; preserving it.");
                return false;
            }

            for (int i = 0; i < Files.Length; i++)
            {
                string path = Path.Combine(target, Files[i]);
                if (File.Exists(path) && Hash(File.ReadAllBytes(path)) != ownership[i + 1])
                {
                    report?.Invoke($"Project skill file {path} was modified; preserving the entire skill.");
                    return false;
                }
            }

            return true;
        }

        internal static string GetSkillPath(string projectPath)
        {
            string root = Path.GetFullPath(projectPath);
            if (!new[] { "Assets", "Packages", "ProjectSettings" }
                .All(directory => Directory.Exists(Path.Combine(root, directory))))
            {
                throw new IOException($"Not a Unity project: {root}");
            }

            string target = Path.Combine(root, ".agents", "skills", SkillName);
            // A project-local path must not redirect writes into a global skill directory.
            foreach (string file in Files.Concat(new[] { OwnershipFile }))
            {
                string current = Path.Combine(target, file);
                while (current != root)
                {
                    if ((File.Exists(current) || Directory.Exists(current)) &&
                        (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException($"Project skill path contains a symbolic link or junction: {current}");
                    }

                    current = Path.GetDirectoryName(current);
                }
            }

            return target;
        }

        private static string Hash(byte[] content)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
