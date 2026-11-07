namespace HistoryMap.Api.Auth;

public static class AuthPolicies
{
    /// <summary>App role / scope required for write/ingest endpoints.</summary>
    public const string IngestWrite = "IngestWrite";

    public const string IngestWriteRole = "HistoryMap.Ingest.Write";
    public const string AccessAsApplicationScope = "access_as_application";
}
