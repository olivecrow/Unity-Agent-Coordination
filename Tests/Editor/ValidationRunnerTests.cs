using System;
using System.Collections.Generic;
using Olivecrow.UnityAgentCoordination.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Olivecrow.UnityAgentCoordination.Tests
{
    public sealed class ValidationRunnerTests
    {
        [Test]
        public void RefreshAssetsAndRequestCompilation_RefreshesSynchronouslyBeforeCompilation()
        {
            var calls = new List<string>();
            ImportAssetOptions? refreshOptions = null;

            ValidationRunner.RefreshAssetsAndRequestCompilation(
                options =>
                {
                    refreshOptions = options;
                    calls.Add("refresh");
                },
                () => calls.Add("compile"));

            Assert.That(refreshOptions, Is.EqualTo(ImportAssetOptions.ForceSynchronousImport));
            Assert.That(calls, Is.EqualTo(new[] { "refresh", "compile" }));
        }

        [TestCase("inspecting", "Inspecting")]
        [TestCase("waiting_editor_access", "Waiting for Editor Access")]
        [TestCase("using_editor", "Using Editor")]
        [TestCase("waiting_validation", "Waiting for Validation")]
        [TestCase("testing", "Testing")]
        [TestCase(null, "Unspecified")]
        public void CoordinationWindowFormatsKnownPhases(string phase, string expected)
        {
            Assert.That(AgentCoordinationWindow.FormatPhase(phase), Is.EqualTo(expected));
        }

        [Test]
        public void CoordinationWindowFormatsElapsedTimeForOperators()
        {
            DateTime now = new DateTime(2026, 8, 13, 7, 0, 0, DateTimeKind.Utc);

            Assert.That(AgentCoordinationWindow.FormatElapsed(now.AddSeconds(-12), now), Is.EqualTo("12s"));
            Assert.That(AgentCoordinationWindow.FormatElapsed(now.AddMinutes(-3).AddSeconds(-4), now),
                Is.EqualTo("3m 4s"));
            Assert.That(AgentCoordinationWindow.FormatElapsed(now.AddHours(-2).AddMinutes(-5), now),
                Is.EqualTo("2h 5m"));
        }

        [TestCase(null, "parameters_required")]
        [TestCase("", "parameters_required")]
        [TestCase("[]", "invalid_json")]
        [TestCase("{", "invalid_json")]
        [TestCase("{}", "missing_parameter")]
        public void TransportNeutralRouterReturnsStableInputErrors(string json, string expectedErrorCode)
        {
            CoordinationCommandResult result = UnityAgentCoordinationCommandRouter.ExecuteJson(json);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(expectedErrorCode));
            Assert.That(result.ToJson(), Does.Contain($"\"error_code\":\"{expectedErrorCode}\""));
        }
    }
}
