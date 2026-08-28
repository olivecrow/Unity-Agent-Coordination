using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    public sealed class GitWorkspaceInspection
    {
        public string HeadSha { get; set; }
        public bool HasUncommittedChanges { get; set; }
        public string StatusOutput { get; set; }
    }

    public static class GitWorkspaceInspector
    {
        public static GitWorkspaceInspection Inspect(string projectPath)
        {
            string headSha = RepositoryIdentity.TryReadHeadSha(projectPath);
            if (string.IsNullOrWhiteSpace(headSha))
            {
                throw new CoordinationException("git_head_unavailable", "Cannot read the Unity project's Git HEAD.");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain --untracked-files=all",
                WorkingDirectory = Path.GetFullPath(projectPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new CoordinationException("git_unavailable", "Cannot start the git process.");
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(10000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Best effort only; the operation still fails below.
                    }

                    throw new CoordinationException("git_timeout", "git status did not finish within 10 seconds.");
                }

                if (process.ExitCode != 0)
                {
                    throw new CoordinationException("git_status_failed", error.Trim());
                }

                return new GitWorkspaceInspection
                {
                    HeadSha = headSha,
                    HasUncommittedChanges = !string.IsNullOrWhiteSpace(output),
                    StatusOutput = output.Trim()
                };
            }
        }
    }
}
