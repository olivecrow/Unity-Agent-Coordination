using System;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [InitializeOnLoad]
    internal static class McpToolRegistrar
    {
        private const int MaximumAttempts = 30;
        private const double RetryIntervalSeconds = 2d;
        private static int attemptsRemaining = MaximumAttempts;
        private static double nextAttemptTime;

        static McpToolRegistrar()
        {
            EditorApplication.update += TryRegister;
        }

        private static void TryRegister()
        {
            if (EditorApplication.timeSinceStartup < nextAttemptTime)
            {
                return;
            }

            nextAttemptTime = EditorApplication.timeSinceStartup + RetryIntervalSeconds;
            attemptsRemaining--;
            try
            {
                IMcpTransportClient httpClient =
                    MCPServiceLocator.TransportManager.GetClient(TransportMode.Http);
                IMcpTransportClient stdioClient =
                    MCPServiceLocator.TransportManager.GetClient(TransportMode.Stdio);

                bool httpConnected = httpClient != null && httpClient.IsConnected;
                bool stdioConnected = stdioClient != null && stdioClient.IsConnected;
                if (httpConnected || stdioConnected)
                {
                    MCPServiceLocator.ToolDiscovery.InvalidateCache();
                    MCPServiceLocator.ResourceDiscovery.InvalidateCache();
                    EditorApplication.update -= TryRegister;

                    if (httpConnected)
                    {
                        _ = httpClient.ReregisterToolsAsync();
                    }

                    if (stdioConnected)
                    {
                        _ = stdioClient.ReregisterToolsAsync();
                    }

                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Unity Agent Coordination] Coplay MCP tool registration failed: " +
                                 exception.Message);
            }

            if (attemptsRemaining <= 0)
            {
                EditorApplication.update -= TryRegister;
                Debug.LogWarning("[Unity Agent Coordination] Coplay MCP was not connected within 60 seconds. " +
                                 "Reconnect the MCP client to discover the optional coordination tools.");
            }
        }
    }
}
