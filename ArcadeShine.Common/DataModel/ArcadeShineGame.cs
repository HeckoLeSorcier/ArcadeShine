// ArcadeShineGame.cs
// 
// Author: Hecko @ Silversoft
// Created at: 06/08/2025

namespace ArcadeShine.Common.DataModel;

public class ArcadeShineGame
{
    public string GameRomIdentifier { get; set; } = "";
    
    public string GameName { get; set; } = "";
    
    public string GameDescription { get; set; } = "";
    
    public string GameSystem { get; set; } = "";
    
    public List<string> GameGenres { get; set; } = new List<string>();
    
    public string GameDeveloper { get; set; } = "";
    
    public string GameReleaseYear { get; set; } = "";
    
    public string GameLogo { get; set; } = "";
    
    public string GameBackgroundPicture { get; set; } = "";
    
    public string GameVideo { get; set; } = "";
    
    public string GameVideoAspectRatio { get; set; } = "";
}