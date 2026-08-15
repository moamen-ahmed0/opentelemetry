// Bound from appsettings.json, overridable via env vars of the same name, e.g.
//   ObservabilitySettings__ServiceName=simple-service
//   ObservabilitySettings__CollectorUrl=http://localhost:4317
//   ObservabilitySettings__BearerToken=<token>
record ObservabilitySettings
{
    public required string ObservabilitySettings__ServiceName { get; init; }
    public required string ObservabilitySettings__CollectorUrl { get; init; }
    public required string ObservabilitySettings__BearerToken { get; init; }
}
