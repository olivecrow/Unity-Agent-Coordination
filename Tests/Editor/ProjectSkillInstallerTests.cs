using System;
using System.IO;
using System.Linq;
using Olivecrow.UnityAgentCoordination.Editor;
using NUnit.Framework;

namespace Olivecrow.UnityAgentCoordination.Tests
{
    public sealed class ProjectSkillInstallerTests
    {
        private string root;
        private string project;
        private string package;
        private string target;
        private string warning;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "coordination-skill-tests-" + Guid.NewGuid().ToString("N"));
            project = Path.Combine(root, "project");
            package = Path.Combine(root, "package-cache", "package@hash");
            foreach (string directory in new[] { "Assets", "Packages", "ProjectSettings" })
                Directory.CreateDirectory(Path.Combine(project, directory));
            foreach (string file in ProjectSkillInstaller.Files)
            {
                string path = Path.Combine(package, "Skills~", ProjectSkillInstaller.SkillName, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "packaged " + file);
            }
            target = ProjectSkillInstaller.GetSkillPath(project);
            warning = null;
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(root, true);
        }

        private bool Install() => ProjectSkillInstaller.Install(project, package, message => warning = message);

        [Test]
        public void InstallationCopiesAllResourcesIntoOnlyTheConsumingProject()
        {
            Assert.That(Install(), Is.True);
            foreach (string file in ProjectSkillInstaller.Files)
                Assert.That(File.ReadAllBytes(Path.Combine(target, file)), Is.EqualTo(File.ReadAllBytes(
                    Path.Combine(package, "Skills~", ProjectSkillInstaller.SkillName, file))));
            Assert.That(Directory.Exists(Path.Combine(root, ".agents")), Is.False);
            Assert.That(warning, Is.Null);
        }

        [Test]
        public void RepeatedRegistrationDoesNotRewriteFiles()
        {
            Install();
            string[] files = Directory.GetFiles(target, "*", SearchOption.AllDirectories);
            DateTime sentinel = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (string file in files) File.SetLastWriteTimeUtc(file, sentinel);
            Assert.That(Install(), Is.True);
            Assert.That(files.All(file => File.GetLastWriteTimeUtc(file) == sentinel), Is.True);
        }

        [Test]
        public void PackageUpdateRefreshesUnmodifiedFiles()
        {
            Install();
            File.WriteAllText(Path.Combine(package, "Skills~", ProjectSkillInstaller.SkillName, "SKILL.md"), "new version");
            Assert.That(Install(), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(target, "SKILL.md")), Is.EqualTo("new version"));
            Assert.That(ProjectSkillInstaller.Remove(project, null), Is.True);
        }

        [Test]
        public void ExistingUnmanagedSkillIsPreserved()
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "SKILL.md"), "user skill");
            Assert.That(Install(), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(target, "SKILL.md")), Is.EqualTo("user skill"));
            Assert.That(File.Exists(Path.Combine(target, ProjectSkillInstaller.OwnershipFile)), Is.False);
            Assert.That(warning, Does.Contain("preserving"));
        }

        [Test]
        public void CustomizedSkillBlocksTheWholeUpdateAndRemoval()
        {
            Install();
            File.WriteAllText(Path.Combine(target, "SKILL.md"), "user customization");
            string protocol = Path.Combine(target, "references/mcp-protocol.md");
            byte[] previous = File.ReadAllBytes(protocol);
            File.WriteAllText(Path.Combine(package, "Skills~", ProjectSkillInstaller.SkillName, "references/mcp-protocol.md"), "updated protocol");
            Assert.That(Install(), Is.False);
            Assert.That(File.ReadAllBytes(protocol), Is.EqualTo(previous));
            Assert.That(ProjectSkillInstaller.Remove(project, message => warning = message), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(target, "SKILL.md")), Is.EqualTo("user customization"));
        }

        [Test]
        public void RemovalPreservesUnrelatedFilesAndOtherSkills()
        {
            Install();
            File.WriteAllText(Path.Combine(target, "user-notes.txt"), "keep");
            string other = Path.Combine(project, ".agents/skills/other/SKILL.md");
            Directory.CreateDirectory(Path.GetDirectoryName(other));
            File.WriteAllText(other, "other skill");
            Assert.That(ProjectSkillInstaller.Remove(project, null), Is.True);
            Assert.That(ProjectSkillInstaller.Files.Any(file => File.Exists(Path.Combine(target, file))), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(target, "user-notes.txt")), Is.EqualTo("keep"));
            Assert.That(File.ReadAllText(other), Is.EqualTo("other skill"));
        }

        [Test]
        public void RemovalDeletesEmptyOwnedSkillDirectories()
        {
            Install();
            Assert.That(ProjectSkillInstaller.Remove(project, null), Is.True);
            Assert.That(Directory.Exists(target), Is.False);
        }

        [Test]
        public void MissingPackageResourceLeavesTheProjectUntouched()
        {
            File.Delete(Path.Combine(package, "Skills~", ProjectSkillInstaller.SkillName, "references/mcp-protocol.md"));
            Assert.Throws<FileNotFoundException>(() => Install());
            Assert.That(Directory.Exists(Path.Combine(project, ".agents")), Is.False);
        }

        [Test]
        public void CorruptOwnershipRecordDoesNotGrantWriteOrDeleteAccess()
        {
            Install();
            File.WriteAllText(Path.Combine(target, ProjectSkillInstaller.OwnershipFile), "invalid record");
            Assert.That(Install(), Is.False);
            Assert.That(ProjectSkillInstaller.Remove(project, null), Is.False);
            Assert.That(File.Exists(Path.Combine(target, "SKILL.md")), Is.True);
        }
    }
}
