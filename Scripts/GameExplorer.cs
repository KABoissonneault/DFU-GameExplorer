using System;
using System.IO;
using UnityEngine;

using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility;
using System.Collections.Generic;
using System.Linq;
using DaggerfallWorkshop.Utility;

public class GameExplorer : MonoBehaviour
{
    readonly string careersOutputFilename = "careers.csv";
    readonly string dungeonOutputFilename = "dungeons.csv";
    readonly string dungeonExteriorOutputFilename = "dungeons_exterior.csv";

    static Mod mod;

    [Invoke(StateManager.StateTypes.Start, 0)]
    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        new GameObject(mod.Title).AddComponent<GameExplorer>();
    }

    void Awake()
    {
        Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand("dump_monster_careers"
            , $"Dumps all the monster career info in {careersOutputFilename}", "DUMP_MONSTER_CAREERS", PrintMonsterCareers);
        Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand("dump_dungeon_data"
            , $"Dumps all the dungeon info in {dungeonOutputFilename}", "DUMP_DUNGEON_DATA", PrintDungeonData);
        Wenzil.Console.ConsoleCommandsDatabase.RegisterCommand("print_blocks_using"
            , $"Prints all the block using the specified model", BlocksUsage, PrintBlocksUsingModel);
    }

    string PrintDungeonData(params string[] args)
    {
        string outputPath = Path.Combine(DaggerfallUnity.Settings.PersistentDataPath, dungeonOutputFilename);
        string exteriorOutputPath = Path.Combine(DaggerfallUnity.Settings.PersistentDataPath, dungeonExteriorOutputFilename);

        using (StreamWriter outputFile = new StreamWriter(outputPath))
        using (StreamWriter exteriorOutputFile = new StreamWriter(exteriorOutputPath))
        {
            outputFile.WriteLine("Region;Name;Type;Flags;Index;A;B;#Loc");
            exteriorOutputFile.WriteLine("Region;Name;Type;Longitude;Latitude;Width;Height;Discovered;Hidden");

            MapsFile mapFileReader = DaggerfallUnity.Instance.ContentReader.MapFileReader;
            for(int regionIndex = 0; regionIndex < mapFileReader.RegionCount; ++regionIndex)
            {
                DFRegion region = mapFileReader.GetRegion(regionIndex);
                for(int locationIndex = 0; locationIndex < region.LocationCount; ++locationIndex)
                {
                    DFLocation location = mapFileReader.GetLocation(regionIndex, locationIndex);
                    if (!location.HasDungeon)
                        continue;

                    DFLocation.LocationDungeon dungeon = location.Dungeon;
                    var LocationHeader = dungeon.RecordElement.Header;
                    if (LocationHeader.Unknown3 == null)
                        continue;

                    DFLocation.ExteriorData exterior = location.Exterior.ExteriorData;

                    if (!region.MapIdLookup.TryGetValue(exterior.MapId, out int mapTableIndex))
                        continue;

                    DFRegion.RegionMapTable mapTable = region.MapTable[mapTableIndex];

                    var Flags = LocationHeader.Unknown1;
                    var Index = LocationHeader.Unknown2;
                    var DungeonType = (DFRegion.DungeonTypes)LocationHeader.Unknown3[2];
                    var Unknown3A = LocationHeader.Unknown3[5]; // Value [0, 255]
                    var Unknown3B = LocationHeader.Unknown3[6]; // Value [0, 9]
                    var QuestLocations = LocationHeader.Unknown3[7]; // Value [0, 18]
                    // Unknown3[8] is always 0xFA
                    // Unknown3[9] is always 0x00

                    outputFile.WriteLine($"{region.Name};{LocationHeader.LocationName};{DungeonType};{Flags:X};{Index};" +
                        $"{Unknown3A};{Unknown3B};{QuestLocations}"
                        );
                    /*
                    exteriorOutputFile.WriteLine($"{region.Name};{LocationHeader.LocationName};{DungeonType};{mapTable.Longitude};" +
                        $"{mapTable.Latitude};{mapTable.Width};{mapTable.Height};{mapTable.Discovered};{mapTable.Hidden}"
                        );
                    */
                }
            }
        }

        return $"Dungeon info dumped in {dungeonOutputFilename}";
    }

    string PrintMonsterCareers(params string[] args)
    {
        string outputPath = Path.Combine(DaggerfallUnity.Settings.PersistentDataPath, careersOutputFilename);

        using (StreamWriter outputFile = new StreamWriter(outputPath))
        {
            outputFile.WriteLine("Name;HP;Strength;Intelligence;Willpower;Agility;Endurance;Personality;Speed;Luck;" +
                "Primary 1;Primary 2;Primary 3;Major 1;Major 2;Major 3;Minor 1;Minor 2;Minor 3;Minor 4;Minor 5;Minor 6");

            foreach (MonsterCareers monster in Enum.GetValues(typeof(MonsterCareers)))
            {
                if (monster == MonsterCareers.None)
                    continue;

                DFCareer career = DaggerfallEntity.GetMonsterCareerTemplate(monster);

                outputFile.WriteLine($"{career.Name};{career.HitPointsPerLevel};{career.Strength};{career.Intelligence}"
                    + $";{career.Willpower};{career.Agility};{career.Endurance};{career.Personality};{career.Speed};{career.Luck}"
                    + $";{career.PrimarySkill1};{career.PrimarySkill2};{career.PrimarySkill3};{career.MajorSkill1};{career.MajorSkill2};{career.MajorSkill3}"
                    + $";{career.MinorSkill1};{career.MinorSkill2};{career.MinorSkill3};{career.MinorSkill4};{career.MinorSkill5};{career.MinorSkill6}"
                    );
            }
        }

        return $"Careers dumped in {careersOutputFilename}";
    }

    string BlocksUsage = "print_blocks_using <model name>";

    string PrintBlocksUsingModel(params string[] args)
    {
        if (args.Length == 0)
            return "error: expected model name";

        string model = args[0];

        List<string> output = new List<string>();

        BsaFile blockBsa = DaggerfallUnity.Instance.ContentReader.BlockFileReader.BsaFile;
        for (int b = 0; b < blockBsa.Count; b++)
        {
            RMBLayout.GetBlockData(blockBsa.GetRecordName(b), out DFBlock blockData);
            if (blockData.Type != DFBlock.BlockTypes.Rmb)
                continue;

            bool added = false;
            foreach(DFBlock.RmbSubRecord subrecord in blockData.RmbBlock.SubRecords)
            {
                DFBlock.RmbBlock3dObjectRecord found = subrecord.Exterior.Block3dObjectRecords.FirstOrDefault(record => record.ModelId == model);
                if(!string.IsNullOrEmpty(found.ModelId))
                {
                    output.Add(blockData.Name);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                DFBlock.RmbBlock3dObjectRecord found = blockData.RmbBlock.Misc3dObjectRecords.FirstOrDefault(record => record.ModelId == model);
                if (!string.IsNullOrEmpty(found.ModelId))
                {
                    output.Add(blockData.Name);
                    continue;
                }
            }

        }

        if (output.Count == 0)
            return $"Model '{model}' is unused";

        return $"Model used in: {string.Join(", ", output)}";
    }
}
