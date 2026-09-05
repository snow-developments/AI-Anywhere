using Anywhere.Controls;
using Xunit;

public class MarkdownLabelTests {
  [Fact]
  public void Setting_markdown_text_does_not_throw() {
    using var label = new MarkdownLabel();
    label.Text = "**bold** and _italic_ and a [link](https://example.com)";
    Assert.Equal("**bold** and _italic_ and a [link](https://example.com)", label.Text);
  }
}
