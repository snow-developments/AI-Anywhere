using Anywhere.Agents;
using Xunit;

public class AgentProfileParsingTests {
  [Fact]
  public void ParseArgs_splits_on_commas_and_trims_whitespace() {
    var args = AgentProfileParser.ParseArgs(" --stdio, --verbose ,--port 4000 ");

    Assert.Equal(new[] { "--stdio", "--verbose", "--port 4000" }, args);
  }

  [Fact]
  public void ParseArgs_returns_empty_array_for_blank_input() {
    Assert.Empty(AgentProfileParser.ParseArgs(""));
    Assert.Empty(AgentProfileParser.ParseArgs("   "));
  }

  [Fact]
  public void ParseArgs_skips_empty_entries_from_consecutive_commas() {
    var args = AgentProfileParser.ParseArgs("--stdio,,--verbose");

    Assert.Equal(new[] { "--stdio", "--verbose" }, args);
  }
}
