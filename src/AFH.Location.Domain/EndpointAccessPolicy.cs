namespace AFH.Location.Domain
{
    public enum EndpointAccessPolicy
    {
        Public,
        UserAuthenticated,
        InternalOnly,
        WebhookVerified
    }
}
