using System;
using System.Collections.Generic;
using System.Text;

namespace Devoplus.JobForge.Entities.Nested;

public class Metadata
{
    public MetadataKey MetaKey { get; set; }
    public object Value { get; set; } = default!;
}

public enum MetadataKey
{
    None = 0,

    // Portal Enumerators
    Portal_MicrosoftEntraIDAuthenticationEnabled = 1001,

    // UserAccount Enumerators
    UserAccount_MicrosoftEntraIDAuthenticationEnabled = 2001,
    UserAccount_MicrosoftEntraIDPrincipalName = 2002,
    UserAccount_GoogleAuthenticationEnabled = 2003,
    UserAccount_GoogleAuthenticationEmailAddress = 2004,
    UserAccount_MFA_SMSSetupStatus = 2005,
    UserAccount_MFA_SMSPhoneNumber = 2006,
    UserAccount_MFA_TOTPSetupStatus = 2007,
    UserAccount_MFA_TOTPSecret = 2008,
}
