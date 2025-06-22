using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ArcadeShine.Manager;

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
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}