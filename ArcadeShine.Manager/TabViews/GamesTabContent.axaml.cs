using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace ArcadeShine.Manager.TabViews;

public partial class GamesTabContent : UserControl
{
    private MainWindow _mainWindow;
    
    private ArcadeShineGame? _selectedGame;
    
    private List<GameSystemGamesExpander> _gameSystemExpanders = new();
    
    public GamesTabContent()
    {
        InitializeComponent();
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = desktop.MainWindow as MainWindow;
        }
        LoadGameAndSystemList();
    }

    public void LoadGameAndSystemList()
    {
        int index = 0;
        foreach (var arcadeShineSystem in App.ArcadeShineSystemList.OrderBy(s => s.SystemDisplayName))
        {
            ArcadeShineGameList systemGames = new ArcadeShineGameList();
            systemGames.AddRange(App.ArcadeShineGameList.Where(g => g.GameSystem == arcadeShineSystem.SystemIdentifier)
                .OrderBy(g => g.GameName));
            var systemExpander = new GameSystemGamesExpander(this, arcadeShineSystem, systemGames);
            _gameSystemExpanders.Add(systemExpander);
            GameSystemList.Children.Add(systemExpander);
            if (index == 0)
            {
                if (systemGames.Count > 0)
                {
                    systemExpander.GameListBox.SelectedIndex = 0;
                    systemExpander.Expander.IsExpanded = true;
                }
                else
                {
                    GameDetailPanel.IsVisible = false;
                }
            }
            index++;
        }
    }
    
    public void SelectGame(ArcadeShineGame game, GameSystemGamesExpander expanderSender)
    {
        foreach (var gameSystemExpander in _gameSystemExpanders)
        {
            if(gameSystemExpander != expanderSender)
                gameSystemExpander.GameListBox.SelectedIndex = -1;
        }
        _selectedGame = game;
        LoadGameDetails(_selectedGame);
    }

    private void LoadGameDetails(ArcadeShineGame game)
    {
        ResetGameDetails();
        GameDetailPanel.IsVisible = true;
        GameSystemComboBox.ItemsSource = App.ArcadeShineSystemList;
        GameDisplayNameTextBox.Text = game.GameName;
        GameRomFileTextBox.Text = game.GameProcessArgs;
        SpecificProcessNameSetting.IsVisible = App.ArcadeShineSystemList
            .First(s => s.SystemIdentifier == game.GameSystem).SystemIsGameLauncher;
        SpecificGameProcessNameTextBox.Text = game.GameProcessNameToWatch;
        GameShortDescTextBox.Text = game.GameDescription;
        GameDeveloperTextBox.Text = game.GameDeveloper;
        GameReleaseYearTextBox.Text = game.GameReleaseYear;
        if(game.GameGenres != null && game.GameGenres.Count > 0)
            GameGenresTextBox.Text = game.GameGenres.Aggregate((a, b) => $"{a}, {b}");
        var systemIdentifier = game.GameSystem;
        var item = GameSystemComboBox.Items.FirstOrDefault(s => ((ArcadeShineSystem)s).SystemIdentifier == systemIdentifier);
        if(item != null)
            GameSystemComboBox.SelectedItem = item;
        switch (game.GameVideoAspectRatio)
        {
            case "16:9":
                GameVideoAspectRatioComboBox.SelectedIndex = 0;
                break;
            case "4:3":
                GameVideoAspectRatioComboBox.SelectedIndex = 1;
                break;
            default:
                GameVideoAspectRatioComboBox.SelectedIndex = 0;
                break;           
        }
        
        try
        {
            GameLogoFilename.Text = game.GameLogo;
            var gameLogo = new Avalonia.Media.Imaging.Bitmap(game.GameLogo);
            GameLogoImage.Source = gameLogo;
        }
        catch (Exception ex)
        {
            // ignored
        }
        try
        {
            GameBackgroundFilename.Text = game.GameBackgroundPicture;
            var gameBackground = new Avalonia.Media.Imaging.Bitmap(game.GameBackgroundPicture);
            GameBackgroundImage.Source = gameBackground;
        }
        catch (Exception ex)
        {
            // ignored
        }
        GameVideoTextBox.Text = game.GameVideo;
    }

    private void ResetGameDetails()
    {
        GameSystemComboBox.ItemsSource = null;
        SpecificProcessNameSetting.IsVisible = false;
        SpecificGameProcessNameTextBox.Text = string.Empty;
        GameDisplayNameTextBox.Text = string.Empty;
        GameRomFileTextBox.Text = string.Empty;
        GameShortDescTextBox.Text = string.Empty;
        GameDeveloperTextBox.Text = string.Empty;
        GameReleaseYearTextBox.Text = string.Empty;
        GameGenresTextBox.Text = string.Empty;
        GameVideoAspectRatioComboBox.SelectedIndex = 0;
        GameLogoFilename.Text = string.Empty;
        GameLogoImage.Source = null;
        GameBackgroundFilename.Text = string.Empty;
        GameBackgroundImage.Source = null;
        GameVideoTextBox.Text = string.Empty;
    }
    
    private void RedrawGameListBox()
    {
        _mainWindow.UpdateTabContent();
    }

    private void AddGameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.ArcadeShineSystemList.Count == 0) return;
        GameDetailPanel.IsVisible = true;
        var newGame = new ArcadeShineGame();
        var firstSystem = App.ArcadeShineSystemList.OrderBy(s => s.SystemDisplayName).FirstOrDefault();
        newGame.GameSystem = firstSystem.SystemIdentifier;
        App.ArcadeShineGameList.Add(newGame);
        _mainWindow.PreviousSelectedGame = newGame;
        RedrawGameListBox();
    }

    private void SaveGameListFile()
    {
        _selectedGame.GameName = GameDisplayNameTextBox.Text!;
        _selectedGame.GameProcessArgs = GameRomFileTextBox.Text!;
        _selectedGame.GameProcessNameToWatch = SpecificGameProcessNameTextBox.Text!;
        _selectedGame.GameDescription = GameShortDescTextBox.Text!;
        if(GameSystemComboBox.ItemCount > 0 && GameSystemComboBox.SelectedIndex >= 0)
            _selectedGame.GameSystem = ((ArcadeShineSystem)GameSystemComboBox.SelectedValue).SystemIdentifier;
        _selectedGame.GameLogo = GameLogoFilename.Text!;
        _selectedGame.GameBackgroundPicture = GameBackgroundFilename.Text!;
        _selectedGame.GameVideo = GameVideoTextBox.Text!;
        switch (GameVideoAspectRatioComboBox.SelectedIndex)
        {
            case 0:
                _selectedGame.GameVideoAspectRatio = "16:9";
                break;
            case 1:
                _selectedGame.GameVideoAspectRatio = "4:3";
                break;
            default:
                _selectedGame.GameVideoAspectRatio = "16:9";
                break;           
        }
        _selectedGame.GameReleaseYear = GameReleaseYearTextBox.Text!;
        _selectedGame.GameDeveloper = GameDeveloperTextBox.Text!;
        _selectedGame.GameGenres = GameGenresTextBox.Text?.Replace(" ", string.Empty).Split(',').ToList();
        ArcadeShineGameList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineGameList);
        _mainWindow.PreviousSelectedGame = _selectedGame;
        RedrawGameListBox();
    }
    
    private void SaveGameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveGameListFile();
    }

    private void DeleteGameFromGameListFile()
    {
        App.ArcadeShineGameList.Remove(_selectedGame);
        ArcadeShineGameList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineGameList);
        RedrawGameListBox();
    }
    
    private async void DeleteGameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Delete action", $"Are you sure you would like to delete this entry?", ButtonEnum.YesNo);
        var result = await box.ShowAsync();
        if(result == ButtonResult.Yes)
            DeleteGameFromGameListFile();
    }

    private async void PickGameLogoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new()
            {
                AllowMultiple = false, Title = "Choose the game logo",
                FileTypeFilter = [new("Images")
                {
                    Patterns = new[] { "*.jpg", "*.png" },
                    AppleUniformTypeIdentifiers = new[] { "public.image" },
                    MimeTypes = new[] { "image/*" }
                }]
            });
            if (result.Count == 0) return;
            var absolutePath = HttpUtility.UrlDecode(result[0].Path.AbsolutePath, Encoding.UTF8);
            GameLogoFilename.Text = absolutePath;
            GameLogoImage.Source = new Avalonia.Media.Imaging.Bitmap(absolutePath);
        }
    }

    private async void PickGameBackgroundButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new()
            {
                AllowMultiple = false, Title = "Choose the game background",
                FileTypeFilter = [new("Images")
                {
                    Patterns = new[] { "*.jpg", "*.png" },
                    AppleUniformTypeIdentifiers = new[] { "public.image" },
                    MimeTypes = new[] { "image/*" }
                }]
            });
            if (result.Count == 0) return;
            var absolutePath = HttpUtility.UrlDecode(result[0].Path.AbsolutePath, Encoding.UTF8);
            GameBackgroundFilename.Text = absolutePath;
            GameBackgroundImage.Source = new Avalonia.Media.Imaging.Bitmap(absolutePath);
        }
    }

    private async void PickGameVideoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new()
            {
                AllowMultiple = false, Title = "Choose the game video",
                FileTypeFilter = [new("Videos")
                {
                    Patterns = new[] { "*.mp4", "*.mkv" },
                    AppleUniformTypeIdentifiers = new[] { "public.video" },
                    MimeTypes = new[] { "video/*" }
                }]
            });
            if (result.Count == 0) return;
            var absolutePath = HttpUtility.UrlDecode(result[0].Path.AbsolutePath, Encoding.UTF8);
            GameVideoTextBox.Text = absolutePath;
        }
    }

    private async void BrowseRomFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new()
            {
                AllowMultiple = false, 
                Title = "Choose the game ROM file"
            });
            if (result.Count == 0) return;
            var absolutePath = HttpUtility.UrlDecode(result[0].Path.AbsolutePath, Encoding.UTF8);
            GameRomFileTextBox.Text = absolutePath;
        }
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var textBlock = sender as TextBlock;
        textBlock.Focusable = true;
        textBlock?.Focus();
    }
}