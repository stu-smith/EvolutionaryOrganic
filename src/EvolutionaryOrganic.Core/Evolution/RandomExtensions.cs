namespace EvolutionaryOrganic.Core.Evolution;

internal static class RandomExtensions
{
    public static float NextFloat(this Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

    /// <summary>A standard normal sample (mean 0, standard deviation 1).</summary>
    public static float NextGaussian(this Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }

    public static bool Chance(this Random rng, double probability) => rng.NextDouble() < probability;

    public static T Pick<T>(this Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    /// <summary>Picks an index with probability proportional to its weight.</summary>
    public static int PickWeighted(this Random rng, params ReadOnlySpan<double> weights)
    {
        double total = 0;
        foreach (var w in weights) total += w;
        double roll = rng.NextDouble() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0) return i;
        }
        return weights.Length - 1;
    }
}
