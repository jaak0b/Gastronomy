using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Api.Hosting;

public sealed class OutstandingInvitationCache : IOutstandingInvitationCache
{
  private readonly Lock _guard = new();

  private OutstandingInvitation? _outstanding;

  public void Remember(OutstandingInvitation invitation)
  {
    lock (_guard)
    {
      _outstanding = invitation;
    }
  }

  public OutstandingInvitation? Read()
  {
    lock (_guard)
    {
      return _outstanding;
    }
  }

  public void ForgetInvitation()
  {
    lock (_guard)
    {
      _outstanding = null;
    }
  }
}
