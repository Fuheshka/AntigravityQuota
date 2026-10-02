using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using Xunit;

namespace AntigravityQuota.Tests;

public class ServerDiscoveryTests
{
    [Theory]
    [InlineData("language_server_windows_x64.exe --csrf_token secret123 --port 1234", "secret123")]
    [InlineData("language_server.exe --csrf_token=abc-456-def", "abc-456-def")]
    [InlineData("C:\\path\\language_server_windows_x64.exe --app_dir C:\\temp --csrf_token \"quoted-token-999\" --parent_pid 42", "quoted-token-999")]
    [InlineData("--csrf_token='single-quoted-token'", "single-quoted-token")]
    [InlineData("language_server.exe --csrf_token=\"token_with_equals=value\"", "token_with_equals=value")]
    public void ExtractCsrfToken_WithValidCommandLine_ReturnsExpectedToken(string commandLine, string expectedToken)
    {
        var token = ServerDiscovery.ExtractCsrfToken(commandLine);
        Assert.Equal(expectedToken, token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("language_server.exe --parent_pid 1234")]
    [InlineData("language_server.exe --csrf_token")]
    [InlineData("language_server_windows_x64.exe --csrf_token token123 multicall")]
    [InlineData("multicall --csrf_token token123")]
    public void ExtractCsrfToken_WithInvalidOrMulticallCommandLine_ReturnsNull(string? commandLine)
    {
        var token = ServerDiscovery.ExtractCsrfToken(commandLine);
        Assert.Null(token);
    }

    [Theory]
    [InlineData("language_server_windows_x64.exe", true)]
    [InlineData("language_server_windows_x64", true)]
    [InlineData("LANGUAGE_SERVER_WINDOWS_X64.EXE", true)]
    [InlineData("language_server.exe", true)]
    [InlineData("language_server", true)]
    [InlineData("LANGUAGE_SERVER", true)]
    [InlineData("Antigravity.exe", false)]
    [InlineData("code.exe", false)]
    [InlineData("other_server.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMatchingProcessName_ValidatesProcessNamesCorrectly(string? processName, bool expectedMatch)
    {
        var isMatch = ServerDiscovery.IsMatchingProcessName(processName);
        Assert.Equal(expectedMatch, isMatch);
    }

    [Fact]
    public void ParseTcpTable_FiltersPidAndListenState_AndSortsDescending()
    {
        // Construct synthetic Win32 MIB_TCPTABLE_OWNER_PID buffer
        // Header: 4 bytes dwNumEntries
        // Each row: 24 bytes (dwState: 4, dwLocalAddr: 4, dwLocalPort: 4, dwRemoteAddr: 4, dwRemotePort: 4, dwOwningPid: 4)
        const int targetPid = 1234;
        const int otherPid = 5678;
        const uint stateListen = 2;
        const uint stateEstablished = 5;

        var entries = new List<(uint state, ushort port, uint pid)>
        {
            (stateListen, 52140, targetPid),       // match: pid 1234, listen, port 52140
            (stateEstablished, 52141, targetPid),  // ignore: not listening
            (stateListen, 52142, otherPid),        // ignore: other pid
            (stateListen, 52150, targetPid),       // match: pid 1234, listen, port 52150
            (stateListen, 52140, targetPid)        // duplicate: should be deduplicated
        };

        byte[] buffer = BuildTcpTableBuffer(entries);

        IReadOnlyList<int> ports = ServerDiscovery.ParseTcpTable(buffer, targetPid);

        Assert.Equal(2, ports.Count);
        Assert.Equal(52150, ports[0]); // sorted descending
        Assert.Equal(52140, ports[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ParseTcpTable_WithTooSmallBuffer_ReturnsEmpty(int length)
    {
        var buffer = new byte[length];
        var ports = ServerDiscovery.ParseTcpTable(buffer, 1234);
        Assert.Empty(ports);
    }

    [Fact]
    public void ParseTcpTable_WithTruncatedRows_ParsesOnlyCompleteRows()
    {
        // Header says 2 entries, but buffer only has 1 complete entry
        var entries = new List<(uint state, ushort port, uint pid)>
        {
            (2, 8080, 4321)
        };
        byte[] fullBuffer = BuildTcpTableBuffer(entries);
        // Modify header dwNumEntries to 5 (more than actual bytes)
        BinaryPrimitives.WriteUInt32LittleEndian(fullBuffer.AsSpan(0, 4), 5);

        var ports = ServerDiscovery.ParseTcpTable(fullBuffer, 4321);
        Assert.Single(ports);
        Assert.Equal(8080, ports[0]);
    }

    [Fact]
    public void DiscoverActiveServer_WhenServerNotRunning_ReturnsNull()
    {
        // When running on macOS or system without language_server running, returns null safely without exception
        var endpoint = ServerDiscovery.DiscoverActiveServer();
        // Since we are running in test environment where Antigravity Windows language_server is not running
        Assert.Null(endpoint);
    }

    [Fact]
    public void DiscoverActiveServer_WithCustomDelegates_ReturnsExpectedEndpoint()
    {
        const int testPid = 7777;
        const string testToken = "mock-csrf-token-123";
        var mockPorts = new List<int> { 55001, 55000 };

        var endpoint = ServerDiscovery.DiscoverActiveServer(
            processFinder: () => new[] { (testPid, "language_server_windows_x64") },
            commandLineGetter: pid => $"language_server_windows_x64.exe --csrf_token {testToken}",
            portsGetter: pid => mockPorts
        );

        Assert.NotNull(endpoint);
        Assert.Equal(testPid, endpoint.Pid);
        Assert.Equal(testToken, endpoint.CsrfToken);
        Assert.Equal(mockPorts, endpoint.Ports);
    }

    [Fact]
    public void DiscoverActiveServer_WhenNoPortsFound_ReturnsNull()
    {
        var endpoint = ServerDiscovery.DiscoverActiveServer(
            processFinder: () => new[] { (1234, "language_server.exe") },
            commandLineGetter: pid => "language_server.exe --csrf_token token123",
            portsGetter: pid => Array.Empty<int>()
        );

        Assert.Null(endpoint);
    }

    [Fact]
    public void DiscoverActiveServer_WhenNoCsrfTokenFound_ReturnsNull()
    {
        var endpoint = ServerDiscovery.DiscoverActiveServer(
            processFinder: () => new[] { (1234, "language_server.exe") },
            commandLineGetter: pid => "language_server.exe --no_token_here",
            portsGetter: pid => new[] { 8080 }
        );

        Assert.Null(endpoint);
    }

    [Fact]
    public void DiscoverActiveServer_WhenProcessDoesNotMatch_ReturnsNull()
    {
        var endpoint = ServerDiscovery.DiscoverActiveServer(
            processFinder: () => new[] { (1234, "unrelated_process.exe") },
            commandLineGetter: pid => "unrelated_process.exe --csrf_token token123",
            portsGetter: pid => new[] { 8080 }
        );

        Assert.Null(endpoint);
    }

    [Fact]
    public void DiscoverActiveServer_SortsUnsortedPortsDescending()
    {
        var endpoint = ServerDiscovery.DiscoverActiveServer(
            processFinder: () => new[] { (1234, "language_server_windows_x64.exe") },
            commandLineGetter: pid => "language_server_windows_x64.exe --csrf_token token123",
            portsGetter: pid => new[] { 50000, 50010, 50005 }
        );

        Assert.NotNull(endpoint);
        Assert.Equal(3, endpoint.Ports.Count);
        Assert.Equal(50010, endpoint.Ports[0]);
        Assert.Equal(50005, endpoint.Ports[1]);
        Assert.Equal(50000, endpoint.Ports[2]);
    }

    private static byte[] BuildTcpTableBuffer(IEnumerable<(uint state, ushort port, uint pid)> rows)
    {
        var rowList = new List<(uint state, ushort port, uint pid)>(rows);
        byte[] buffer = new byte[4 + rowList.Count * 24];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), (uint)rowList.Count);

        for (int i = 0; i < rowList.Count; i++)
        {
            int offset = 4 + i * 24;
            var (state, port, pid) = rowList[i];

            // dwState (offset + 0)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, 4), state);

            // dwLocalAddr (offset + 4) - 127.0.0.1 (0x0100007F)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 4, 4), 0x0100007F);

            // dwLocalPort (offset + 8) - network byte order (big-endian 16-bit in first 2 bytes)
            buffer[offset + 8] = (byte)((port >> 8) & 0xFF);
            buffer[offset + 9] = (byte)(port & 0xFF);
            buffer[offset + 10] = 0;
            buffer[offset + 11] = 0;

            // dwRemoteAddr (offset + 12)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 12, 4), 0);

            // dwRemotePort (offset + 16)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 16, 4), 0);

            // dwOwningPid (offset + 20)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 20, 4), pid);
        }

        return buffer;
    }
}
