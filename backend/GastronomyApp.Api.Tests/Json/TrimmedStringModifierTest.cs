using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GastronomyApp.Api.Json;

namespace GastronomyApp.Api.Tests.Json;

[TestFixture]
public sealed class TrimmedStringModifierTest
{
  private readonly JsonSerializerOptions _options = new()
                                                    {
                                                      PropertyNameCaseInsensitive = true,
                                                      TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(new TrimmedStringModifier().Trim)
                                                    };

  [Test]
  public void Trim_TextWithSpacesAroundIt_RemovesThemAndKeepsTheSpacesInside()
  {
    var read = JsonSerializer.Deserialize<Outer>("""{"name":"  Grosse  Portion ","inner":{"label":" Salz "},"lines":[{"label":" a "},{"label":"b c  "}],"missing":null,"count":3}""", _options);

    Assert.Multiple(() =>
                    {
                      Assert.That(read!.Name, Is.EqualTo("Grosse  Portion"));
                      Assert.That(read.Inner!.Label, Is.EqualTo("Salz"));
                      Assert.That(read.Lines!.Select(line => line.Label),
                                  Is.EqualTo(new[]
                                             {
                                               "a",
                                               "b c"
                                             }));
                      Assert.That(read.Missing, Is.Null);
                      Assert.That(read.Count, Is.EqualTo(3));
                    });
  }

  [Test]
  public void Trim_TextOfOnlySpaces_BecomesEmpty()
  {
    var read = JsonSerializer.Deserialize<Outer>("""{"name":"   "}""", _options);

    Assert.That(read!.Name, Is.Empty);
  }

  private sealed record Outer
  {
    public string? Name { get; init; }

    public Inner? Inner { get; init; }

    public List<Inner>? Lines { get; init; }

    public string? Missing { get; init; }

    public int Count { get; init; }
  }

  private sealed record Inner
  {
    public string? Label { get; init; }
  }
}
