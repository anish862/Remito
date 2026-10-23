namespace Remito.Application.Engagement;

public static class AdFrequencyCap
{
    public static bool HasReached(int impressionCount, int dailyCap) =>
        impressionCount >= dailyCap;
}
