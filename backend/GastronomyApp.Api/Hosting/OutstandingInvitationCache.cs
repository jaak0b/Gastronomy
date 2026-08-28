namespace GastronomyApp.Api.Hosting;

public sealed record OutstandingInvitation(Guid InvitationId, string QrCodeValue, string QrUrl, DateTime ExpiresAtUtc);

public sealed class OutstandingInvitationCache
{
  private readonly Lock guard = new();

  private OutstandingInvitation? outstanding;

  public void Remember(OutstandingInvitation invitation)
  {
    lock (guard)
    {
      outstanding = invitation;
    }
  }

  public OutstandingInvitation? Read()
  {
    lock (guard)
    {
      return outstanding;
    }
  }

  public void Forget()
  {
    lock (guard)
    {
      outstanding = null;
    }
  }
}
