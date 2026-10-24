using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvolutionaryOrganic.App.Services;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Evolution;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.App.ViewModels;

/// <summary>
/// Drives the evolutionary loop: the centre slot holds the current creation, and the
/// eight surrounding slots hold its mutants. Selecting a mutant makes it current.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const int CenterIndex = 4;

    private readonly Func<AppSettings> _settings;
    private readonly Stack<Genome> _history = new();
    private readonly Random _rng = new();
    private int _batch;

    public MainViewModel(Func<AppSettings> settings)
    {
        _settings = settings;
        Slots = new ReadOnlyCollection<CreationSlotViewModel>(Enumerable.Range(0, 9)
            .Select(i => new CreationSlotViewModel(i, i == CenterIndex, SelectCommand, RerollCommand))
            .ToList());
    }

    public IReadOnlyList<CreationSlotViewModel> Slots { get; }

    public CreationSlotViewModel CurrentSlot => Slots[CenterIndex];

    public Genome? CurrentGenome => CurrentSlot.Genome;

    /// <summary>The genome tree of the current creation, as a single-root list for the TreeView.</summary>
    [ObservableProperty]
    private IReadOnlyList<GenomeNodeViewModel> _genomeTree = [];

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _surfaceSummary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    private int _historyDepth;

    /// <summary>0 = subtle, 1 = wild. Takes effect on the next re-roll.</summary>
    public double MutationStrength
    {
        get => _settings().MutationStrength;
        set => SetSetting(_settings().MutationStrength, value, v => _settings().MutationStrength = v);
    }

    /// <summary>Chance that a mutation alters structure rather than parameters. Takes effect on the next re-roll.</summary>
    public double StructuralMutationRate
    {
        get => _settings().StructuralMutationRate;
        set => SetSetting(_settings().StructuralMutationRate, value, v => _settings().StructuralMutationRate = v);
    }

    /// <summary>Upper limit on spheres per creation. Takes effect on the next re-roll.</summary>
    public int MaxSpheres
    {
        get => _settings().MaxSpheres;
        set => SetSetting(_settings().MaxSpheres, value, v => _settings().MaxSpheres = v);
    }

    public void Start() => NewRandom();

    private void SetSetting<T>(T current, T value, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        apply(value);
        OnPropertyChanged(propertyName);
    }

    /// <summary>Makes <paramref name="genome"/> the current creation and breeds new mutants from it.</summary>
    public void SetCurrent(Genome genome, bool remember = true)
    {
        if (remember && CurrentGenome is not null)
        {
            _history.Push(CurrentGenome);
            HistoryDepth = _history.Count;
        }

        var morphology = MorphologyBuilder.Build(genome, Limits());
        CurrentSlot.SetCreation(genome, morphology);
        GenomeTree = [new GenomeNodeViewModel(genome.Root)];
        Summary = $"Generation {genome.Generation} · {morphology.Spheres.Count:N0} spheres" +
                  (morphology.Truncated ? " (capped by sphere limit)" : string.Empty);
        SurfaceSummary = genome.Surface.ToString();
        RerollCommand.Execute(null);
    }

    [RelayCommand]
    private void NewRandom() => SetCurrent(RandomGenomeFactory.CreateGenome(_rng, _settings().MaxSpheres));

    [RelayCommand]
    private void Select(CreationSlotViewModel? slot)
    {
        if (slot is null || slot.IsCurrent || !slot.CanSelect) return;
        SetCurrent(slot.Genome!);
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (_history.Count == 0) return;
        var previous = _history.Pop();
        HistoryDepth = _history.Count;
        SetCurrent(previous, remember: false);
    }

    private bool CanGoBack() => HistoryDepth > 0;

    /// <summary>Discards the eight mutants and breeds a fresh set from the current creation.</summary>
    [RelayCommand]
    private void Reroll()
    {
        var parent = CurrentGenome;
        if (parent is null) return;

        int batch = ++_batch;
        var settings = _settings();
        var mutation = new MutationSettings
        {
            Strength = (float)settings.MutationStrength,
            StructuralProbability = (float)settings.StructuralMutationRate,
            MaxSpheres = settings.MaxSpheres,
        };
        var limits = Limits();
        var ui = TaskScheduler.FromCurrentSynchronizationContext();

        foreach (var slot in Slots.Where(s => !s.IsCurrent))
        {
            int seed = _rng.Next();
            slot.IsBusy = true;
            Task.Run(() =>
                {
                    var genome = Mutator.Mutate(parent, new Random(seed), mutation);
                    return (Genome: genome, Morphology: MorphologyBuilder.Build(genome, limits));
                })
                .ContinueWith(task =>
                {
                    if (batch != _batch) return; // A newer re-roll has superseded this one.
                    if (task.IsFaulted)
                    {
                        slot.Error = task.Exception?.GetBaseException().Message;
                        slot.IsBusy = false;
                        return;
                    }
                    slot.SetCreation(task.Result.Genome, task.Result.Morphology);
                }, ui);
        }
    }

    private MorphologyLimits Limits() => new() { MaxSpheres = _settings().MaxSpheres };
}
