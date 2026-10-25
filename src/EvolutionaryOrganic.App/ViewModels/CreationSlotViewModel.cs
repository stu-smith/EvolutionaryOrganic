using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.App.ViewModels;

/// <summary>One cell of the 3×3 grid: either the current creation or one of its mutants or offspring.</summary>
public sealed partial class CreationSlotViewModel : ObservableObject
{
    public CreationSlotViewModel(int index, bool isCurrent, ICommand selectCommand, ICommand rerollCommand, ICommand keepCommand)
    {
        Index = index;
        IsCurrent = isCurrent;
        SelectCommand = selectCommand;
        RerollCommand = rerollCommand;
        KeepCommand = keepCommand;
    }

    public int Index { get; }

    public bool IsCurrent { get; }

    public bool IsMutant => !IsCurrent;

    public string Title => IsCurrent ? "Current" : $"{(IsOffspring ? "Offspring" : "Mutant")} {(Index < 4 ? Index + 1 : Index)}";

    public ICommand SelectCommand { get; }

    public ICommand RerollCommand { get; }

    public ICommand KeepCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect), nameof(CanKeep))]
    private Genome? _genome;

    /// <summary>True when this slot holds a cross of two genomes rather than a mutant of the current one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool _isOffspring;

    /// <summary>True when this slot's genome is already in the project.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanKeep), nameof(KeepToolTip))]
    private bool _isKept;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    private Morphology? _morphology;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect), nameof(CanKeep))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    public bool CanSelect => Genome is not null && !IsBusy;

    public bool CanKeep => CanSelect && !IsKept;

    public string KeepToolTip => IsKept ? "Already kept in the project" : "Keep this creation in the project";

    public string Details => Morphology is null
        ? string.Empty
        : $"{Morphology.Spheres.Count:N0} spheres{(Morphology.Truncated ? " (capped)" : string.Empty)}";

    public void SetCreation(Genome genome, Morphology morphology, bool isKept)
    {
        Genome = genome;
        IsKept = isKept;
        Morphology = morphology;
        Error = null;
        IsBusy = false;
    }
}
