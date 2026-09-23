using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Questlog.ViewModels;

namespace Questlog.Views;

public partial class GoalsView : UserControl
{
    private bool _waveRunning;

    public GoalsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is GoalsViewModel oldVm)
            oldVm.PropertyChanged -= OnVmPropertyChanged;

        if (e.NewValue is GoalsViewModel newVm)
            newVm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GoalsViewModel.IsVoiceListening))
            return;

        if (sender is not GoalsViewModel vm) return;

        Dispatcher.Invoke(() =>
        {
            if (vm.IsVoiceListening)
                StartWaveAnimations();
            else
                StopWaveAnimations();
        });
    }

    private void StartWaveAnimations()
    {
        if (_waveRunning) return;
        _waveRunning = true;

        BeginStoryboard((Storyboard)Resources["OrbPulse"], HandoffBehavior.SnapshotAndReplace);
        BeginStoryboard((Storyboard)Resources["Wave1"], HandoffBehavior.SnapshotAndReplace);
        BeginStoryboard((Storyboard)Resources["Wave2"], HandoffBehavior.SnapshotAndReplace);
        BeginStoryboard((Storyboard)Resources["Wave3"], HandoffBehavior.SnapshotAndReplace);
        BeginStoryboard((Storyboard)Resources["Wave4"], HandoffBehavior.SnapshotAndReplace);
        BeginStoryboard((Storyboard)Resources["Wave5"], HandoffBehavior.SnapshotAndReplace);
    }

    private void StopWaveAnimations()
    {
        if (!_waveRunning) return;
        _waveRunning = false;

        foreach (var key in new[] { "OrbPulse", "Wave1", "Wave2", "Wave3", "Wave4", "Wave5" })
        {
            if (Resources.Contains(key))
            {
                var sb = (Storyboard)Resources[key];
                sb.Stop(this);
            }
        }
    }
}
