using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects.Types;
using Realms;
using osu.Framework;

public class MyClass
{
    public static string osu_ver = @"osu-development"; //Roaming osu folder name
    public static ulong schema_version = 42; //realm version

    public static string MAIN_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), osu_ver);
    //public string DATATEXT_PATH => Path.Combine(MAIN_PATH, "RealmManager", "data.txt");
    protected string[] HashableFileTypes => new[] { ".osu" };
    ImportParameters parameters = default;
    CancellationToken cancellationToken = default;
    protected static RealmFileStore Files;
    private static void onMigration(Migration migration, ulong lastSchemaVersion) { }

    public static List<BeatmapInfo> createBeatmapDifficulties(BeatmapSetInfo beatmapSet, Realm realm)
    {
        var beatmaps = new List<BeatmapInfo>();

        foreach (var file in beatmapSet.Files.Where(f => f.Filename.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
        {
            string kek = MAIN_PATH + "\\files\\" + (file.File.GetStoragePath());
            if (!File.Exists(kek)) continue;
            using (var memoryStream = new MemoryStream(Files.Store.Get(file.File.GetStoragePath())))
            {
                IBeatmap decoded;

                using (var lineReader = new LineBufferedReader(memoryStream, true))
                {
                    if (lineReader.PeekLine() == null)
                    {
                        continue;
                    }

                    decoded = Decoder.GetDecoder<Beatmap>(lineReader).Decode(lineReader);
                }

                string hash = memoryStream.ComputeSHA2Hash();

                if (beatmaps.Any(b => b.Hash == hash))
                {
                    continue;
                }

                var decodedInfo = decoded.BeatmapInfo;
                var decodedDifficulty = decodedInfo.Difficulty;

                var ruleset = realm.All<RulesetInfo>().FirstOrDefault(r => r.OnlineID == decodedInfo.Ruleset.OnlineID);

                if (ruleset?.Available != true)
                {
                    continue;
                }

                var difficulty = new BeatmapDifficulty
                {
                    DrainRate = decodedDifficulty.DrainRate,
                    CircleSize = decodedDifficulty.CircleSize,
                    OverallDifficulty = decodedDifficulty.OverallDifficulty,
                    ApproachRate = decodedDifficulty.ApproachRate,
                    SliderMultiplier = decodedDifficulty.SliderMultiplier,
                    SliderTickRate = decodedDifficulty.SliderTickRate
                };

                var metadata = new BeatmapMetadata
                {
                    Title = decoded.Metadata.Title,
                    TitleUnicode = decoded.Metadata.TitleUnicode,
                    Artist = decoded.Metadata.Artist,
                    ArtistUnicode = decoded.Metadata.ArtistUnicode,
                    Author =
                        {
                            OnlineID = decoded.Metadata.Author.OnlineID,
                            Username = decoded.Metadata.Author.Username
                        },
                    Source = decoded.Metadata.Source,
                    Tags = decoded.Metadata.Tags,
                    PreviewTime = decoded.Metadata.PreviewTime,
                    AudioFile = decoded.Metadata.AudioFile,
                    BackgroundFile = decoded.Metadata.BackgroundFile,
                };

                var beatmap = new BeatmapInfo(ruleset, difficulty, metadata)
                {
                    Hash = hash,
                    DifficultyName = decodedInfo.DifficultyName,
                    OnlineID = decodedInfo.OnlineID,
                    BeatDivisor = decodedInfo.BeatDivisor,
                    MD5Hash = memoryStream.ComputeMD5Hash(),
                    EndTimeObjectCount = decoded.HitObjects.Count(h => h is IHasDuration),
                    TotalObjectCount = decoded.HitObjects.Count
                };

                beatmaps.Add(beatmap);
            }
        }

        //if (!beatmaps.Any()) throw new ArgumentException("No valid beatmap files found in the beatmap archive.");

        return beatmaps;
    }

    public static void ImportFromText(string pathToRealmFile, string pathToInputText)
    {
        var sourceConfig = new RealmConfiguration(pathToRealmFile)
        {
            SchemaVersion = schema_version,
            MigrationCallback = onMigration,
            FallbackPipePath = Path.Combine(Path.GetTempPath(), @"lazer"),
        };

        var realm = Realm.GetInstance(sourceConfig);

        var beatmapDiffList = new List<Tuple<List<string>, List<RealmFile>>>();
        if (!File.Exists(pathToInputText)) return;
        string[] lines = File.ReadAllLines(pathToInputText);

        var beatmapList = new List<Tuple<List<string>, List<RealmFile>>>();

        string osufile = "";
        string audiofile = "";
        string bgfile = "";
        string osuhash = "";
        string audiohash = "";
        string bghash = "";

        string sethash = "";

        foreach (var line in lines)
        {
            if (line.StartsWith("BEATMAPSETHASH:"))
            { sethash = line.Substring("BEATMAPSETHASH:".Length); continue; }
            var parts = line.Substring("__OSUNAME:".Length).Split(new[] { "__AUDIOFILE:", "__AUDIOHASH:" }, StringSplitOptions.None);

            if (parts.Length == 2)
            {
                if (parts[0].EndsWith(".osu"))
                {
                    osufile = "";
                    audiofile = "";
                    bgfile = "";
                    osuhash = "";
                    audiohash = "";
                    bghash = "";
                    Console.WriteLine(parts[0]); osufile = parts[0];
                    var p = parts[1].Split(new[] { "__BGFILE:" }, StringSplitOptions.None);
                    if (p[0] != "")
                    {
                        Console.WriteLine(p[0]);
                        audiofile = p[0];
                    }
                    if (p[1] != "")
                    {
                        Console.WriteLine(p[1]);
                        bgfile = p[1];
                    }
                    continue;
                }
                else
                {
                    Console.WriteLine(parts[0]); osuhash = parts[0];
                    var p = parts[1].Split(new[] { "__BGHASH:" }, StringSplitOptions.None);
                    if (p[0].Length > 5)
                    {
                        Console.WriteLine(p[0]);
                        audiohash = (p[0]);
                    }
                    if (p[1].Length > 5)
                    {
                        Console.WriteLine(p[1]);
                        bghash = (p[1]);
                    }

                }

            }
            else
            {
                return;
            }

            var g = new List<string>() { osufile, audiofile, bgfile, sethash };
            var f = new List<RealmFile>
                {
                    new RealmFile() { Hash = osuhash },
                    new RealmFile() { Hash = audiohash },
                    new RealmFile() { Hash = bghash }
                };
            beatmapList.Add(new Tuple<List<string>, List<RealmFile>>(g, f));

        }
        string currentHash = beatmapList[0].Item1[3];
        for (int slide = 0; slide < beatmapList.Count;)
        {
            var itss = beatmapList[slide];
            List<RealmNamedFileUsage> files = new List<RealmNamedFileUsage>();
            BeatmapSetInfo item = new BeatmapSetInfo();
            string tempPathArchidk = Path.Combine(Path.GetTempPath(), "a.osu");
            if (!System.IO.File.Exists(tempPathArchidk)) System.IO.File.Create(tempPathArchidk).Dispose();
            ArchiveReader arch = new SingleFileArchiveReader(tempPathArchidk);
            for (int i = 0; i < 3; i++)
            {
                if (itss.Item1[i].Length > 1)
                    files.Add(new RealmNamedFileUsage(itss.Item2[i], itss.Item1[i]));
            }
            currentHash = beatmapList[slide].Item1[3];
            slide++;
            if (!(slide == beatmapList.Count))
            {
                while (beatmapList[slide].Item1[3] == currentHash)
                {
                    itss = beatmapList[slide];
                    files.Add(new RealmNamedFileUsage(itss.Item2[0], itss.Item1[0]));
                    slide++;
                    if (slide == beatmapList.Count) break;
                }
            }
            using (var transaction = realm.BeginWrite())
            {
                foreach (var file in files)
                {
                    if (!file.File.IsManaged)
                        realm.Add(file.File, true);
                }

                transaction.Commit();
            }

            item.Files.AddRange(files);
            item.Hash = itss.Item1[3];

            using (var transaction = realm.BeginWrite())
            {
                if (arch != null)
                    item.Beatmaps.AddRange(createBeatmapDifficulties(item, realm));

                item.DateAdded = DateTimeOffset.UtcNow;

                foreach (BeatmapInfo b in item.Beatmaps)
                {
                    b.BeatmapSet = item;
                    if (!b.Ruleset.IsManaged)
                        b.Ruleset = realm.Find<RulesetInfo>(b.Ruleset.ShortName) ?? throw new ArgumentNullException(nameof(b.Ruleset));
                }
                bool hadOnlineIDs = item.Beatmaps.Any(b => b.OnlineID > 0);
                if (hadOnlineIDs && !item.Beatmaps.Any(b => b.OnlineID > 0))
                {
                    if (item.OnlineID > 0)
                    {
                        item.OnlineID = -1;
                    }
                }
                realm.Add(item);

                transaction.Commit();
            }
        }
    }
    //export C:\Users\Bodya\AppData\Roaming\osu-development\client_43.realm C:\ninja\gyes.txt
    public static void ExportToText(string pathToRealmFile, string pathToResultText)
    {
        var sourceConfig = new RealmConfiguration(pathToRealmFile)
        {
            SchemaVersion = schema_version,
            MigrationCallback = onMigration,
            FallbackPipePath = Path.Combine(Path.GetTempPath(), @"lazer"),
        };

        {
            try
            {
                var sourceRealm = Realm.GetInstance(sourceConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            using (var sourceRealm = Realm.GetInstance(sourceConfig))
            {
                var allObjectsBeatmapInfo = sourceRealm.All<BeatmapInfo>().ToList();

                List<string> thashes = new List<string>();
                foreach (var sourceObject in allObjectsBeatmapInfo)
                {
                    var mapSet = sourceObject.BeatmapSet;
                    if (mapSet == null) continue;
                    string? audiofile = mapSet.GetPathForFile(sourceObject.Metadata.AudioFile);
                    string? bgfile = mapSet.GetPathForFile(sourceObject.Metadata.BackgroundFile);
                    audiofile ??= "_____!NOAUDIOFILE";
                    bgfile ??= "_____!NOBGFILE";
                    using (StreamWriter writer = new StreamWriter(pathToResultText, append: true))
                    {
                        if (!thashes.Contains(mapSet.Hash))
                        {
                            IList<BeatmapInfo> beatmapInfObj = mapSet.Beatmaps;
                            int i = 0;
                            foreach (var beatmapObj in beatmapInfObj)
                            {
                                if (beatmapObj == null)
                                {
                                    writer.WriteLine("__OSUNAME:" + "__AUDIOFILE:" + "__BGFILE:");
                                    writer.WriteLine("__OSUHASH:" + "__AUDIOHASH:" + "__BGHASH:");
                                }
                                else
                                {
                                    string filename = beatmapObj.File == null ? "" : beatmapObj.File.Filename;
                                    if (i == 0) { thashes.Add(mapSet.Hash); writer.WriteLine("BEATMAPSETHASH:" + mapSet.Hash); i++; }
                                    writer.WriteLine("__OSUNAME:" + filename + "__AUDIOFILE:" + beatmapObj.Metadata.AudioFile + "__BGFILE:" + beatmapObj.Metadata.BackgroundFile);
                                    writer.WriteLine("__OSUHASH:" + beatmapObj.Hash + "__AUDIOHASH:" + audiofile.Substring(5) + "__BGHASH:" + bgfile.Substring(5));
                                }
                            }
                        }
                    }
                }
                sourceRealm.Dispose();
            }
        }
    }
    static void Main(string[] args)
    {
        var host = Host.GetSuitableDesktopHost("osu!RealmTools");
        Storage storage = new StableStorage(MAIN_PATH, host);
        RealmAccess realm = new RealmAccess(storage, "temp.realm");
        Files = new RealmFileStore(realm, storage);
        string command = args.Length > 0 ? args[0] : string.Empty;
        string pathRealm = args.Length > 1 ? args[1] : string.Empty;
        string pathFile = args.Length > 2 ? args[2] : string.Empty;
        ulong temp = 0;
        if (args.Length > 3 && !UInt64.TryParse(args[3], out temp)) temp = 0;
        if (temp > 1) schema_version = temp;

        osu_ver = args.Length > 4 && !string.IsNullOrEmpty(args[4]) ? args[4] : osu_ver;
        MAIN_PATH = args.Length > 5 && !string.IsNullOrEmpty(args[5]) ? args[5] : MAIN_PATH;
        if (command == null)
        {
            return;
        }
        if (command.Equals("import"))
        {
            if (File.Exists(args[1]) && File.Exists(args[2]))
            {
                ImportFromText(args[1], args[2]);
            }
            else
            {
                Console.WriteLine("File.Exists returned false for either .realm or data file.");
            }
            return;
        }
        if (command.Equals("export"))
        {
            if (args[1].EndsWith(".realm") && File.Exists(args[1]))
            {
                ExportToText(args[1], args[2]);
            }
            else
            {
                if (!args[1].EndsWith(".realm"))
                    Console.WriteLine("A file doesn't end with \".realm\".");
                if (!File.Exists(args[1]))
                    Console.WriteLine("File.Exists returned false");
            }

            return;
        }
        if (command == "quit")
        {
            return;
        }
        Console.WriteLine("end");
    }
}