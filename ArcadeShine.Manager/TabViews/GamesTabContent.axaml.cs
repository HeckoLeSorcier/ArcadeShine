using System;
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
    
    public GamesTabContent()
    {
        InitializeComponent();
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = desktop.MainWindow as MainWindow;
        }
        
        GameListBox.ItemsSource = App.ArcadeShineGameList;
        GameListBox.SelectionMode = SelectionMode.Single;
        if(GameListBox.ItemCount == 0)
        {
            GameDetailPanel.IsVisible = false;
        }
        if (_mainWindow.PreviousSelectedGameIndex != null)
        {
            GameListBox.SelectedIndex = _mainWindow.PreviousSelectedGameIndex.Value;
            _mainWindow.PreviousSelectedGameIndex = null;       
        }
        else
        {
            GameListBox.SelectedIndex = 0;
        }
    }

    private void ResetUi()
    {
        GameSystemComboBox.ItemsSource = null;
        GameDisplayNameTextBox.Text = String.Empty;
        GameRomFileTextBox.Text = String.Empty;
        GameShortDescTextBox.Text = String.Empty;
        GameDeveloperTextBox.Text = String.Empty;
        GameReleaseYearTextBox.Text = String.Empty;
        GameGenresTextBox.Text = String.Empty;
        GameVideoAspectRatioComboBox.SelectedIndex = 0;
        GameLogoFilename.Text = String.Empty;
        GameLogoImage.Source = null;
        GameBackgroundFilename.Text = String.Empty;
        GameBackgroundImage.Source = null;
        GameVideoTextBox.Text = String.Empty;
    }
    
    private void LoadGameUi(ArcadeShineGame game)
    {
        ResetUi();
        GameSystemComboBox.ItemsSource = App.ArcadeShineSystemList;
        GameDisplayNameTextBox.Text = game.GameName;
        GameRomFileTextBox.Text = game.GameRomFile;
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
    
    private void RedrawGameListBox()
    {
        _mainWindow.UpdateTabContent();
    }

    private void AddGameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        GameDetailPanel.IsVisible = true;
        var newGame = new ArcadeShineGame();
        App.ArcadeShineGameList.Add(newGame);
        GameListBox.ItemsSource = App.ArcadeShineGameList;
        _mainWindow.PreviousSelectedGameIndex = App.ArcadeShineGameList.Count - 1;
        RedrawGameListBox();
    }

    private void GameListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        LoadGameUi(App.ArcadeShineGameList[GameListBox.SelectedIndex]);
    }

    private void SaveGameListFile()
    {
        var game = App.ArcadeShineGameList[GameListBox.SelectedIndex];
        game.GameName = GameDisplayNameTextBox.Text!;
        game.GameRomFile = GameRomFileTextBox.Text!;
        game.GameDescription = GameShortDescTextBox.Text!;
        if(GameSystemComboBox.ItemCount > 0 && GameSystemComboBox.SelectedIndex >= 0)
            game.GameSystem = ((ArcadeShineSystem)GameSystemComboBox.SelectedValue).SystemIdentifier;
        game.GameLogo = GameLogoFilename.Text!;
        game.GameBackgroundPicture = GameBackgroundFilename.Text!;
        game.GameVideo = GameVideoTextBox.Text!;
        switch (GameVideoAspectRatioComboBox.SelectedIndex)
        {
            case 0:
                game.GameVideoAspectRatio = "16:9";
                break;
            case 1:
                game.GameVideoAspectRatio = "4:3";
                break;
            default:
                game.GameVideoAspectRatio = "16:9";
                break;           
        }
        game.GameReleaseYear = GameReleaseYearTextBox.Text!;
        game.GameDeveloper = GameDeveloperTextBox.Text!;
        game.GameGenres = GameGenresTextBox.Text?.Replace(" ", string.Empty).Split(',').ToList();
        ArcadeShineGameList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineGameList);
        _mainWindow.PreviousSelectedGameIndex = GameListBox.SelectedIndex;
        RedrawGameListBox();
    }
    
    private void SaveGameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveGameListFile();
    }

    private void DeleteGameFromGameListFile()
    {
        var game = App.ArcadeShineGameList[GameListBox.SelectedIndex];
        GameListBox.SelectedIndex = 0;
        App.ArcadeShineGameList.Remove(game);
        GameListBox.ItemsSource = App.ArcadeShineGameList;
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