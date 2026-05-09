namespace Shared.Models;

public class OneAccessToken
{
    public string AccessToken { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> Groups { get; set; } = [];
    public string ContextName { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
}
