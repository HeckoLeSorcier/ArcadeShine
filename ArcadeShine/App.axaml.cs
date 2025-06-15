using System.Globalization;
using System.IO;
using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Newtonsoft.Json;

namespace ArcadeShine;

public partial class App : Application
{
    public static ArcadeShineFrontendSettings ArcadeShineFrontendSettings = null!;
    
    public static ArcadeShineSystemList ArcadeShineSystemList = null!;
    
    public static ArcadeShineGameList ArcadeShineGameList = null!;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ArcadeShineFrontendSettings = ArcadeShineFrontendSettings.Load();
        ArcadeShineSystemList = ArcadeShineSystemList.Load(ArcadeShineFrontendSettings.GameLibraryPath);
        ArcadeShineGameList = ArcadeShineGameList.Load(ArcadeShineFrontendSettings.GameLibraryPath);

        ChangeLanguage(ArcadeShineFrontendSettings.Language);
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ChangeLanguage(string language)
    {
        switch (language)
        {
            case "English":
                Lang.Resources.Culture = new CultureInfo("en-GB");
                break;
            case "français":
                Lang.Resources.Culture = new CultureInfo("fr-FR");
                break;
        }
    }
}