using FakeItEasy;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class EnrolmentComposition
{
  public EnrolmentComposition(GastronomyAppDbContext dbContext, TimeProvider clock)
  {
    RevocationAnnouncer = A.Fake<IDeviceRevocationAnnouncer>();
    CompletionAnnouncer = A.Fake<IEnrolmentCompletionAnnouncer>();
    Cache = new();

    var afterCommitActions = A.Fake<IAfterCommitActions>();
    A.CallTo(() => afterCommitActions.RunWhenCommittedAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._)).ReturnsLazily(call => ((Func<CancellationToken, Task>)call.Arguments[0]!)(call.GetArgument<CancellationToken>(1)));

    Pbkdf2SecretHasher hasher = new();
    Lookup = new(dbContext, hasher);
    Retirement = new(dbContext, Lookup, RevocationAnnouncer, afterCommitActions, clock);
    Service = new(dbContext, new CoreMappingFactory().Create(), Lookup, Retirement, hasher, new DeviceTokenSplitter(), Cache, new LaptopAddress(), CompletionAnnouncer, afterCommitActions, clock, NullLogger<EnrolmentService>.Instance);
  }

  public IDeviceRevocationAnnouncer RevocationAnnouncer { get; }

  public IEnrolmentCompletionAnnouncer CompletionAnnouncer { get; }

  public InMemoryInvitationCache Cache { get; }

  public DeviceOwnerLookup Lookup { get; }

  public DeviceOwnerRetirement Retirement { get; }

  public EnrolmentService Service { get; }

  public sealed class InMemoryInvitationCache : IOutstandingInvitationCache
  {
    private OutstandingInvitation? _outstanding;

    public int ForgetCount { get; private set; }

    public void Remember(OutstandingInvitation invitation)
    {
      _outstanding = invitation;
    }

    public OutstandingInvitation? Read()
    {
      return _outstanding;
    }

    public void ForgetInvitation()
    {
      ForgetCount++;
      _outstanding = null;
    }
  }

  private sealed class LaptopAddress : IEnrolmentUrlBuilder
  {
    public string BuildEnrolmentUrl(string qrCodeValue)
    {
      return $"http://192.168.0.5:5000/j/{qrCodeValue}";
    }
  }
}
