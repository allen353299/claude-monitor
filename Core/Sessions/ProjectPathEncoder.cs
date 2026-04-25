namespace ClaudeMonitor.Core.Sessions;

public static class ProjectPathEncoder
{
    public static string Encode(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath)) return "";
        var chars = projectPath.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c is '\\' or '/' or ':') chars[i] = '-';
        }
        return new string(chars);
    }
}
