using System.Reflection;

namespace TennoForge.Core;

public static class ClientIdentity
{
    public static string Version=>typeof(ClientIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??"0.0.0";
    public static string UserAgent=>$"WF-Labs/{Version} (+https://github.com/Revo-32/WF-Labs)";
}
