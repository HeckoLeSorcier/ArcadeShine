using System;
using System.Linq;
using ArcadeShine.Common.DataModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ArcadeShine.Manager;

public partial class MainWindow : Window
{
    public ArcadeShineGame? PreviousSelectedGame = null;
    public int? PreviousSelectedSystemIndex = null;
    
    public MainWindow()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        MenuBar.SelectedIndex = 0;
        UpdateTabContent();
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TabContent == null) return;
        
        UpdateTabContent();
    }

    public void UpdateTabContent()
    {
        var tabStrip = MenuBar;
        var tabStripItem = tabStrip?.SelectedItem as TabStripItem;
        switch (tabStripItem?.Name)
        {
            case "GamesTabItem":
                TabContent.Child = new TabViews.GamesTabContent();
                break;
            case "GameSystemsTabItem":
                TabContent.Child = new TabViews.GameSystemsTabContent();
                break;
            case "FrontendSettingsTabItem":
                TabContent.Child = new TabViews.FrontendSettingsTabContent();
                break;
            default:
                break;
        }
        Dispatcher.UIThread.InvokeAsync(TabContent.InvalidateMeasure);
    }
}