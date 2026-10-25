namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record CopiedTablesView(IReadOnlyList<Guid> TableIds);
