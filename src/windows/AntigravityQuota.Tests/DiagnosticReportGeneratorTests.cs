using System;
using System.Collections.Generic;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using Xunit;

namespace AntigravityQuota.Tests;

public class DiagnosticReportGeneratorTests
{
    [Theory]
    [InlineData(null, "None")]
    [InlineData("", "None")]
    [InlineData("   ", "None")]
    [InlineData("short", "***")]
    [InlineData("12345678", "***")]
    [InlineData("123456789", "1234...6789")]
    [InlineData("csrf_super_secret_token_123456", "csrf...3456")]
    public void MaskCsrfToken_MasksCorrectly(string? token, string expected)
    {
        var result = DiagnosticReportGenerator.MaskCsrfToken(token);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateReport_WhenEndpointProvided_ContainsPidPortsAndMaskedToken()
    {
        var endpoint = new ServerEndpoint(4242, "secret_csrf_token_value_999", new[] { 54321, 54320 });
        var now = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero);
        var lastSync = new DateTimeOffset(2026, 10, 3, 0, 58, 30, TimeSpan.Zero);

        var report = DiagnosticReportGenerator.GenerateReport(
            endpoint: endpoint,
            snapshot: null,
            appVersion: "1.0.0",
            osDescription: "Microsoft Windows 11 Pro",
            lastUpdate: lastSync,
            timestamp: now);

        Assert.Contains("=== AntigravityQuota Diagnostic Report ===", report);
        Assert.Contains("App Version: 1.0.0", report);
        Assert.Contains("Platform: .NET 9.0 (Windows x64)", report);
        Assert.Contains("Timestamp: 2026-10-03 01:00:00 UTC", report);
        Assert.Contains("Status: Connected", report);
        Assert.Contains("PID: 4242", report);
        Assert.Contains("Listening Ports: 54321, 54320", report);
        Assert.Contains("CSRF Token: secr..._999", report);
        Assert.Contains("Last Quota Sync: 2026-10-03 00:58:30 UTC", report);
        Assert.Contains("OS: Microsoft Windows 11 Pro", report);
    }

    [Fact]
    public void GenerateReport_WhenDisconnected_ShowsNotDetected()
    {
        var now = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero);

        var report = DiagnosticReportGenerator.GenerateReport(
            endpoint: null,
            snapshot: null,
            appVersion: "1.0.0",
            osDescription: "Microsoft Windows 11 Pro",
            lastUpdate: null,
            timestamp: now);

        Assert.Contains("Status: Disconnected", report);
        Assert.Contains("PID: Not detected", report);
        Assert.Contains("Listening Ports: None", report);
        Assert.Contains("CSRF Token: None", report);
        Assert.Contains("Last Quota Sync: Never", report);
        Assert.Contains("Snapshot: No data available", report);
    }

    [Fact]
    public void GenerateReport_WithSnapshotData_FormatsQuotaDetails()
    {
        var now = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero);
        var bucket5h = new QuotaBucket("gemini-5h", "5h", 0.75, now.AddHours(2));
        var bucketWeek = new QuotaBucket("gemini-week", "weekly", 0.90, now.AddDays(3));
        var group = new QuotaGroup("Gemini", "gemini-models", new[] { bucket5h, bucketWeek });

        var models = new[]
        {
            new ModelConfig("gemini-2.5-pro", "Gemini 2.5 Pro", 0.60, now.AddHours(3)),
            new ModelConfig("gemini-2.5-flash", "Gemini 2.5 Flash", 0.85, now.AddHours(1))
        };

        var snapshot = new QuotaSnapshot(new[] { group }, models, now);

        var report = DiagnosticReportGenerator.GenerateReport(
            endpoint: new ServerEndpoint(1001, "csrf_1234567890", new[] { 8080 }),
            snapshot: snapshot,
            appVersion: "1.0.0",
            osDescription: "Windows 11",
            lastUpdate: now,
            timestamp: now);

        Assert.Contains("Group: Gemini", report);
        Assert.Contains("5h: 75.0% available", report);
        Assert.Contains("weekly: 90.0% available", report);
        Assert.Contains("Model: Gemini 2.5 Pro (gemini-2.5-pro) - 60.0% available", report);
        Assert.Contains("Model: Gemini 2.5 Flash (gemini-2.5-flash) - 85.0% available", report);
    }
}
