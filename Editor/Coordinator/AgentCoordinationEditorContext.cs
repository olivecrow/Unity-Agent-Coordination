using System.IO;
using UnityEditor;
using UnityEngine;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    [InitializeOnLoad]
    public static class AgentCoordinationEditorContext
    {
        static AgentCoordinationEditorContext()
        {
            ProjectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Store = new CoordinationStore(ProjectPath);
            Service = new CoordinationService(Store);
            AssemblyReloadEvents.beforeAssemblyReload += Store.Dispose;
        }

        public static string ProjectPath { get; }
        public static CoordinationStore Store { get; }
        public static CoordinationService Service { get; }

        public static bool IsReady => !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !BuildPipeline.isBuildingPlayer && EditorApplication.isPlaying == EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
