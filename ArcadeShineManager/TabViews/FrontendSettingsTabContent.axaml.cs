using System.Linq;
using ArcadeShine.Common.DataModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArcadeShineManager.TabViews;

public partial class FrontendSettingsTabContent : UserControl
{
    public FrontendSettingsTabContent()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        switch (App.ArcadeShineFrontendSettings.Language)
        {
            case "English":
                FrontendLangComboBox.SelectedIndex = 0;
                break;
            case "français":
                FrontendLangComboBox.SelectedIndex = 1;
                break;
            default:
                FrontendLangComboBox.SelectedIndex = 0;
                break;
        }
        PreserveLastSelectedGameCheckBox.IsChecked = App.ArcadeShineFrontendSettings.PreserveLastSelectedGameOnExit;
        if (!string.IsNullOrEmpty(App.ArcadeShineFrontendSettings.DefaultSelectedGame))
        {
            var game = App.ArcadeShineFrontendSettings.DefaultSelectedGame;
            var itemGame = Enumerable.FirstOrDefault<object?>(DefaultSelectedGameComboBox.Items, item => ((ComboBoxItem)item).Content.ToString() == game);
            if (itemGame == null) DefaultSelectedGameComboBox.SelectedIndex = -1;
            else DefaultSelectedGameComboBox.SelectedItem = App.ArcadeShineFrontendSettings.DefaultSelectedGame;    
        }
        MenuUpButton.Content = App.ArcadeShineFrontendSettings.UpKey;
        MenuDownButton.Content = App.ArcadeShineFrontendSettings.DownKey;
        MenuLeftButton.Content = App.ArcadeShineFrontendSettings.LeftKey;
        MenuRightButton.Content = App.ArcadeShineFrontendSettings.RightKey;
        MenuSelectButton.Content = App.ArcadeShineFrontendSettings.EnterKey;
        MenuBackButton.Content = App.ArcadeShineFrontendSettings.BackKey;
        ExitGameButton.Content = App.ArcadeShineFrontendSettings.ExitKey;
    }
    
    private void OnChangeFrontedLanguage(object? sender, SelectionChangedEventArgs e)
    {
        var comboBox = sender as ComboBox;

        switch (comboBox.SelectedIndex)
        {
            case 0:
                App.ArcadeShineFrontendSettings.Language = "English";
                break;
            case 1:
                App.ArcadeShineFrontendSettings.Language = "français";
                break;
            default:
                App.ArcadeShineFrontendSettings.Language = "English";
                break;
        }
        
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
    }

    private void OnChangeDefaultSelectedGame(object? sender, SelectionChangedEventArgs e)
    {
        var comboBox = sender as ComboBox;
        App.ArcadeShineFrontendSettings.DefaultSelectedGame = comboBox.SelectedItem.ToString();
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
    }

    private void PreserveLastSelectedGameCheckBox_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        var checkBox = sender as CheckBox;
        App.ArcadeShineFrontendSettings.PreserveLastSelectedGameOnExit = checkBox.IsChecked ?? false;
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
    }
}