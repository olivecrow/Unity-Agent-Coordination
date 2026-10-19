namespace Olivecrow.UnityAgentCoordination.Editor
{
    public static class UnityAgentCoordinationCommandRouter
    {
        public static CoordinationCommandResult ExecuteJson(string json)
            => CoordinationProtocol.ExecuteJson(json, AgentCoordinationEditorContext.Service,
                () => AgentCoordinationEditorContext.IsReady);

        public static CoordinationCommandResult Execute(CoordinationCommand command)
            => CoordinationProtocol.Execute(command, AgentCoordinationEditorContext.Service,
                () => AgentCoordinationEditorContext.IsReady);
    }
}
