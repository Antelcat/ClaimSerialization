using System.Collections.Generic;
using System.Security.Claims;

namespace Antelcat.ClaimSerialization.Interfaces;

public interface IClaimSerializable
{
    void FromClaims(IEnumerable<Claim> claims);
    
    IEnumerable<Claim> GetClaims();
}