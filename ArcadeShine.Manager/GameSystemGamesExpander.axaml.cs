using System.Collections.Generic;
using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using ArcadeShine.Manager.TabViews;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ArcadeShine.Manager;

public partial class GameSystemGamesExpander : UserControl
{
    private GamesTabContent _gamesTabContent;
    private MainWindow _mainWindow;
    private ArcadeShineSystem _system;
    private ArcadeShineGameList _games;
    
    public GameSystemGamesExpander(GamesTabContent gamesTabContent, ArcadeShineSystem system, ArcadeShineGameList games)
    {
        InitializeComponent();
        
        _gamesTabContent = gamesTabContent;
        _system = system;
        _games = games;
        
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = desktop.MainWindow as MainWindow;
        }

        Expander.Header = _system.SystemDisplayName;
        
        GameListBox.ItemsSource = _games;
        GameListBox.SelectionMode = SelectionMode.Single;
        if(GameListBox.ItemCount == 0)
        {
            _gamesTabContent.GameDetailPanel.IsVisible = false;
        }
        if (_mainWindow.PreviousSelectedGame != null && GameListBox.Items.Contains(_mainWindow.PreviousSelectedGame))
        {
            GameListBox.SelectedItem = _mainWindow.PreviousSelectedGame;
            Expander.IsExpanded = true;
            _mainWindow.PreviousSelectedGame = null;       
        }
        else
        {
            GameListBox.SelectedIndex = -1;
        }
    }

    private void GameListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if(GameListBox.SelectedIndex < 0) return;
        
        ArcadeShineGame selectedGame = GameListBox.SelectedItem as ArcadeShineGame;
        _gamesTabContent.SelectGame(selectedGame, this);
    }
}