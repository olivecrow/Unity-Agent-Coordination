using System;
using System.Collections.Generic;
using System.Linq;
using Olivecrow.UnityAgentCoordination;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    [InitializeOnLoad]
    public static class ValidationRunner
    {
        private const string RequestedCompilationKey = "Olivecrow.UnityAgentCoordination.RequestedCompilation";
        private static readonly TestRunnerApi TestApi;
        private static readonly ValidationTestCallbacks TestCallbacks;

        static ValidationRunner()
        {
            TestApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            TestApi.hideFlags = HideFlags.HideAndDontSave;
            TestCallbacks = new ValidationTestCallbacks();
            TestApi.RegisterCallbacks(TestCallbacks, 1000);
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.delayCall += ResumeActiveValidation;
        }

        public static ValidationRequest AcquireAndStart(string integratorAgentId, string requestId,
            string testedHeadSha, string sessionId = null)
        {
            AgentCoordinationEditorContext.RequireCoordinatorEditor();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new CoordinationException("editor_busy",
                    "Unity Editor is compiling, updating assets, or changing Play Mode state.");
            }

            GitWorkspaceInspection inspection = GitWorkspaceInspector.Inspect(
                AgentCoordinationEditorContext.ProjectPath);
            if (inspection.HeadSha != testedHeadSha.Trim().ToLowerInvariant())
            {
                throw new CoordinationException("actual_head_mismatch",
                    $"The actual Unity worktree HEAD is {inspection.HeadSha}.");
            }

            if (inspection.HasUncommittedChanges)
            {
                throw new CoordinationException("tracked_worktree_dirty",
                    "The serial-validation worktree has uncommitted changes:\n" + inspection.StatusOutput);
            }

            ValidationRequest request = AgentCoordinationEditorContext.Service.AcquireValidation(
                integratorAgentId, requestId, inspection.HeadSha, sessionId);
            SessionState.SetString(RequestedCompilationKey, request.RequestId);
            RefreshAssetsAndRequestCompilation(
                options => AssetDatabase.Refresh(options),
                () => CompilationPipeline.RequestScriptCompilation());
            return request;
        }

        internal static void RefreshAssetsAndRequestCompilation(
            Action<ImportAssetOptions> refreshAssets,
            Action requestScriptCompilation)
        {
            refreshAssets(ImportAssetOptions.ForceSynchronousImport);
            requestScriptCompilation();
        }

        private static void OnCompilationStarted(object _)
        {
            ValidationRequest request = TryGetActiveRequest(ValidationPhases.Compiling);
            if (request == null)
            {
                return;
            }

            SafeUpdate(() => AgentCoordinationEditorContext.Service.MarkCompilationStarted(request.RequestId));
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            ValidationRequest request = TryGetActiveRequest(ValidationPhases.Compiling);
            if (request == null)
            {
                return;
            }

            List<string> errors = messages.Where(message => message.type == CompilerMessageType.Error)
                .Select(message => $"{assemblyPath}: {message.message}")
                .ToList();
            if (errors.Count > 0)
            {
                SafeUpdate(() => AgentCoordinationEditorContext.Service.AddCompilationErrors(
                    request.RequestId, errors));
            }
        }

        private static void OnCompilationFinished(object _)
        {
            ValidationRequest request = TryGetActiveRequest(ValidationPhases.Compiling);
            if (request == null)
            {
                return;
            }

            SessionState.EraseString(RequestedCompilationKey);
            SafeUpdate(() => AgentCoordinationEditorContext.Service.MarkCompilationFinished(request.RequestId));
            EditorApplication.delayCall += ResumeActiveValidation;
        }

        private static void ResumeActiveValidation()
        {
            if (!AgentCoordinationEditorContext.IsCoordinatorEditor || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            CoordinationState state;
            try
            {
                state = AgentCoordinationEditorContext.Store.ReadState();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Unity Agent Coordination] Failed to recover validation state: " + exception.Message);
                return;
            }

            if (state.ActiveValidation == null)
            {
                return;
            }

            ValidationRequest request = state.FindRequest(state.ActiveValidation.RequestId);
            if (request == null)
            {
                return;
            }

            if (request.Phase == ValidationPhases.Testing &&
                !string.IsNullOrWhiteSpace(request.CurrentTestMode) &&
                string.IsNullOrWhiteSpace(request.TestJobId))
            {
                RunCurrentTest(request);
            }
        }

        private static void RunCurrentTest(ValidationRequest request)
        {
            TestMode mode = request.CurrentTestMode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;
            string jobId = TestApi.Execute(new ExecutionSettings(new Filter { testMode = mode }));
            AgentCoordinationEditorContext.Service.SetTestJobId(request.RequestId, jobId);
        }

        private static ValidationRequest TryGetActiveRequest(string requiredPhase)
        {
            try
            {
                CoordinationState state = AgentCoordinationEditorContext.Store.ReadState();
                if (state.ActiveValidation == null || state.ActiveValidation.Phase != requiredPhase)
                {
                    return null;
                }

                return state.FindRequest(state.ActiveValidation.RequestId);
            }
            catch
            {
                return null;
            }
        }

        private static void SafeUpdate(Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Unity Agent Coordination] Failed to update validation state: " + exception.Message);
            }
        }

        private sealed class ValidationTestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                ValidationRequest request = TryGetActiveRequest(ValidationPhases.Testing);
                if (request == null)
                {
                    return;
                }

                SafeUpdate(() =>
                {
                    List<string> failures = CollectFailures(result).ToList();
                    if (result.FailCount > 0 && failures.Count == 0)
                    {
                        failures.Add($"{request.CurrentTestMode}: {result.FailCount} tests failed. {result.Message}");
                    }

                    AgentCoordinationEditorContext.Service.AddTestFailures(request.RequestId, failures);
                    AgentCoordinationEditorContext.Service.CompleteCurrentTest(request.RequestId);
                });
                EditorApplication.delayCall += ResumeActiveValidation;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            private static IEnumerable<string> CollectFailures(ITestResultAdaptor result)
            {
                if (!result.HasChildren)
                {
                    if (result.FailCount > 0)
                    {
                        yield return $"{result.FullName}: {result.Message}\n{result.StackTrace}".Trim();
                    }

                    yield break;
                }

                foreach (ITestResultAdaptor child in result.Children)
                {
                    foreach (string failure in CollectFailures(child))
                    {
                        yield return failure;
                    }
                }
            }
        }
    }
}
