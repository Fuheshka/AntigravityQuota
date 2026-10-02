using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core;

public static class ServerDiscovery
{
    private static readonly Regex CsrfTokenRegex = new(
        @"(?i)--csrf_token(?:=|\s+)(?:[""']?)([^\s""']+)[""']?",
        RegexOptions.Compiled);

    private const string IphlpapiDll = "iphlpapi.dll";
    private const uint AfInet = 2; // AF_INET (IPv4)
    private const int TcpTableOwnerPidAll = 5;
    private const uint MibTcpStateListen = 2;

    [DllImport(IphlpapiDll, SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int pdwSize,
        bool bOrder,
        uint ulAf,
        int tableClass,
        uint reserved);

    /// <summary>
    /// Checks whether the given process name matches language_server_windows_x64 or language_server.
    /// Case-insensitive, supports optional .exe extension.
    /// </summary>
    public static bool IsMatchingProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        string name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name.Equals("language_server_windows_x64", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("language_server", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the --csrf_token argument value from a process command line.
    /// Returns null if the token is missing or if the command line represents a multicall worker.
    /// </summary>
    public static string? ExtractCsrfToken(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        // Avoid multicall workers, similar to macOS discovery
        if (commandLine.Contains("multicall", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = CsrfTokenRegex.Match(commandLine);
        if (match.Success && match.Groups.Count > 1)
        {
            string token = match.Groups[1].Value.Trim().Trim('\'', '"');
            return string.IsNullOrEmpty(token) ? null : token;
        }

        return null;
    }

    /// <summary>
    /// Parses a Win32 MIB_TCPTABLE_OWNER_PID byte buffer, extracting all listening ports for the target PID.
    /// Ports are returned sorted descending (highest port first, which is the plain HTTP Connect-RPC endpoint).
    /// </summary>
    public static IReadOnlyList<int> ParseTcpTable(ReadOnlySpan<byte> tableBuffer, int targetPid)
    {
        if (tableBuffer.Length < 4)
        {
            return Array.Empty<int>();
        }

        uint numEntries = BinaryPrimitives.ReadUInt32LittleEndian(tableBuffer[..4]);
        const int rowSize = 24; // MIB_TCPROW_OWNER_PID is 24 bytes
        int availableRows = (tableBuffer.Length - 4) / rowSize;
        int rowsToRead = (int)Math.Min(numEntries, (uint)Math.Max(0, availableRows));

        var ports = new HashSet<int>();

        for (int i = 0; i < rowsToRead; i++)
        {
            int offset = 4 + i * rowSize;
            var row = tableBuffer.Slice(offset, rowSize);

            uint state = BinaryPrimitives.ReadUInt32LittleEndian(row[..4]);
            uint pid = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(20, 4));

            if (pid == targetPid && state == MibTcpStateListen)
            {
                // LocalPort in MIB_TCPROW_OWNER_PID is stored in network byte order (big-endian)
                // in the lower 16 bits of dwLocalPort (offset 8 and 9)
                int port = (row[8] << 8) | row[9];
                if (port > 0)
                {
                    ports.Add(port);
                }
            }
        }

        return ports.OrderByDescending(p => p).ToArray();
    }

    /// <summary>
    /// Queries the command line of a process using WMI (Win32_Process.CommandLine).
    /// Protected against WMI errors and access denied exceptions.
    /// </summary>
    public static string? GetProcessCommandLineWmi(int pid)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return GetProcessCommandLineWmiInternal(pid);
    }

    [SupportedOSPlatform("windows")]
    private static string? GetProcessCommandLineWmiInternal(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                var cmd = obj["CommandLine"]?.ToString();
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    return cmd;
                }
            }
        }
        catch
        {
            // Fail gracefully on WMI errors or security restrictions
        }

        return null;
    }

    /// <summary>
    /// Discovers listening TCP ports for the specified process on Windows via GetExtendedTcpTable.
    /// </summary>
    public static IReadOnlyList<int> GetListeningPortsWin32(int pid)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<int>();
        }

        int bufferSize = 0;
        uint ret = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AfInet, TcpTableOwnerPidAll, 0);
        if (bufferSize <= 0)
        {
            return Array.Empty<int>();
        }

        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            ret = GetExtendedTcpTable(buffer, ref bufferSize, false, AfInet, TcpTableOwnerPidAll, 0);
            if (ret == 0) // NO_ERROR
            {
                byte[] raw = new byte[bufferSize];
                Marshal.Copy(buffer, raw, 0, bufferSize);
                return ParseTcpTable(raw, pid);
            }
        }
        catch
        {
            // Fail gracefully
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return Array.Empty<int>();
    }

    /// <summary>
    /// Discovers the active Google Antigravity language_server process on Windows.
    /// Returns a valid ServerEndpoint or null if Antigravity is not running.
    /// </summary>
    public static ServerEndpoint? DiscoverActiveServer()
    {
        return DiscoverActiveServer(
            processFinder: FindDefaultProcesses,
            commandLineGetter: GetProcessCommandLineWmi,
            portsGetter: GetListeningPortsWin32);
    }

    /// <summary>
    /// Discovers the active server using provided or default providers (enables unit testing).
    /// </summary>
    public static ServerEndpoint? DiscoverActiveServer(
        Func<IEnumerable<(int Pid, string ProcessName)>>? processFinder = null,
        Func<int, string?>? commandLineGetter = null,
        Func<int, IReadOnlyList<int>>? portsGetter = null)
    {
        var finder = processFinder ?? FindDefaultProcesses;
        var cmdGetter = commandLineGetter ?? GetProcessCommandLineWmi;
        var portsResolver = portsGetter ?? GetListeningPortsWin32;

        try
        {
            foreach (var (pid, processName) in finder())
            {
                if (!IsMatchingProcessName(processName))
                {
                    continue;
                }

                string? commandLine = cmdGetter(pid);
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    continue;
                }

                string? csrfToken = ExtractCsrfToken(commandLine);
                if (string.IsNullOrWhiteSpace(csrfToken))
                {
                    continue;
                }

                var ports = portsResolver(pid);
                if (ports == null || ports.Count == 0)
                {
                    continue;
                }

                // Ensure sorted descending
                var sortedPorts = ports.OrderByDescending(p => p).Distinct().ToArray();
                return new ServerEndpoint(pid, csrfToken, sortedPorts);
            }
        }
        catch
        {
            // Top-level safety guard
        }

        return null;
    }

    private static IEnumerable<(int Pid, string ProcessName)> FindDefaultProcesses()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Enumerable.Empty<(int, string)>();
        }

        var list = new List<(int, string)>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (IsMatchingProcessName(proc.ProcessName))
                    {
                        list.Add((proc.Id, proc.ProcessName));
                    }
                }
                catch
                {
                    // Ignore processes we can't inspect
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch
        {
            // Ignore system enumeration failures
        }

        return list;
    }
}
