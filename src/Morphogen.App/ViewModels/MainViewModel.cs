using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Morphogen.App.Services;
using Morphogen.Core.Development;
using Morphogen.Core.Evolution;
using Morphogen.Core.Genetics;
using Morphogen.Core.Serialization;

namespace Morphogen.App.ViewModels;

/// <summary>
/// Drives the evolutionary loop: the centre slot holds the current creation, and the
/// eight surrounding slots hold its mutants, or its offspring with a genome kept in the project.
/// Selecting one makes it current.
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
            .Select(i => new CreationSlotViewModel(i, i == CenterIndex, SelectCommand, RerollCommand, KeepCommand))
            .ToList());
        ProjectGenomes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsProjectEmpty));
    }

    public IReadOnlyList<CreationSlotViewModel> Slots { get; }

    /// <summary>The genomes kept in the project, shown down the left of the window.</summary>
    public ObservableCollection<ProjectGenomeViewModel> ProjectGenomes { get; } = [];

    public bool IsProjectEmpty => ProjectGenomes.Count == 0;

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

    /// <summary>Where the project was last opened from or saved to, or null if it has never been saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectName), nameof(WindowTitle))]
    private string? _projectPath;

    /// <summary>True when genomes have been kept or removed since the project was last opened or saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    public string ProjectName => ProjectPath is null
        ? "Untitled"
        : Path.GetFileName(ProjectPath)
            .Replace(GenomeSerializer.ProjectFileExtension, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(GenomeSerializer.LegacyProjectFileExtension, string.Empty, StringComparison.OrdinalIgnoreCase);

    public string WindowTitle => $"{ProjectName}{(IsDirty ? "*" : string.Empty)} – Morphogen";

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

    /// <summary>Upper limit on procedural textures per genome. Takes effect on the next re-roll.</summary>
    public int MaxTextures
    {
        get => _settings().MaxTextures;
        set => SetSetting(_settings().MaxTextures, value, v => _settings().MaxTextures = v);
    }

    /// <summary>Upper limit on procedural bump maps per genome. Takes effect on the next re-roll.</summary>
    public int MaxBumpMaps
    {
        get => _settings().MaxBumpMaps;
        set => SetSetting(_settings().MaxBumpMaps, value, v => _settings().MaxBumpMaps = v);
    }

    public void Start() => NewRandom();

    private void SetSetting<T>(T current, T value, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        apply(value);
        OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// Makes <paramref name="genome"/> the current creation and breeds new mutants from it. Pass the genome's
    /// <paramref name="morphology"/> if it has already been grown, to save growing it again.
    /// </summary>
    public void SetCurrent(Genome genome, bool remember = true, Morphology? morphology = null)
    {
        if (remember && CurrentGenome is not null)
        {
            _history.Push(CurrentGenome);
            HistoryDepth = _history.Count;
        }

        morphology ??= MorphologyBuilder.Build(genome, Limits());
        CurrentSlot.SetCreation(genome, morphology, IsKept(genome));
        GenomeTree = GenomeNodeViewModel.ForGenome(genome, morphology);
        Summary = $"Generation {genome.Generation} · {morphology.Spheres.Count:N0} spheres" +
                  (morphology.Truncated ? " (capped by sphere limit)" : string.Empty);
        SurfaceSummary = $"{genome.Surface} · {Plural(genome.Textures.Count, "texture")} · {Plural(genome.BumpMaps.Count, "bump map")}";
        RerollCommand.Execute(null);
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    // ---------------------------------------------------------------- project

    /// <summary>Starts an empty, untitled project with a fresh random creation.</summary>
    public void NewProject()
    {
        ProjectGenomes.Clear();
        ClearHistory();
        ProjectPath = null;
        IsDirty = false;
        NewRandom();
    }

    /// <summary>Replaces the project with <paramref name="project"/>, resuming from its current creation.</summary>
    public void OpenProject(Project project, string path)
    {
        ProjectGenomes.Clear();
        foreach (var genome in project.Genomes) AddToProject(genome);
        ClearHistory();
        ProjectPath = path;
        IsDirty = false;

        var current = project.Current ?? project.Genomes.FirstOrDefault();
        if (current is null)
        {
            NewRandom();
            return;
        }
        // If the current creation was kept, show it as kept: it was saved as a separate copy of the same genome.
        string json = GenomeSerializer.Serialize(current);
        SetCurrent(project.Genomes.FirstOrDefault(g => GenomeSerializer.Serialize(g) == json) ?? current, remember: false);
    }

    /// <summary>A snapshot of the project for saving.</summary>
    public Project ToProject() => new()
    {
        Genomes = ProjectGenomes.Select(p => p.Genome).ToList(),
        Current = CurrentGenome,
    };

    public void MarkSaved(string path)
    {
        ProjectPath = path;
        IsDirty = false;
    }

    /// <summary>Adds <paramref name="genome"/> to the project list, unless it is already there.</summary>
    public void AddToProject(Genome genome, Morphology? morphology = null)
    {
        if (IsKept(genome)) return;

        var item = new ProjectGenomeViewModel(genome, CombineCommand, SelectKeptCommand, RemoveKeptCommand);
        ProjectGenomes.Add(item);
        IsDirty = true;
        RefreshKept();

        if (morphology is not null)
        {
            item.Morphology = morphology;
            return;
        }

        var limits = Limits();
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        Task.Run(() => MorphologyBuilder.Build(genome, limits))
            .ContinueWith(task =>
            {
                if (task.IsFaulted) item.Error = task.Exception?.GetBaseException().Message;
                else item.Morphology = task.Result;
            }, ui);
    }

    private bool IsKept(Genome genome) => ProjectGenomes.Any(p => ReferenceEquals(p.Genome, genome));

    private void RefreshKept()
    {
        foreach (var slot in Slots) slot.IsKept = slot.Genome is { } genome && IsKept(genome);
    }

    private void ClearHistory()
    {
        _history.Clear();
        HistoryDepth = 0;
    }

    /// <summary>Keeps a creation from the grid in the project.</summary>
    [RelayCommand]
    private void Keep(CreationSlotViewModel? slot)
    {
        if (slot is null || !slot.CanKeep) return;
        AddToProject(slot.Genome!, slot.Morphology);
    }

    /// <summary>Makes a kept genome the current creation.</summary>
    [RelayCommand]
    private void SelectKept(ProjectGenomeViewModel? item)
    {
        if (item is null || ReferenceEquals(item.Genome, CurrentGenome)) return;
        SetCurrent(item.Genome, morphology: item.Morphology);
    }

    [RelayCommand]
    private void RemoveKept(ProjectGenomeViewModel? item)
    {
        if (item is null || !ProjectGenomes.Remove(item)) return;
        IsDirty = true;
        RefreshKept();
    }

    /// <summary>Fills the eight outer slots with offspring of the current creation crossed with a kept genome.</summary>
    [RelayCommand]
    private void Combine(ProjectGenomeViewModel? item)
    {
        if (item is null || CurrentGenome is null) return;
        var mate = item.Genome;
        var mutation = CreateMutationSettings();
        Breed((parent, rng) => Crossover.Cross(parent, mate, rng, mutation), isOffspring: true);
    }

    // ---------------------------------------------------------------- evolution

    [RelayCommand]
    private void NewRandom()
    {
        var settings = _settings();
        SetCurrent(RandomGenomeFactory.CreateGenome(_rng, settings.MaxSpheres, settings.MaxTextures, settings.MaxBumpMaps));
    }

    [RelayCommand]
    private void Select(CreationSlotViewModel? slot)
    {
        if (slot is null || slot.IsCurrent || !slot.CanSelect) return;
        SetCurrent(slot.Genome!, morphology: slot.Morphology);
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

    /// <summary>Discards the eight outer creations and breeds a fresh set of mutants from the current creation.</summary>
    [RelayCommand]
    private void Reroll()
    {
        var mutation = CreateMutationSettings();
        Breed((parent, rng) => Mutator.Mutate(parent, rng, mutation), isOffspring: false);
    }

    /// <summary>Fills the eight outer slots with children of the current creation made by <paramref name="breed"/>.</summary>
    private void Breed(Func<Genome, Random, Genome> breed, bool isOffspring)
    {
        var parent = CurrentGenome;
        if (parent is null) return;

        int batch = ++_batch;
        var limits = Limits();
        var ui = TaskScheduler.FromCurrentSynchronizationContext();

        foreach (var slot in Slots.Where(s => !s.IsCurrent))
        {
            int seed = _rng.Next();
            slot.BeginGrowing(isOffspring);
            Task.Run(() =>
                {
                    var genome = breed(parent, new Random(seed));
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
                    slot.SetCreation(task.Result.Genome, task.Result.Morphology, isKept: false);
                }, ui);
        }
    }

    private MutationSettings CreateMutationSettings()
    {
        var settings = _settings();
        return new MutationSettings
        {
            Strength = (float)settings.MutationStrength,
            StructuralProbability = (float)settings.StructuralMutationRate,
            MaxSpheres = settings.MaxSpheres,
            MaxTextures = settings.MaxTextures,
            MaxBumpMaps = settings.MaxBumpMaps,
        };
    }

    private MorphologyLimits Limits() => new() { MaxSpheres = _settings().MaxSpheres };
}
