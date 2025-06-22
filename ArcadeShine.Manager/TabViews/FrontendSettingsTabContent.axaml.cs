using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcadeShine.Common.DataModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ArcadeShine.Manager.TabViews;

public partial class FrontendSettingsTabContent : UserControl
{
    ManualResetEvent waitInput = new ManualResetEvent(false);
    Key lastKey = Key.None;
    bool isWaitingInput = false;
    private Button? lastInputMappingbutton;

    public FrontendSettingsTabContent()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        
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
        DefaultSelectedGameComboBox.ItemsSource = App.ArcadeShineGameList;
        if (!string.IsNullOrEmpty(App.ArcadeShineFrontendSettings.DefaultSelectedGame))
        {
            var gameName = App.ArcadeShineFrontendSettings.DefaultSelectedGame;
            ArcadeShineGame itemGameName = (ArcadeShineGame)DefaultSelectedGameComboBox.Items.FirstOrDefault(s => ((ArcadeShineGame)s).GameName == gameName);
            if (itemGameName == null) DefaultSelectedGameComboBox.SelectedIndex = -1;
            else DefaultSelectedGameComboBox.SelectedItem = itemGameName;    
        }
        MenuUpButton.Content = App.ArcadeShineFrontendSettings.UpKey;
        MenuDownButton.Content = App.ArcadeShineFrontendSettings.DownKey;
        MenuLeftButton.Content = App.ArcadeShineFrontendSettings.LeftKey;
        MenuRightButton.Content = App.ArcadeShineFrontendSettings.RightKey;
        MenuSelectButton.Content = App.ArcadeShineFrontendSettings.EnterKey;
        MenuBackButton.Content = App.ArcadeShineFrontendSettings.BackKey;
        ExitGameButton.Content = App.ArcadeShineFrontendSettings.ExitKey;
        AllowRandomGameSelectionCheckBox.IsChecked = App.ArcadeShineFrontendSettings.AllowInactivityMode;
        SecondsBeforeRandomGameSelectionTextBox.Text = App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode.ToString();
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
        App.ArcadeShineFrontendSettings.DefaultSelectedGame = ((ArcadeShineGame)comboBox.SelectedItem).GameName;
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
    }

    private void PreserveLastSelectedGameCheckBox_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        var checkBox = sender as CheckBox;
        App.ArcadeShineFrontendSettings.PreserveLastSelectedGameOnExit = checkBox.IsChecked ?? false;
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
    }

    private void InputMappingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        lastInputMappingbutton = sender as Button;
        lastInputMappingbutton.Content = "[Press any key...]";
        isWaitingInput = true;
        WaitInputText.Focus();
        Task.Run(() =>
        {
            waitInput.WaitOne();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                lastInputMappingbutton.Content = lastKey.ToString();
                App.ArcadeShineFrontendSettings.UpKey = lastKey.ToString();
                ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
            });
            waitInput.Reset();
        });
    }

    private void WaitInputText_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!isWaitingInput) return;
        
        var textbox = sender as TextBox;
        e.Handled = true;
        lastInputMappingbutton?.Focus();
        lastKey = e.Key;
        waitInput.Set();
        isWaitingInput = false;
    }

    private void AllowRandomGameSelectionCheckBox_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        var checkBox = sender as CheckBox;
        App.ArcadeShineFrontendSettings.AllowInactivityMode = checkBox.IsChecked ?? false;
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
    }

    private void SecondsBeforeRandomGameSelectionTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (!int.TryParse(textBox.Text, out var seconds))
        {
            return;
        }
        App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode = seconds;
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
    }
}