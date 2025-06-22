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
        var gameList = JsonConvert.DeserializeObject<ArcadeShineGameList>(json);
        if (gameList != null)
        {
            var ordered = gameList.OrderBy(s => s.GameName).ToList();
            return FromList(ordered);
        }
        return  new ArcadeShineGameList();
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
    
    public static ArcadeShineGameList FromList(List<ArcadeShineGame> games)
    {
        var gameList = new ArcadeShineGameList();
        foreach (var arcadeShineGame in games)
        {
            gameList.Add(arcadeShineGame);
        }
        return gameList;       
    }
}