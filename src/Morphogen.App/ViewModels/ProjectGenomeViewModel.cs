using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Morphogen.Core.Development;
using Morphogen.Core.Genetics;

namespace Morphogen.App.ViewModels;

/// <summary>A genome kept in the project, shown in the project list down the left of the window.</summary>
public sealed partial class ProjectGenomeViewModel : ObservableObject
{
    public ProjectGenomeViewModel(Genome genome, ICommand combineCommand, ICommand selectCommand, ICommand removeCommand)
    {
        Genome = genome;
        CombineCommand = combineCommand;
        SelectCommand = selectCommand;
        RemoveCommand = removeCommand;
    }

    public Genome Genome { get; }

    public ICommand CombineCommand { get; }

    public ICommand SelectCommand { get; }

    public ICommand RemoveCommand { get; }

    public string Title => $"Generation {Genome.Generation}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details), nameof(IsReady))]
    private Morphology? _morphology;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private string? _error;

    public bool IsReady => Morphology is not null;

    public string Details => Morphology is null
        ? (Error is null ? "Growing…" : string.Empty)
        : $"{Morphology.Spheres.Count:N0} spheres{(Morphology.Truncated ? " (capped)" : string.Empty)}"
          + (Morphology.FusedSpheres.Count > 0 ? $", {Morphology.FusedSpheres.Count:N0} fused" : string.Empty);
}
