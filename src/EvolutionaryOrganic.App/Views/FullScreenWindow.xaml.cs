using System.Windows;
using System.Windows.Input;
using EvolutionaryOrganic.App.ViewModels;

namespace EvolutionaryOrganic.App.Views;

/// <summary>Shows a single creation filling the screen. Esc or the close button returns to the grid.</summary>
public partial class FullScreenWindow : Window
{
    public FullScreenWindow(CreationSlotViewModel slot)
    {
        InitializeComponent();
        DataContext = slot;
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => Close();
}
