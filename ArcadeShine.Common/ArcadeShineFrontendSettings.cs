// ArcadeShineFrontendSettings.cs
// 
// Author: Hecko @ Silversoft
// Created at: 06/08/2025

using Newtonsoft.Json;

namespace ArcadeShine.Common.DataModel;

public class ArcadeShineFrontendSettings
{
    public string Language { get; set; } = "English";
    
    public string UpKey { get; set; } = "Up";
    
    public string DownKey { get; set; } = "Down";
    
    public string LeftKey { get; set; } = "Left";
    
    public string RightKey { get; set; } = "Right";
    
    public string EnterKey { get; set; } = "LeftCtrl";
    
    public string BackKey { get; set; } = "LeftAlt";
    
    public string ExitKey { get; set; } = "Escape";
    
    public string GameLibraryPath { get; set; } = "GameLibrary";
    
    public bool PreserveLastSelectedGameOnExit { get; set; } = false;
    
    public string LastSelectedGame { get; set; } = "";
    
    public string DefaultSelectedGame { get; set; } = "";
    
    public static ArcadeShineFrontendSettings Load()
    {
        if (!File.Exists("ArcadeShineFrontendSettings.json"))
        {
            return new ArcadeShineFrontendSettings();
        }
        var json = File.ReadAllText("ArcadeShineFrontendSettings.json");
        return JsonConvert.DeserializeObject<ArcadeShineFrontendSettings>(json) ?? new ArcadeShineFrontendSettings();
    }

    public static void Save(ArcadeShineFrontendSettings settings)
    {
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText("ArcadeShineFrontendSettings.json", json);
    }
}