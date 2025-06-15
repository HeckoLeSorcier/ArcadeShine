// ArcadeShineGameList.cs
// 
// Author: Hecko @ Silversoft
// Created at: 06/08/2025

using ArcadeShine.Common.DataModel;
using Newtonsoft.Json;

namespace ArcadeShine.Common;

public class ArcadeShineGameList : List<ArcadeShineGame>
{
    public static string GameLibraryFile = "ArcadeShineGames.json";
    
    public static ArcadeShineGameList Load(string gameLibraryPath)
    {
        if (!File.Exists(gameLibraryPath + "/" + GameLibraryFile))
        {
            return new ArcadeShineGameList();
        }
        var json = File.ReadAllText(gameLibraryPath + "/" + GameLibraryFile);
        return JsonConvert.DeserializeObject<ArcadeShineGameList>(json) ?? new ArcadeShineGameList();
    }

    public static void Save(string gameLibraryPath, ArcadeShineGameList gameList)
    {
        var json = JsonConvert.SerializeObject(gameList, Formatting.Indented);
        if (!File.Exists(gameLibraryPath + "/" + GameLibraryFile))
        {
            Directory.CreateDirectory(gameLibraryPath);
            var fileStream = File.Create(gameLibraryPath + "/" + GameLibraryFile);
            fileStream.Close();
        }
        File.WriteAllText(gameLibraryPath + "/" + GameLibraryFile, json);
    }
}