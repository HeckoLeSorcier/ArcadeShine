// ArcadeShineSystem.cs
// 
// Author: Hecko @ Silversoft
// Created at: 06/08/2025

namespace ArcadeShine.Common.DataModel;

public class ArcadeShineSystem
{
    public string SystemDisplayName { get; set; } = "System Display Name";
    
    public string SystemIdentifier { get; set; } = "SystemIdentifier";
    
    public string SystemExecutable { get; set; } = "C:\\SystemGameLaunchFolder\\Executable.exe";
    
    public string SystemExecutableArguments { get; set; } = "-f -g \"{GAME_FILE}\"";
    
    public string SystemLogo { get; set; } = "GameSystemLogo.png";

    public bool SystemIsGameLauncher { get; set; } = false;
    
    public bool ExitLauncherOnGameExit { get; set; } = false;
}