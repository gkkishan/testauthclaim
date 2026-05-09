namespace OneAccess.Models;

public class RegisteredApp
{
    public string AppName { get; set; } = string.Empty;
    public string AppIdentifier { get; set; } = string.Empty;
    public string AppURL { get; set; } = string.Empty;
    public string TokenParameterName { get; set; } = string.Empty;
    public List<string> AllowedGroups { get; set; } = [];
    public bool RequiresRoleSelection { get; set; }
    public bool UseUserDomainIdOnly { get; set; }
}
