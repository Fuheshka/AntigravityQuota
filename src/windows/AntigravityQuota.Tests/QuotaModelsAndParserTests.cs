using System;
using System.Collections.Generic;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using Xunit;

namespace AntigravityQuota.Tests;

public class QuotaModelsAndParserTests
{
    [Fact]
    public void QuotaBucket_CalculatesPercentageAndClampsRemainingFraction()
    {
        var reset = DateTimeOffset.Parse("2026-09-27T17:06:50Z");
        var bucket = new QuotaBucket(
            bucketId: "gemini-5h",
            window: "5h",
            remainingFraction: 0.853634,
            resetTime: reset
        );

        Assert.Equal("gemini-5h", bucket.BucketId);
        Assert.Equal("5h", bucket.Window);
        Assert.Equal(0.853634, bucket.RemainingFraction);
        Assert.Equal(reset, bucket.ResetTime);
        Assert.Equal(85.3634, bucket.Percentage, precision: 4);

        // Clamp tests
        var negativeBucket = new QuotaBucket("test-neg", "5h", -0.2, null);
        Assert.Equal(0.0, negativeBucket.Percentage);

        var overBucket = new QuotaBucket("test-over", "5h", 1.5, null);
        Assert.Equal(100.0, overBucket.Percentage);
    }

    [Fact]
    public void QuotaGroup_ExposesBucketsAndGroupName()
    {
        var fiveHour = new QuotaBucket("gemini-5h", "5h", 0.85, null);
        var weekly = new QuotaBucket("gemini-weekly", "weekly", 0.95, null);
        var group = new QuotaGroup(
            displayName: "Gemini Models",
            description: "Flash and Pro",
            buckets: new[] { weekly, fiveHour }
        );

        Assert.Equal("Gemini Models", group.DisplayName);
        Assert.Equal("Flash and Pro", group.Description);
        Assert.Equal("gemini", group.GroupName);
        Assert.Equal(fiveHour, group.FiveHourBucket);
        Assert.Equal(weekly, group.WeeklyBucket);
        Assert.Equal(2, group.Buckets.Count);

        var claudeGroup = new QuotaGroup("Claude and GPT models", "3p models", Array.Empty<QuotaBucket>());
        Assert.Equal("claude", claudeGroup.GroupName);
    }

    [Fact]
    public void ModelConfig_InitializesCorrectlyWithModelIdAndPercentage()
    {
        var reset = DateTimeOffset.Parse("2026-09-27T17:26:05Z");
        var model = new ModelConfig(
            modelId: "gemini-3.8-flash",
            label: "Gemini 3.8 Flash (High)",
            remainingFraction: 0.9062434,
            resetTime: reset
        );

        Assert.Equal("gemini-3.8-flash", model.ModelId);
        Assert.Equal("Gemini 3.8 Flash (High)", model.Label);
        Assert.Equal(0.9062434, model.RemainingFraction);
        Assert.Equal(reset, model.ResetTime);
        Assert.Equal(90.62434, model.Percentage, precision: 4);
    }

    [Fact]
    public void QuotaSnapshot_ExposesGroupsModelsAndTitle()
    {
        var gBucket = new QuotaBucket("gemini-5h", "5h", 0.853634, null);
        var cBucket = new QuotaBucket("3p-5h", "5h", 1.0, null);

        var gGroup = new QuotaGroup("Gemini Models", "", new[] { gBucket });
        var cGroup = new QuotaGroup("Claude and GPT models", "", new[] { cBucket });

        var now = DateTimeOffset.UtcNow;
        var snapshot = new QuotaSnapshot(
            groups: new[] { gGroup, cGroup },
            models: Array.Empty<ModelConfig>(),
            updatedAt: now
        );

        Assert.Equal(gGroup, snapshot.GeminiGroup);
        Assert.Equal(cGroup, snapshot.ClaudeGroup);
        Assert.Equal("85% · 100%", snapshot.MenuBarTitle);
        Assert.Equal(now, snapshot.UpdatedAt);
    }

    [Fact]
    public void ServerEndpoint_InitializesWithPidCsrfTokenAndPorts()
    {
        var ports = new[] { 63660, 63659 };
        var endpoint = new ServerEndpoint(49291, "test-csrf-token", ports);

        Assert.Equal(49291, endpoint.Pid);
        Assert.Equal("test-csrf-token", endpoint.CsrfToken);
        Assert.Equal(ports, endpoint.Ports);
    }

    [Fact]
    public void QuotaParser_ParseSummary_ParsesFullResponsePayload()
    {
        var json = """
        {
            "response": {
                "groups": [
                    {
                        "displayName": "Gemini Models",
                        "description": "Models within this group: Gemini Flash, Gemini Pro",
                        "buckets": [
                            {
                                "bucketId": "gemini-weekly",
                                "displayName": "Weekly Limit Remaining",
                                "window": "weekly",
                                "remainingFraction": 0.95666826,
                                "resetTime": "2026-09-30T12:11:22Z"
                            },
                            {
                                "bucketId": "gemini-5h",
                                "displayName": "Five Hour Limit Remaining",
                                "window": "5h",
                                "remainingFraction": 0.853634,
                                "resetTime": "2026-09-27T17:06:50Z"
                            }
                        ]
                    },
                    {
                        "displayName": "Claude and GPT models",
                        "description": "Models within this group: Claude Opus, Claude Sonnet, GPT-OSS",
                        "buckets": [
                            {
                                "bucketId": "3p-weekly",
                                "displayName": "Weekly Limit Remaining",
                                "window": "weekly",
                                "remainingFraction": 0.9999768,
                                "resetTime": "2026-09-30T12:11:33Z"
                            },
                            {
                                "bucketId": "3p-5h",
                                "displayName": "Five Hour Limit Remaining",
                                "window": "5h",
                                "remainingFraction": 1.0,
                                "resetTime": "2026-09-27T17:39:56Z"
                            }
                        ]
                    }
                ]
            }
        }
        """;

        var groups = QuotaParser.ParseSummary(json);

        Assert.Equal(2, groups.Count);

        var gemini = groups[0];
        Assert.Equal("Gemini Models", gemini.DisplayName);
        Assert.Equal("gemini", gemini.GroupName);
        Assert.NotNull(gemini.FiveHourBucket);
        Assert.Equal("gemini-5h", gemini.FiveHourBucket!.BucketId);
        Assert.Equal(0.853634, gemini.FiveHourBucket.RemainingFraction);
        Assert.Equal(DateTimeOffset.Parse("2026-09-27T17:06:50Z"), gemini.FiveHourBucket.ResetTime);
        Assert.NotNull(gemini.WeeklyBucket);
        Assert.Equal("gemini-weekly", gemini.WeeklyBucket!.BucketId);

        var claude = groups[1];
        Assert.Equal("Claude and GPT models", claude.DisplayName);
        Assert.Equal("claude", claude.GroupName);
        Assert.NotNull(claude.FiveHourBucket);
        Assert.Equal("3p-5h", claude.FiveHourBucket!.BucketId);
        Assert.Equal(1.0, claude.FiveHourBucket.RemainingFraction);
    }

    [Fact]
    public void QuotaParser_ParseSummary_HandlesRootGroupsWithoutResponseEnvelope()
    {
        var json = """
        {
            "groups": [
                {
                    "displayName": "Gemini Models",
                    "buckets": [
                        {
                            "bucketId": "gemini-5h",
                            "window": "5h",
                            "remainingFraction": 0.5,
                            "resetTime": "2026-10-02T18:00:00.123456Z"
                        }
                    ]
                }
            ]
        }
        """;

        var groups = QuotaParser.ParseSummary(json);

        Assert.Single(groups);
        Assert.Equal("Gemini Models", groups[0].DisplayName);
        Assert.Equal(0.5, groups[0].FiveHourBucket?.RemainingFraction);
        Assert.Equal(DateTimeOffset.Parse("2026-10-02T18:00:00.123456Z"), groups[0].FiveHourBucket?.ResetTime);
    }

    [Fact]
    public void QuotaParser_ParseCascadeModelConfigs_ParsesFlatPayload()
    {
        var json = """
        {
            "clientModelConfigs": [
                {
                    "label": "Gemini 3.8 Flash (High)",
                    "quotaInfo": {
                        "remainingFraction": 0.9062434,
                        "resetTime": "2026-09-27T17:06:50Z"
                    }
                },
                {
                    "modelId": "claude-opus-4-6",
                    "label": "Claude Opus 4.6 (Thinking)",
                    "quotaInfo": {
                        "remainingFraction": 1.0,
                        "resetTime": "2026-09-27T17:26:05Z"
                    }
                }
            ]
        }
        """;

        var models = QuotaParser.ParseCascadeModelConfigs(json);

        Assert.Equal(2, models.Count);
        Assert.Equal("Gemini 3.8 Flash (High)", models[0].Label);
        Assert.Equal("Gemini 3.8 Flash (High)", models[0].ModelId); // fallback to label if modelId absent
        Assert.Equal(90.62434, models[0].Percentage, precision: 4);
        Assert.Equal(DateTimeOffset.Parse("2026-09-27T17:06:50Z"), models[0].ResetTime);

        Assert.Equal("claude-opus-4-6", models[1].ModelId);
        Assert.Equal("Claude Opus 4.6 (Thinking)", models[1].Label);
        Assert.Equal(100.0, models[1].Percentage);
    }

    [Fact]
    public void QuotaParser_ParseCascadeModelConfigs_ParsesNestedUserStatusPayload()
    {
        var json = """
        {
            "userStatus": {
                "cascadeModelConfigData": {
                    "clientModelConfigs": [
                        {
                            "modelId": "gemini-pro",
                            "label": "Gemini 3.8 Pro",
                            "quotaInfo": {
                                "remainingFraction": 0.42,
                                "resetTime": "2026-10-01T12:00:00Z"
                            }
                        }
                    ]
                }
            }
        }
        """;

        var models = QuotaParser.ParseCascadeModelConfigs(json);

        Assert.Single(models);
        Assert.Equal("gemini-pro", models[0].ModelId);
        Assert.Equal("Gemini 3.8 Pro", models[0].Label);
        Assert.Equal(42.0, models[0].Percentage);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:00:00Z"), models[0].ResetTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ invalid json }")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"other\": 123}")]
    [InlineData("{\"groups\": \"not-an-array\"}")]
    public void QuotaParser_ParseSummary_SafelyHandlesCorruptedAndInvalidPayloads(string? badJson)
    {
        var groups = QuotaParser.ParseSummary(badJson);
        Assert.NotNull(groups);
        Assert.Empty(groups);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"bad\": true}")]
    [InlineData("{ clientModelConfigs: corrupt }")]
    [InlineData("{\"clientModelConfigs\": \"invalid\"}")]
    public void QuotaParser_ParseCascadeModelConfigs_SafelyHandlesCorruptedPayloads(string? badJson)
    {
        var models = QuotaParser.ParseCascadeModelConfigs(badJson);
        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Fact]
    public void QuotaParser_SafelyHandlesMissingAndMalformedBucketFields()
    {
        var json = """
        {
            "groups": [
                {
                    "displayName": null,
                    "description": null,
                    "buckets": [
                        {
                            "bucketId": null,
                            "window": null,
                            "remainingFraction": "invalid-number",
                            "resetTime": "not-a-valid-date"
                        },
                        {
                            "remainingFraction": null
                        }
                    ]
                }
            ]
        }
        """;

        var groups = QuotaParser.ParseSummary(json);
        Assert.Single(groups);
        Assert.Equal(string.Empty, groups[0].DisplayName);
        Assert.Equal(2, groups[0].Buckets.Count);
        Assert.Equal(0.0, groups[0].Buckets[0].RemainingFraction);
        Assert.Null(groups[0].Buckets[0].ResetTime);
        Assert.Equal(0.0, groups[0].Buckets[1].RemainingFraction);
    }

    [Fact]
    public void QuotaParser_AliasesAndByteOverloads_WorkEqually()
    {
        var summaryJson = "{\"groups\":[{\"displayName\":\"Gemini\",\"buckets\":[{\"bucketId\":\"g-5h\",\"window\":\"5h\",\"remainingFraction\":0.7}]}]}";
        var summaryBytes = System.Text.Encoding.UTF8.GetBytes(summaryJson);

        var fromAlias = QuotaParser.ParseRetrieveUserQuotaSummary(summaryJson);
        var fromBytes = QuotaParser.ParseSummary(summaryBytes.AsMemory());

        Assert.Single(fromAlias);
        Assert.Single(fromBytes);
        Assert.Equal(0.7, fromAlias[0].FiveHourBucket?.RemainingFraction);
        Assert.Equal(0.7, fromBytes[0].FiveHourBucket?.RemainingFraction);

        var modelJson = "{\"clientModelConfigs\":[{\"label\":\"TestModel\",\"quotaInfo\":{\"remainingFraction\":0.8}}]}";
        var modelBytes = System.Text.Encoding.UTF8.GetBytes(modelJson);

        var modelFromAlias = QuotaParser.ParseCascadeModelConfigData(modelJson);
        var modelFromBytes = QuotaParser.ParseCascadeModelConfigs(modelBytes.AsMemory());

        Assert.Single(modelFromAlias);
        Assert.Single(modelFromBytes);
        Assert.Equal("TestModel", modelFromAlias[0].Label);
        Assert.Equal(80.0, modelFromBytes[0].Percentage);
    }

    [Fact]
    public void ModelConstructors_SupportOverloadsAndDefaults()
    {
        var bucket = new QuotaBucket(remainingFraction: 0.5);
        Assert.Equal(50.0, bucket.Percentage);
        Assert.Null(bucket.ResetTime);
        Assert.Equal(string.Empty, bucket.BucketId);

        var group = new QuotaGroup("gemini", new[] { bucket });
        Assert.Equal("gemini", group.DisplayName);
        Assert.Equal("gemini", group.GroupName);

        var model = new ModelConfig("Claude 3.5 Sonnet", 0.9);
        Assert.Equal("Claude 3.5 Sonnet", model.ModelId);
        Assert.Equal("Claude 3.5 Sonnet", model.Label);
        Assert.Equal(90.0, model.Percentage);

        var snapshot = new QuotaSnapshot(new[] { group }, new[] { model });
        Assert.True((DateTimeOffset.UtcNow - snapshot.UpdatedAt).TotalSeconds < 5);
    }
}

