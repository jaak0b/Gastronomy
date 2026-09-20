using GastronomyApp.Infrastructure.Configurations;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

public sealed class EnrolmentInvitationConfigurationTest
{
  [Test]
  public void Configure_NullBuilder_ThrowsArgumentNullException()
  {
    EnrolmentInvitationConfiguration configuration = new();

    Assert.That(() => configuration.Configure(null!), Throws.ArgumentNullException);
  }
}
