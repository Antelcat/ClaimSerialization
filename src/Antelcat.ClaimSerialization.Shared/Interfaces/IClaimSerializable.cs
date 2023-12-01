using System.Collections.Generic;
using System.Security.Claims;

namespace Antelcat.Interfaces;

public interface IClaimSerializable
{
    void FromClaims(IEnumerable<Claim> claims);
    
    IEnumerable<Claim> GetClaims();
}