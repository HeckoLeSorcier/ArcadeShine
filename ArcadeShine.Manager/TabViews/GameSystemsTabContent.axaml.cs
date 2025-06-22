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
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace ArcadeShine.Manager.TabViews;

public partial class GameSystemsTabContent : UserControl
{
    private MainWindow _mainWindow;
    
    public GameSystemsTabContent()
    {
        InitializeComponent();
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = desktop.MainWindow as MainWindow;
        }
        
        GameSystemListBox.ItemsSource = App.ArcadeShineSystemList;
        GameSystemListBox.SelectionMode = SelectionMode.Single;
        if(GameSystemListBox.ItemCount == 0)
        {
            GameSystemDetailPanel.IsVisible = false;
        }
        if (_mainWindow.PreviousSelectedSystemIndex != null)
        {
            GameSystemListBox.SelectedIndex = _mainWindow.PreviousSelectedSystemIndex.Value;
            _mainWindow.PreviousSelectedSystemIndex = null;       
        }
        else
        {
            GameSystemListBox.SelectedIndex = 0;
        }
    }

    private void ResetUi()
    {
        SystemDisplayNameTextBox.Text = String.Empty;
        SystemIdentifierTextBox.Text = String.Empty;
        SystemExecutableTextBox.Text = String.Empty;
        SystemExecutableArgumentsTextBox.Text = String.Empty;
        SystemLogoFilename.Text = String.Empty;
        SystemLogoImage.Source = null;
    }
    
    private void LoadGameSystemUi(ArcadeShineSystem system)
    {
        ResetUi();
        SystemDisplayNameTextBox.Text = system.SystemDisplayName;
        SystemIdentifierTextBox.Text = system.SystemIdentifier;
        SystemExecutableTextBox.Text = system.SystemExecutable;
        SystemExecutableArgumentsTextBox.Text = system.SystemExecutableArguments;
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
    
    private async void BrowseSystemExecutableButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow as MainWindow;
            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new()
            {
                AllowMultiple = false, 
                Title = "Choose the game system executable"
            });
            if (result.Count == 0) return;
            var absolutePath = HttpUtility.UrlDecode(result[0].Path.AbsolutePath, Encoding.UTF8);
            SystemExecutableTextBox.Text = absolutePath;
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
        system.SystemExecutable = SystemExecutableTextBox.Text!;
        system.SystemExecutableArguments = SystemExecutableArgumentsTextBox.Text!;
        system.SystemLogo = SystemLogoFilename.Text!;
        _mainWindow.PreviousSelectedSystemIndex = GameSystemListBox.SelectedIndex;
        ArcadeShineSystemList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineSystemList);
        RedrawGameSystemListBox();
    }

    private void RedrawGameSystemListBox()
    {
        _mainWindow.UpdateTabContent();
    }
    
    private async void DeleteSystemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Delete action", $"Are you sure you would like to delete this entry?", ButtonEnum.YesNo);
        var result = await box.ShowAsync();
        if(result == ButtonResult.Yes)
            DeleteSystemFromSystemListFile();
    }

    private void DeleteSystemFromSystemListFile()
    {
        var system = App.ArcadeShineSystemList[GameSystemListBox.SelectedIndex];
        GameSystemListBox.SelectedIndex = 0;
        App.ArcadeShineSystemList.Remove(system);
        GameSystemListBox.ItemsSource = App.ArcadeShineSystemList;
        ArcadeShineSystemList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineSystemList);
        RedrawGameSystemListBox();
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
        _mainWindow.PreviousSelectedSystemIndex = App.ArcadeShineSystemList.Count - 1;
        RedrawGameSystemListBox();
    }
}