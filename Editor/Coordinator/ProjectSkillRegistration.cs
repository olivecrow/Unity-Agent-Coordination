using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    [InitializeOnLoad]
    internal static class ProjectSkillRegistration
    {
        static ProjectSkillRegistration()
        {
            EditorApplication.delayCall += Install;
            Events.registeringPackages += OnRegisteringPackages;
        }

        private static string ProjectPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static void Install()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(ProjectSkillRegistration).Assembly);
            if (package == null || package.name != ProjectSkillInstaller.PackageName)
            {
                return;
            }

            try
            {
                ProjectSkillInstaller.Install(ProjectPath, package.resolvedPath, Warn);
            }
            catch (IOException exception)
            {
                Warn(exception.Message);
            }
            catch (System.UnauthorizedAccessException exception)
            {
                Warn(exception.Message);
            }
        }

        private static void OnRegisteringPackages(PackageRegistrationEventArgs changes)
        {
            foreach (PackageInfo package in changes.removed)
            {
                if (package.name != ProjectSkillInstaller.PackageName)
                {
                    continue;
                }

                EditorApplication.delayCall -= Install;
                try
                {
                    ProjectSkillInstaller.Remove(ProjectPath, Warn);
                }
                catch (IOException exception)
                {
                    Warn(exception.Message);
                }
                catch (System.UnauthorizedAccessException exception)
                {
                    Warn(exception.Message);
                }
            }
        }

        private static void Warn(string message)
        {
            Debug.LogWarning("[Unity Agent Coordination] " + message);
        }
    }
}
