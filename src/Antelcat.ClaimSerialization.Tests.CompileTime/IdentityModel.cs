using System.Collections.Generic;

namespace Antelcat.ClaimSerialization.Tests.CompileTime;

public class IdentityModel
{
    public string? Name { get; set; } = nameof(Name);

    public int Id { get; set; } = 123456;

    public ISet<string> Roles = new SortedSet<string>()
    {
        "Admin",
        "User",
        "Guest"
    };
    
    public object? Additional { get; set; } = new
    {
        Key = "Value"
    };
}