namespace GastronomyApp.Contracts.TablePlan;

public sealed record TablePlanView(IReadOnlyList<TablePlanAreaView> Areas);
