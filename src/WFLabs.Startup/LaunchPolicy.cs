namespace WFLabs.Client;

/// <summary>One launch per game-session transition; closing WF Labs does not continuously reopen it.</summary>
public sealed class LaunchPolicy
{
    private bool previousGameRunning;
    public bool ShouldLaunch(bool startAtLogin,bool startWithGame,bool loginEvent,bool gameRunning)
    {
        var launch=startAtLogin&&loginEvent||startWithGame&&gameRunning&&!previousGameRunning;
        previousGameRunning=gameRunning;
        return launch;
    }
}
