using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface IOutstandingInvitationCache
{
  public void Remember(OutstandingInvitation invitation);

  public OutstandingInvitation? Read();

  public void ForgetInvitation();
}
