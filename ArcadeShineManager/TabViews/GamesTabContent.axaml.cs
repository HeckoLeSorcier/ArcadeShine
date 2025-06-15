using System;
using System.Linq;
using System.Text;
using System.Web;
using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace ArcadeShineManager.TabViews;

public partial class GamesTabContent : UserControl
{
    public GamesTabContent()
    {
        InitializeComponent();
        
        GameListBox.ItemsSource = App.ArcadeShineGameList;
        GameListBox.SelectionMode = SelectionMode.Single;
        GameListBox.SelectedIndex = 0;
        if(GameListBox.ItemCount == 0) GameDetailPanel.IsVisible = false;
    }
    
    private void LoadGameUi(ArcadeShineGame game)
    {
        GameSystemComboBox.ItemsSource = App.ArcadeShineSystemList;
        
        GameDisplayNameTextBox.Text = game.GameName;
        GameRomIdentifierTextBox.Text = game.GameRomIdentifier;
        GameShortDescTextBox.Text = game.GameDescription;
        GameDeveloperTextBox.Text = game.GameDeveloper;
        GameReleaseYearTextBox.Text = game.GameReleaseYear;
        if(game.GameGenres != null && game.GameGenres.Count > 0)
            GameGenresTextBox.Text = game.GameGenres.Aggregate((a, b) => $"{a}, {b}");
        var systemIdentifier = game.GameSystem;
        var item = GameSystemComboBox.Items.FirstOrDefault(s => ((ArcadeShineSystem)s).SystemIdentifier == systemIdentifier);
        if(item != null)
            GameSystemComboBox.SelectedItem = item;
        
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
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            mainWindow.UpdateTabContent();
        }
    }

    private void AddGameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        GameDetailPanel.IsVisible = true;
        var newGame = new ArcadeShineGame();
        App.ArcadeShineGameList.Add(newGame);
        GameListBox.ItemsSource = App.ArcadeShineGameList;
        RedrawGameListBox();
        GameListBox.SelectedIndex = App.ArcadeShineGameList.Count - 1;
    }

    private void GameListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        LoadGameUi(App.ArcadeShineGameList[GameListBox.SelectedIndex]);
    }

    private void SaveGameListFile()
    {
        var game = App.ArcadeShineGameList[GameListBox.SelectedIndex];
        game.GameName = GameDisplayNameTextBox.Text!;
        game.GameRomIdentifier = GameRomIdentifierTextBox.Text!;
        game.GameDescription = GameShortDescTextBox.Text!;
        if(GameSystemComboBox.ItemCount > 0 && GameSystemComboBox.SelectedIndex >= 0)
            game.GameSystem = ((ArcadeShineSystem)GameSystemComboBox.SelectedValue).SystemIdentifier;
        game.GameLogo = GameLogoFilename.Text!;
        game.GameBackgroundPicture = GameBackgroundFilename.Text!;
        game.GameVideo = GameVideoTextBox.Text!;
        game.GameReleaseYear = GameReleaseYearTextBox.Text!;
        game.GameDeveloper = GameDeveloperTextBox.Text!;
        game.GameGenres = GameGenresTextBox.Text.Replace(" ", string.Empty).Split(',').ToList();
        RedrawGameListBox();
        ArcadeShineGameList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineGameList);
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
        RedrawGameListBox();
        ArcadeShineGameList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineGameList);
    }
    
    private void DeleteGameButton_OnClick(object? sender, RoutedEventArgs e)
    {
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
}