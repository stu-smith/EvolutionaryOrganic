using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.App.ViewModels;

/// <summary>One cell of the 3×3 grid: either the current creation or one of its mutants.</summary>
public sealed partial class CreationSlotViewModel : ObservableObject
{
    public CreationSlotViewModel(int index, bool isCurrent, ICommand selectCommand, ICommand rerollCommand)
    {
        Index = index;
        IsCurrent = isCurrent;
        SelectCommand = selectCommand;
        RerollCommand = rerollCommand;
    }

    public int Index { get; }

    public bool IsCurrent { get; }

    public bool IsMutant => !IsCurrent;

    public string Title => IsCurrent ? "Current" : $"Mutant {(Index < 4 ? Index + 1 : Index)}";

    public ICommand SelectCommand { get; }

    public ICommand RerollCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private Genome? _genome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private Morphology? _morphology;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    public bool CanSelect => Genome is not null && !IsBusy;

    public string Details => Morphology is null
        ? string.Empty
        : $"{Morphology.Spheres.Count:N0} spheres{(Morphology.Truncated ? " (capped)" : string.Empty)}";

    public void SetCreation(Genome genome, Morphology morphology)
    {
        Genome = genome;
        Morphology = morphology;
        Error = null;
        IsBusy = false;
    }
}
