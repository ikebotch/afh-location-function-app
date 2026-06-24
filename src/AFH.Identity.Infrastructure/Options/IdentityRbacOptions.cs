namespace AFH.Identity.Infrastructure.Options;

public sealed class IdentityRbacOptions
{
    public IdentityPermissionResolutionMode PermissionMode { get; set; } = IdentityPermissionResolutionMode.Hybrid;
}
