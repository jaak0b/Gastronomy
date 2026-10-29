# Setting up a device

A waiter's phone and a station's tablet are set up the same way. The admin creates an invitation
for the waiter or the station on the laptop, and the device scans the QR code it shows. The code is
single-use and short-lived, and the device exchanges it for a long-lived device token kept in
`localStorage`. The invitation says whether it belongs to a waiter or a station, and the laptop
tells the device which of the two it is each time it starts.

## Where the code lives

Backend, under `backend/`:

- `GastronomyApp.Core/Entities/Device.cs` and `EnrolmentInvitation.cs`. `StaffMember.DeviceId` and
  `Station.DeviceId` point at the owner's device (`IDeviceOwner`).
- `Core/Services/EnrolmentService.cs` creates an invitation (`CreateInvitationAsync`, which also
  remembers its code in the outstanding invitation cache), redeems it (`RedeemAsync`, which issues the
  device and its token, and signs out the device the browser handed over) and answers the QR address
  (`ReadOpenQRUrlAsync`). `DeviceSessionService.cs` reads the session of the calling device
  (`ReadAsync`) and changes its language (`ChangeLanguageAsync`). `DeviceOwnerLookup.cs` finds the
  owner of a device, of an invitation or of a presented token (`FindByTokenAsync`), and
  `DeviceOwnerRetirement.cs` signs a device out. Refusals: `Core/Refusals/Refusal.Enrolment.cs`,
  `Refusal.EnrolmentRedemption.cs`, `Refusal.Session.cs` and `Refusal.DeviceLanguage.cs`. The
  mapping is `Core/Mapping/EnrolmentMapping.cs`.
- Ports: `ISecretHasher` (implemented by `Infrastructure/Security/SecretHasher.cs`, which hashes
  every token and code with salted SHA-256 and accepts only a stored hash of that algorithm),
  `IDeviceTokenSplitter` (`Infrastructure/Security/DeviceTokenSplitter.cs`),
  `IEnrolmentUrlBuilder` (`Api/Hosting/EnrolmentUrlBuilder.cs`) and
  `IOutstandingInvitationCache`. No repository stands in front of the database for this area.
- Routes: `POST /api/admin/enrolment/invitations` (`AdminEnrolmentEndpoints.cs`), the QR picture at
  `GET /api/admin/enrolment/invitations/{invitationId}/qr.svg` (`AdminInvitationQREndpoints.cs`,
  drawn by `Responders/InvitationQRResponder.cs`), `POST /api/enrolment/redeem`
  (`EnrolmentEndpoints.cs`), `GET /api/session` and `PUT /api/session/language`
  (`SessionEndpoints.cs`) and `GET /api/language` (`LanguageEndpoints.cs`, the language the laptop
  was started with). `Auth/DeviceAuthenticationHandler.cs` reads the token on every request through
  `DeviceOwnerLookup`, and `Values/DeviceCaller.cs`, `StaffDeviceCaller.cs` and
  `StationDeviceCaller.cs` hand the device to an endpoint. Wire records in
  `GastronomyApp.Contracts/Enrolment` and `Contracts/Session`.

Frontend, under `frontend/src/`:

- `admin/components/enrolment/InvitationPanel.vue` and `admin/stores/enrolment.ts` on the laptop.
- `shared/views/EnrolQr.vue` (the address the QR opens), `Welcome.vue` and `StartingUp.vue`;
  `shared/stores/session.ts` keeps the token under `deviceToken`; `shared/core/landing.ts`
  (`screenFor`) chooses the screen. A waiter's and a station's set-up link are the same address,
  `/j/<code>` (`EnrolmentUrlBuilder` on the laptop), and take the same way: `EnrolQr.vue`, then
  `shared/components/QrLanding.vue`, which redeems the code through `session.redeem`.
- The boot script in `frontend/index.html` decides whether a loaded address is shown or sent
  through the back-button trap's doors (see the `back-button` page). It sends a tab through the
  doors only when the load is the start screen's own entry coming back: not a new navigation (the
  type of `performance.getEntriesByType('navigation')`, or `performance.navigation.type` in an
  older browser) and the address `buildBackButtonTrap` stored under `theDoorAnchorAddress`. A tab
  an older build opened stored no such address and goes through the doors on every load that is
  not a new navigation, as before.

## Invariants

- **One device per owner.** A waiter owns at most one phone and a station at most one tablet, and
  the owner points at its device, so it holds at most one device and at most one outstanding
  invitation. Setting a device up again deletes the old row, which signs the old device out at that
  moment; that is what revoking is. Taking a waiter or a station off the list signs its device out
  too.
- **The owner decides the screen.** A station tablet lands on the station screen and stays there; a
  waiter's phone keeps the ordering flow. A device that is set up but has not yet heard back from
  the laptop shows a starting screen until it does. That decision lives in
  `shared/core/landing.ts`, never in a component.
- **A set-up link opened in a tab that already shows the app redeems its own code.** Typed,
  pasted or followed into a tab that shows the welcome screen, a waiter screen, the station screen
  or the admin, the link is a new navigation, so the tab stays on it and `QrLanding` redeems that
  code, never the address the tab was opened with or last returned to. This holds for a waiter's
  and a station's link alike, because both are `/j/<code>`. A phone camera opens a new tab, which
  asks for the start tap first, as before.
- **The trap still holds after such a link.** Back from it goes to the app screen before it, and a
  back press onto the start screen still sends the tab through the doors to where the device was. A
  reload stays on the screen reloaded. When the browser reports no navigation type, a load of the
  start screen's own address goes through the doors and every other address stays.
- How a token is stored is backend hard rule 5.

## Tests

- `GastronomyApp.Infrastructure.Tests/Services`: `EnrolmentServiceTest`, `DeviceSessionServiceTest`,
  `DeviceOwnerLookupTest` and `DeviceOwnerRetirementTest`, on the real DbContext with in-memory
  SQLite; `Security/DeviceTokenSplitterTest` and `SecretHasherTest`; `Configurations/DeviceConfigurationTest`
  and `EnrolmentInvitationConfigurationTest`; `Persistence/ReleasedDatabaseUpgradeTest`, which
  checks that the migrations remove the devices and invitations of the released database and leave
  their waiters and stations without a device.
- `GastronomyApp.Api.Tests`: `Endpoints/AdminEnrolmentEndpointsTest`, `EnrolmentEndpointsTest`,
  `EnrolmentLoggingTest`, `ConcurrentInvitationTest`, `InvitationQREndpointTest`,
  `SessionEndpointsTest`, `LanguageEndpointTest`, `Responders/InvitationQRResponderTest`,
  `Auth/DeviceAuthenticationTest` and `Auth/Filters/DeviceKindTest`. `TestSupport/TestDeviceEnrolment`
  gives a test a device token by creating and redeeming an invitation through `EnrolmentService`.
- `frontend/tests`: `admin/components/enrolment/InvitationPanel.spec.ts`,
  `admin/stores/enrolment.spec.ts`, `admin/core/invitationQrView.spec.ts`,
  `shared/core/landing.spec.ts`, `shared/core/startingUp.spec.ts`, the `shared/stores/session*`
  specs, `shared/router/enrolmentGuard.spec.ts`, `shared/components/QrLanding.spec.ts`,
  `shared/views/Welcome.spec.ts` and `AppStartingUp.spec.ts`; `shared/router/doorPage.boot.spec.ts`
  (the real boot and door scripts on the address loaded: a waiter's, a station's and an admin tab's
  second link, a tab an older build opened, an older browser, a back press onto the start screen
  with and without a navigation type, a link at another address with no navigation type, a reload
  of an app screen and of the order screen, back after a new link), `shared/router/doorPage.spec.ts`
  (the start tap storing its address, the door page) and
  `AppSetUpLinkInAnOpenTab.spec.ts` (the real scripts, then the App redeeming the new code of a
  waiter and of a station tablet), on `tests/support/doorScripts.ts`.
