// ArcadeShineSystemList.cs
// 
// Author: Hecko @ Silversoft
// Created at: 06/08/2025

using ArcadeShine.Common.DataModel;
using Newtonsoft.Json;

namespace ArcadeShine.Common;

public class ArcadeShineSystemList : List<ArcadeShineSystem>
{
    public static string GameLibraryFile = "ArcadeShineSystems.json";
    
    public static ArcadeShineSystemList Load(string gameLibraryPath)
    {
        if (!File.Exists(gameLibraryPath + "/" + GameLibraryFile))
        {
            return new ArcadeShineSystemList();
        }
        var json = File.ReadAllText(gameLibraryPath + "/" + GameLibraryFile);
        return JsonConvert.DeserializeObject<ArcadeShineSystemList>(json) ?? new ArcadeShineSystemList();
    }

    public static void Save(string gameLibraryPath, ArcadeShineSystemList systemList)
    {
        var json = JsonConvert.SerializeObject(systemList, Formatting.Indented);
        if (!File.Exists(gameLibraryPath + "/" + GameLibraryFile))
        {
            Directory.CreateDirectory(gameLibraryPath);
            var fileStream = File.Create(gameLibraryPath + "/" + GameLibraryFile);
            fileStream.Close();
        }
        File.WriteAllText(gameLibraryPath + "/" + GameLibraryFile, json);
    }
}