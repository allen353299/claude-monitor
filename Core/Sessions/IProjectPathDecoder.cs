namespace ClaudeMonitor.Core.Sessions;

public interface IProjectPathDecoder
{
    string ExtractProjectName(JsonlPath jsonlPath);
    ProjectPath Decode(JsonlPath jsonlPath);
}
