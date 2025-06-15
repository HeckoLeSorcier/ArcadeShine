using System;
using System.Linq;
using System.Text;
using System.Web;
using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ArcadeShineManager.TabViews;

public partial class GameSystemsTabContent : UserControl
{
    public GameSystemsTabContent()
    {
        InitializeComponent();
        
        GameSystemListBox.ItemsSource = App.ArcadeShineSystemList;
        GameSystemListBox.SelectionMode = SelectionMode.Single;
        GameSystemListBox.SelectedIndex = 0;
        if(GameSystemListBox.ItemCount == 0) GameSystemDetailPanel.IsVisible = false;
    }
    
    private void LoadGameSystemUi(ArcadeShineSystem system)
    {
        SystemDisplayNameTextBox.Text = system.SystemDisplayName;
        SystemIdentifierTextBox.Text = system.SystemIdentifier;
        SystemGameLaunchFolderTextBox.Text = system.SystemGameLaunchFolder;
        SystemGameLaunchCommandTextBox.Text = system.SystemGameLaunchCommand;
        try
        {
            SystemLogoFilename.Text = system.SystemLogo;
            var systemLogo = new Avalonia.Media.Imaging.Bitmap(system.SystemLogo);
            SystemLogoImage.Source = systemLogo;
        }
        catch (Exception ex)
        {
            // ignored
        }
    }
    
    private async void BrowseSystemGameLaunchFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                { AllowMultiple = false, Title = "Choose the game system bin folder" });
            if (result.Count == 0) return;
            var absolutePath = HttpUtility.UrlDecode(result[0].Path.AbsolutePath, Encoding.UTF8);
            SystemGameLaunchFolderTextBox.Text = absolutePath;
        }
    }

    private async void PickSystemLogoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new()
            {
                AllowMultiple = false, Title = "Choose the game system logo",
                FileTypeFilter = [new("Images")
                {
                    Patterns = new[] { "*.jpg", "*.png" },
                    AppleUniformTypeIdentifiers = new[] { "public.image" },
                    MimeTypes = new[] { "image/*" }
                }]
            });
            if (result.Count == 0) return;
            var absolutePath = HttpUtility.UrlDecode(result[0].Path.AbsolutePath, Encoding.UTF8);
            SystemLogoFilename.Text = absolutePath;
            SystemLogoImage.Source = new Avalonia.Media.Imaging.Bitmap(absolutePath);
        }
    }

    private void SaveSystemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveSystemListFile();
    }

    private void SaveSystemListFile()
    {
        var system = App.ArcadeShineSystemList[GameSystemListBox.SelectedIndex];
        system.SystemDisplayName = SystemDisplayNameTextBox.Text!;
        system.SystemIdentifier = SystemIdentifierTextBox.Text!;
        system.SystemGameLaunchFolder = SystemGameLaunchFolderTextBox.Text!;
        system.SystemGameLaunchCommand = SystemGameLaunchCommandTextBox.Text!;
        system.SystemLogo = SystemLogoFilename.Text!;
        RedrawGameSystemListBox();
        ArcadeShineSystemList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineSystemList);
    }

    private void RedrawGameSystemListBox()
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            mainWindow.UpdateTabContent();
        }
    }
    
    private void DeleteSystemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DeleteSystemFromSystemListFile();
    }

    private void DeleteSystemFromSystemListFile()
    {
        var system = App.ArcadeShineSystemList[GameSystemListBox.SelectedIndex];
        GameSystemListBox.SelectedIndex = 0;
        App.ArcadeShineSystemList.Remove(system);
        GameSystemListBox.ItemsSource = App.ArcadeShineSystemList;
        RedrawGameSystemListBox();
        ArcadeShineSystemList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineSystemList);
    }
    
    private void SystemIdentifierTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;

        var caretIndex = textBox.CaretIndex;
        textBox.Text = new string(textBox.Text.Where(char.IsLetterOrDigit).ToArray());
        textBox.CaretIndex = Math.Min(caretIndex, textBox.Text.Length);
    }
    
    private void GameSystemListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        LoadGameSystemUi(App.ArcadeShineSystemList[GameSystemListBox.SelectedIndex]);
    }

    private void AddSystemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        GameSystemDetailPanel.IsVisible = true;
        var newSystem = new ArcadeShineSystem();
        App.ArcadeShineSystemList.Add(newSystem);
        GameSystemListBox.ItemsSource = App.ArcadeShineSystemList;
        RedrawGameSystemListBox();
        GameSystemListBox.SelectedIndex = App.ArcadeShineSystemList.Count - 1;
    }
}