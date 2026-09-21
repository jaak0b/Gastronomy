using System.Reflection;
using System.Text.Json;
using ErrorOr;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Api.Tests.ErrorHandling;

[TestFixture]
public sealed class RefusalTest
{
  [Test]
  public void EveryRefusal_CarriesAStatusDeclaredInRefusalType()
  {
    IReadOnlyList<int> declaredStatusCodes = typeof(RefusalType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                                .Select(field => (int)field.GetRawConstantValue()!)
                                                                .ToList();

    Assert.Multiple(() =>
                    {
                      foreach (var (name, refusal) in EveryRefusal())
                        Assert.That(declaredStatusCodes, Does.Contain(refusal.NumericType), $"{name} answers a status that RefusalType does not declare.");
                    });
  }

  [Test]
  public void EveryRefusalThatReachesADevice_CarriesAMessageKeyTheGermanCatalogueHolds()
  {
    IReadOnlyCollection<string> germanKeys = ReadGermanMessageKeys();

    Assert.Multiple(() =>
                    {
                      foreach (var (name, refusal) in EveryRefusal())
                      {
                        if (!CarriesAProblemCode(refusal))
                          continue;

                        Assert.That(germanKeys, Does.Contain(refusal.Code), $"{name} names a message key the German catalogue does not hold.");
                      }
                    });
  }

  [Test]
  public void EveryRefusalAnsweredWithoutABody_NamesNoProblemCode()
  {
    Assert.Multiple(() =>
                    {
                      foreach (var (name, refusal) in EveryRefusal())
                      {
                        if (CarriesAProblemCode(refusal))
                          continue;

                        Assert.That(refusal.NumericType, Is.AnyOf(RefusalType.NotFound, RefusalType.Unauthorized), $"{name} carries no problem code, so it may only be answered with a bare status.");
                      }
                    });
  }

  [Test]
  public void EveryRefusal_NamesADeveloperReason()
  {
    Assert.Multiple(() =>
                    {
                      foreach (var (name, refusal) in EveryRefusal())
                        Assert.That(refusal.Description, Is.Not.Empty, $"{name} states no reason a developer could read in the log.");
                    });
  }

  private bool CarriesAProblemCode(Error refusal)
  {
    return refusal.Metadata is { } metadata && metadata.ContainsKey(Refusal.MetadataKeys.ProblemCode);
  }

  private IReadOnlyList<(string Name, Error Refusal)> EveryRefusal()
  {
    List<(string, Error)> refusals = [];

    foreach (var area in typeof(Refusal).GetNestedTypes(BindingFlags.Public).Where(nested => nested.IsClass))
      foreach (var factory in area.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(method => method.ReturnType == typeof(Error)))
        refusals.Add(($"Refusal.{area.Name}.{factory.Name}", (Error)factory.Invoke(null, factory.GetParameters().Select(parameter => DummyFor(parameter.ParameterType)).ToArray())!));

    Assert.That(refusals, Is.Not.Empty, "the reflection found no refusal at all");

    return refusals;
  }

  private object? DummyFor(System.Type parameterType)
  {
    if (parameterType == typeof(Guid) || parameterType == typeof(Guid?))
      return Guid.Parse("11111111-1111-1111-1111-111111111111");

    if (parameterType == typeof(string))
      return "Bratwurst";

    if (parameterType == typeof(int))
      return 3;

    if (parameterType == typeof(double))
      return 900d;

    if (parameterType == typeof(DateTime))
      return DateTime.UnixEpoch;

    if (parameterType == typeof(Error))
      return Refusal.Settlement.NoRunningFestival();

    if (parameterType == typeof(IReadOnlyList<string>))
      return new[] { "Tisch 1" };

    if (parameterType == typeof(IReadOnlyList<Guid>))
      return new[] { Guid.Parse("22222222-2222-2222-2222-222222222222") };

    throw new InvalidOperationException($"This test has no stand-in value for {parameterType}.");
  }

  private IReadOnlyCollection<string> ReadGermanMessageKeys()
  {
    var catalogue = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "frontend", "src", "shared", "i18n", "de.json");

    Assert.That(File.Exists(catalogue), Is.True, $"the German catalogue was expected at {Path.GetFullPath(catalogue)}");

    using var document = JsonDocument.Parse(File.ReadAllText(catalogue));

    List<string> keys = [];
    CollectKeys(document.RootElement, string.Empty, keys);

    return keys;
  }

  private void CollectKeys(JsonElement element, string prefix, List<string> keys)
  {
    if (element.ValueKind != JsonValueKind.Object)
    {
      keys.Add(prefix);

      return;
    }

    foreach (var property in element.EnumerateObject())
      CollectKeys(property.Value, prefix.Length == 0 ? property.Name : prefix + "." + property.Name, keys);
  }
}
