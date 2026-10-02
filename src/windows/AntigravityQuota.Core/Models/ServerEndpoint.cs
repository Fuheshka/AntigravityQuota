using System;
using System.Collections.Generic;

namespace AntigravityQuota.Core.Models;

public record ServerEndpoint
{
    public int Pid { get; init; }
    public string CsrfToken { get; init; }
    public IReadOnlyList<int> Ports { get; init; }

    public ServerEndpoint(int pid, string csrfToken, IReadOnlyList<int> ports)
    {
        Pid = pid;
        CsrfToken = csrfToken ?? string.Empty;
        Ports = ports ?? Array.Empty<int>();
    }
}
