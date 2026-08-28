using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Olivecrow.UnityAgentCoordination
{
    public sealed class CoordinationStore
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private readonly object processGate = new object();
        private readonly Mutex crossProcessMutex;

        public CoordinationStore(string projectPath)
        {
            ProjectPath = Path.GetFullPath(projectPath);
            RepositoryId = RepositoryIdentity.ResolveRepositoryId(ProjectPath);
            StateRoot = RepositoryIdentity.ResolveStateRoot(ProjectPath);
            StatePath = Path.Combine(StateRoot, "coordination.json");
            EditorOwnerPath = Path.Combine(StateRoot, "editor-owner.json");
            crossProcessMutex = new Mutex(false, "Local\\OlivecrowUnityAgentCoordination_" + RepositoryId);
        }

        public string ProjectPath { get; }
        public string RepositoryId { get; }
        public string StateRoot { get; }
        public string StatePath { get; }
        public string EditorOwnerPath { get; }
        public bool HasCoordinationState => File.Exists(StatePath);

        public CoordinationState ReadState()
        {
            return WithLock(ReadStateUnsafe);
        }

        public EditorOwnerRecord ReadEditorOwner()
        {
            return WithLock(() => ReadJsonUnsafe<EditorOwnerRecord>(EditorOwnerPath));
        }

        public T Mutate<T>(Func<CoordinationState, T> mutation)
        {
            return WithLock(() =>
            {
                CoordinationState state = ReadStateUnsafe();
                T result = mutation(state);
                state.RepositoryId = RepositoryId;
                state.UpdatedUtc = DateTime.UtcNow;
                SaveStateUnsafe(state);
                return result;
            });
        }

        public bool TryAcquireEditor(EditorOwnerRecord proposedOwner, TimeSpan staleAfter)
        {
            return TryAcquireEditor(proposedOwner, staleAfter, false);
        }

        public bool TryRecoverEditor(EditorOwnerRecord proposedOwner, TimeSpan staleAfter)
        {
            return TryAcquireEditor(proposedOwner, staleAfter, true);
        }

        private bool TryAcquireEditor(EditorOwnerRecord proposedOwner, TimeSpan staleAfter,
            bool requireExistingForeignOwner)
        {
            return WithLock(() =>
            {
                EditorOwnerRecord current = ReadJsonUnsafe<EditorOwnerRecord>(EditorOwnerPath);
                bool sameSession = current != null && current.SessionId == proposedOwner.SessionId;
                if (requireExistingForeignOwner && (current == null || sameSession))
                {
                    return false;
                }

                bool heartbeatExpired = current != null &&
                                        DateTime.UtcNow - current.HeartbeatUtc > staleAfter;
                bool processExited = current != null && !IsEditorProcessRunning(current);
                if (!sameSession && current != null && !heartbeatExpired && !processExited)
                {
                    return false;
                }

                Directory.CreateDirectory(StateRoot);
                WriteJsonAtomically(EditorOwnerPath, proposedOwner);
                return true;
            });
        }

        private static bool IsEditorProcessRunning(EditorOwnerRecord owner)
        {
            if (owner.ProcessId <= 0)
            {
                return false;
            }

            try
            {
                using (Process process = Process.GetProcessById(owner.ProcessId))
                {
                    if (process.HasExited)
                    {
                        return false;
                    }

                    if (owner.StartedUtc == default(DateTime))
                    {
                        return true;
                    }

                    try
                    {
                        DateTime processStartedUtc = process.StartTime.ToUniversalTime();
                        return processStartedUtc <= owner.StartedUtc.ToUniversalTime().AddSeconds(2);
                    }
                    catch
                    {
                        return true;
                    }
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch
            {
                return true;
            }
        }

        public void HeartbeatEditor(string sessionId)
        {
            WithLock(() =>
            {
                EditorOwnerRecord owner = ReadJsonUnsafe<EditorOwnerRecord>(EditorOwnerPath);
                if (owner != null && owner.SessionId == sessionId)
                {
                    owner.HeartbeatUtc = DateTime.UtcNow;
                    WriteJsonAtomically(EditorOwnerPath, owner);
                }

                return true;
            });
        }

        public void ReleaseEditorAndClear(string sessionId)
        {
            WithLock(() =>
            {
                EditorOwnerRecord owner = ReadJsonUnsafe<EditorOwnerRecord>(EditorOwnerPath);
                if (owner == null || owner.SessionId == sessionId)
                {
                    DeleteIfExists(StatePath);
                    DeleteIfExists(EditorOwnerPath);
                    DeleteDirectoryIfEmpty(StateRoot);
                }

                return true;
            });
        }

        public void ReleaseEditorOwnership(string sessionId)
        {
            WithLock(() =>
            {
                EditorOwnerRecord owner = ReadJsonUnsafe<EditorOwnerRecord>(EditorOwnerPath);
                if (owner != null && owner.SessionId == sessionId)
                {
                    DeleteIfExists(EditorOwnerPath);
                    DeleteDirectoryIfEmpty(StateRoot);
                }

                return true;
            });
        }

        public void ClearCoordinationState()
        {
            WithLock(() =>
            {
                DeleteIfExists(StatePath);
                DeleteDirectoryIfEmpty(StateRoot);
                return true;
            });
        }

        private CoordinationState ReadStateUnsafe()
        {
            CoordinationState state = ReadJsonUnsafe<CoordinationState>(StatePath) ?? new CoordinationState();
            state.Agents = state.Agents ?? new System.Collections.Generic.List<AgentRecord>();
            state.Claims = state.Claims ?? new System.Collections.Generic.List<ScopeClaim>();
            state.ValidationRequests = state.ValidationRequests ?? new System.Collections.Generic.List<ValidationRequest>();
            foreach (ValidationRequest request in state.ValidationRequests)
            {
                request.TestModes = request.TestModes ?? new System.Collections.Generic.List<string>();
                request.ManualChecks = request.ManualChecks ?? new System.Collections.Generic.List<string>();
                request.CompilationErrors = request.CompilationErrors ?? new System.Collections.Generic.List<string>();
                request.TestFailures = request.TestFailures ?? new System.Collections.Generic.List<string>();
            }

            return state;
        }

        private void SaveStateUnsafe(CoordinationState state)
        {
            if (state.IsEmpty)
            {
                DeleteIfExists(StatePath);
                DeleteDirectoryIfEmpty(StateRoot);
                return;
            }

            Directory.CreateDirectory(StateRoot);
            WriteJsonAtomically(StatePath, state);
        }

        private static T ReadJsonUnsafe<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Utf8WithoutBom));
            }
            catch (Exception exception)
            {
                throw new CoordinationException("state_corrupt",
                    $"Cannot read the coordination state file: {path}. {exception.Message}");
            }
        }

        private static void WriteJsonAtomically(string path, object value)
        {
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string json = JsonConvert.SerializeObject(value, Formatting.Indented);
            File.WriteAllText(temporaryPath, json, Utf8WithoutBom);
            try
            {
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                DeleteIfExists(temporaryPath);
            }
        }

        private T WithLock<T>(Func<T> operation)
        {
            lock (processGate)
            {
                bool acquired = false;
                try
                {
                    try
                    {
                        acquired = crossProcessMutex.WaitOne(TimeSpan.FromSeconds(15));
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                    {
                        throw new CoordinationException("state_busy", "Could not acquire the coordination state lock within 15 seconds.");
                    }

                    return operation();
                }
                finally
                {
                    if (acquired)
                    {
                        crossProcessMutex.ReleaseMutex();
                    }
                }
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void DeleteDirectoryIfEmpty(string path)
        {
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
            {
                Directory.Delete(path);
            }
        }
    }
}
