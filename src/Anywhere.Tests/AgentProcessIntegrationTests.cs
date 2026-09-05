using System.Collections.Generic;
using System.Threading.Tasks;
using Anywhere.Agents;
using Anywhere.Models;
using Xunit;

public class AgentProcessIntegrationTests {
  private static AgentProfile NewFakeProfile() {
    // The fake agent sits next to this test file under the project directory.
    // Computing the path relative to `AppContext.BaseDirectory` keeps it valid
    // no matter what working directory `dotnet test` sets (it usually runs
    // tests from the bin/ output folder, not the repo root).
    var fakeAgentPath = System.IO.Path.Combine(
      AppContext.BaseDirectory,
      "FakeAgent", "fake_agent.py");
    if (!System.IO.File.Exists(fakeAgentPath)) {
      // Fall back to a source-relative path for runs that copy the file in
      // alongside the binary (e.g. `dotnet test` with `CopyToOutputDirectory`).
      var sourcePath = System.IO.Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "FakeAgent", "fake_agent.py");
      if (System.IO.File.Exists(sourcePath)) {
        fakeAgentPath = System.IO.Path.GetFullPath(sourcePath);
      }
    }
    return new AgentProfile {
      Name = "Fake",
      Command = "python",
      Args = new[] { fakeAgentPath },
      Env = new System.Collections.Generic.Dictionary<string, string>(),
      WorkingDir = System.IO.Directory.GetCurrentDirectory(),
    };
  }

  [Fact(Timeout = 30000)]
  public async Task SendPromptAsync_returns_the_fake_agents_response() {
    var profile = NewFakeProfile();

    using var process = new AgentProcess(profile);
    await process.StartAsync();

    var result = await process.SendPromptAsync("hello");

    Assert.Equal("fake agent response", result.Content);
  }

  [Fact(Timeout = 30000)]
  public async Task SendPromptAsync_raises_OnResponseChunk_before_completing() {
    var profile = NewFakeProfile();

    using var process = new AgentProcess(profile);
    await process.StartAsync();

    var chunks = new List<string>();
    process.OnResponseChunk += chunks.Add;

    await process.SendPromptAsync("hello");

    Assert.Contains("fake agent ", chunks);
  }
}
