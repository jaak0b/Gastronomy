using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record ListedTable(Table Table, bool HasItemsToHandOutOrSettleAtTheRunningFestival);
