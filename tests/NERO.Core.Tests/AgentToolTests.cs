using System.Text.Json;
using NERO.Automation;
using NERO.AI;
using Xunit;

namespace NERO.Core.Tests;

public sealed class AgentToolTests
{
    [Fact]
    public async Task ExecuteAsync_DoesNotRunToolWhenUserDeclines()
    {
        using var arguments = JsonDocument.Parse("{}");
        var tool = new FakeTool();
        var approval = new FakeApproval(approved: false);
        var coordinator = new ToolExecutionCoordinator(new[] { tool }, approval);

        var result = await coordinator.ExecuteAsync("fake_action", arguments.RootElement, "test reason");

        Assert.False(result.Succeeded);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(1, approval.RequestCount);
    }

    [Fact]
    public async Task ExecuteAsync_RunsToolOnlyAfterApproval()
    {
        using var arguments = JsonDocument.Parse("{\"value\":\"hello\"}");
        var tool = new FakeTool();
        var approval = new FakeApproval(approved: true);
        var coordinator = new ToolExecutionCoordinator(new[] { tool }, approval);

        var result = await coordinator.ExecuteAsync("fake_action", arguments.RootElement, "test reason");

        Assert.True(result.Succeeded);
        Assert.Equal("hello", tool.LastValue);
        Assert.Equal(1, tool.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownToolWithoutApproval()
    {
        using var arguments = JsonDocument.Parse("{}");
        var approval = new FakeApproval(approved: true);
        var coordinator = new ToolExecutionCoordinator(Array.Empty<IAgentTool>(), approval);

        var result = await coordinator.ExecuteAsync("powershell", arguments.RootElement, "unsafe");

        Assert.False(result.Succeeded);
        Assert.Equal(0, approval.RequestCount);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    public void OpenWebsiteTool_RejectsNonHttpSchemes(string url)
    {
        using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new { url }));
        var tool = new OpenWebsiteTool();

        Assert.Throws<ArgumentException>(() => tool.BuildApprovalSummary(arguments.RootElement));
    }

    [Fact]
    public void OpenApplicationTool_RejectsApplicationsOutsideAllowList()
    {
        using var arguments = JsonDocument.Parse("{\"application\":\"powershell\"}");
        var tool = new OpenApplicationTool();

        Assert.Throws<ArgumentException>(() => tool.BuildApprovalSummary(arguments.RootElement));
    }

    [Fact]
    public void CreateFolderTool_RejectsPathTraversal()
    {
        using var arguments = JsonDocument.Parse("{\"name\":\"..\\\\outside\",\"location\":\"desktop\"}");
        var tool = new CreateFolderTool();

        Assert.Throws<ArgumentException>(() => tool.BuildApprovalSummary(arguments.RootElement));
    }


    {
        public int RequestCount { get; private set; }

        public Task<bool> ConfirmAsync(string summary, CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return Task.FromResult(approved);
        }
    }

    private sealed class FakeTool : IAgentTool
    {
        public string Name => "fake_action";
        public int ExecutionCount { get; private set; }
        public string? LastValue { get; private set; }

        public string BuildApprovalSummary(JsonElement arguments) => "Approve fake action?";

        public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            LastValue = arguments.TryGetProperty("value", out var value) ? value.GetString() : null;
            return Task.FromResult(new ToolResult(true, "Fake action ran."));
        }
    }
}
