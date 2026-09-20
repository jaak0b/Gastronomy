using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed record CatalogWrite(IResult Response, bool SomethingChanged);
