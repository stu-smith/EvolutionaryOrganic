namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>A breeding project: the genomes the user has chosen to keep, and the creation they were last working on.</summary>
public sealed class Project
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>The kept genomes, in the order they were added.</summary>
    public List<Genome> Genomes { get; set; } = [];

    /// <summary>The creation in the centre of the grid when the project was saved, if any.</summary>
    public Genome? Current { get; set; }
}
