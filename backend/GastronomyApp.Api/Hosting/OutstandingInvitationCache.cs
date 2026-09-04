namespace GastronomyApp.Api.Hosting;

public sealed record OutstandingInvitation(Guid InvitationId, string QrCodeValue, string QrUrl, DateTime ExpiresAtUtc);

public sealed class OutstandingInvitationCache
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

  public void Forget()
  {
    lock (_guard)
    {
      _outstanding = null;
    }
  }
}
