using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Antelcat.ClaimSerialization.ComponentModel;

namespace Antelcat.ClaimSerialization.Tests.Runtime;

public class IdentityModel
{
   
    public  string? Name { get; set; } = nameof(Name);

    public  int Id { get; set; } = 123456;

    [ClaimType(ClaimTypes.Role)]
    public ISet<Role> Roles { get; set; }
    
    public object? Additional { get; set; } = new
    {
        Key = "Value"
    };

    public enum Role
    {
        Admin,
        User,
        Guest
    }
}

[JsonSerializable(typeof(IdentityModel))]
public partial class JsonContext : JsonSerializerContext
{
    
}