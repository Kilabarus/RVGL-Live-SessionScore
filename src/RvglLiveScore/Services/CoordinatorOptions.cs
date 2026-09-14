namespace RvglLiveScore.Services;

public sealed class CoordinatorOptions
{
    public string SessionsUrl { get; set; } = "https://net.rv.gl/api/sessions";
    public int PollIntervalSeconds { get; set; } = 10;
}
