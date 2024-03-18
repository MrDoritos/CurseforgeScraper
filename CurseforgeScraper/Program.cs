using System;

using Newtonsoft.Json;
using Newtonsoft;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace CurseforgeScraper
{
    public class Program
    {
        /*
         * Collect all games                        https://addons-ecs.forgesvc.net/api/v2/addon/game
         * Collect all categories for those games   categorySections/+/path
         * Subcategories are                        https://addons-ecs.forgesvc.net/api/v2/category/section/#
         *                                                                                /category/# <- gameCategoryId
         * List Sections (sub categories)                                                 /category/section/# <- gameCategoryId
         * Files                                                                          /addon/#/files
         * Addon info                                                                     /addon/#
         * File info (includes downloadUrl)                                               /addon/{addonID}/file/{fileID}
         * DownloadUrl                                                                    /addon/{addonID}/file/{fileID}/download-url
         * Changelog                                                                      /addon/{addonID}/file/{fileID}/changelog
         * Description                                                                    /addon/{addonID}/description
         * Searching (Page indexing)                                                      /addon/search?categoryId={categoryID}
         *                                                                                              &gameId={gameId}
         *                                                                                              &gameVersion={gameVersion}
         *                                                                                              &index={index}
         *                                                                                              &pageSize={pageSize}5
         *                                                                                              &searchFilter={searchFilter}
         *                                                                                              &sectionId={sectionId}
         *                                                                                              &sort={sort}
         *                                                                                              
         *  /addon/search?categoryId={categoryID}&gameId={gameId}&gameVersion={gameVersion}&index={index}&pageSize={pageSize}5&searchFilter={searchFilter}&sectionId={sectionId}&sort={sort}
         *  https://addons-ecs.forgesvc.net/api/v2/addon/search?categoryId=6&gameId=432&gameVersion=1.12.2&index=0&pageSize=25&searchFilter=ic2&sectionId=6&sort=0
         *  /addon/search?categoryId=0&gameId=432&gameVersion=1.12.2&index=0&pageSize=25&searchFilter=ultimate&sectionId=4471&sort=0
         *  https://addons-ecs.forgesvc.net/api/v2/addon/search?categoryId=0&sectionId=4471&gameId=432&index=1000&pageSize=25&sort=-1
         *  
         *  https://addons-ecs.forgesvc.net/api/v2/addon/search?categoryId=0&sectionId=4471&gameId=432&sort=-1
         *  https://addons-ecs.forgesvc.net/api/v2/addon/search?categoryId=0&gameId=432
         *  https://addons-ecs.forgesvc.net/api/v2/addon/search?gameId=432
         *  
         *  //returns mods
         *  https://addons-ecs.forgesvc.net/api/v2/addon/search?gameId=432&searchFilter=ic2&sort=0
         *  //returns 1 modpack
         *  https://addons-ecs.forgesvc.net/api/v2/addon/search?gameId=432&searchFilter=industrialcraft&sort=-1
         *  
         *  Example URLs for Minecraft, game 432
         *  View category 432 (buildcraft-addons) https://addons-ecs.forgesvc.net/api/v2/category/432
         *  6 (mc-mods) https://addons-ecs.forgesvc.net/api/v2/category/6
         *  LIST SECTIONS for 6 (mc-mods) https://addons-ecs.forgesvc.net/api/v2/category/section/6
         *  LIST GAMES https://addons-ecs.forgesvc.net/api/v2/game
         *  LIST ALL CATEGORIES https://addons-ecs.forgesvc.net/api/v2/category
         *  GET FILES FOR ADDON (304026) https://addons-ecs.forgesvc.net/api/v2/addon/304026/files
         *  GET FILE URL FOR ADDON (296062) and FILE ID (2724357) https://addons-ecs.forgesvc.net/api/v2/addon/296062/file/2724357/download-url
         *  GET FILE INFO https://addons-ecs.forgesvc.net/api/v2/addon/{addonID}/file/{fileID}
         *  GET ADDON INFO https://addons-ecs.forgesvc.net/api/v2/addon/310806
         *  
         *  https://addons-ecs.forgesvc.net /api/v2/addon/search?categoryId={categoryID}
         *                                                      &gameId={gameId}
         *                                                      &gameVersion={gameVersion}
         *                                                      &index={index}
         *                                                      &pageSize={pageSize}5
         *                                                      &searchFilter={searchFilter}
         *                                                      &sectionId={sectionId}
         *                                                      &sort={sort}
         *  
         *  
         *  Search returns 500 when no more pages are available
         *  
         *  curl --http1.1 -H "x-api-key: \$2a\$10\$bL4bIL5pUWqfcO7KQtnMReakwtfHbNKh6v1uTpKlzhwoueEJQnPnm" -H "Accept: application/json" -H "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.141 Safari/537.36 OverwolfClient/0.204.0.1" -H "Authorization: OAuth" -H "X-Twitch-Id:" -H "Accept-Encoding: gzip" "https://api.curseforge.com/v1/mods/367635" -X GET -o - | gunzip
         *  
         *  CHANGES
         *  https://api.curseforge.com/v1/mods
         *  https://api.curseforge.com/v1/mods/367635
         *  238222
         *  
         */

        public static StreamWriter LinkFile
            = new StreamWriter(File.Open("cdnLinks.txt", FileMode.Append, FileAccess.Write, FileShare.Read));

        public static StreamWriter IdFile
            ;//= new StreamWriter(File.Open("addonIdFile.txt", FileMode.Append, FileAccess.Write, FileShare.Read));

        public static StreamWriter ErrorFile
            = new StreamWriter(File.Open("errorLog.txt", FileMode.Append, FileAccess.Write, FileShare.Read));

        public static bool debugMode = false;

        public static WebExt APIClient
            = new WebExt();

        public static WebClient CDNClient
            = new WebClient();

        public static Random gRandom
            = new Random();

        public static int getRandomWaitTime()
        {
            float x = (float)gRandom.NextDouble();
            return (int)((x * (float)ScraperRules.TimeoutSize) + (float)(ScraperRules.TimeoutMin));
            x = -((x - 0.5f) * (x - 0.5f));
            x *= 4;
            int mx = 0 - 5;
            x *= mx;
            x += 2.0f;
            return (int)x;
        }

        public static JsonReader GetJsonReaderFromFile(string path)
        {
            path = path.TrimStart('/').Replace('/', '\\');

            Console.Write($"Finding file \"{path}\".. ");
            try
            {
                var stream = File.OpenRead(path);
                StreamReader sr = new StreamReader(stream);
                JsonReader jr = new JsonTextReader(sr);

                Console.WriteLine("OK.");
                return jr;
            }
            catch (JsonReaderException)
            {
                Console.WriteLine("Json Exception.");
            }
            catch
            {
                Console.WriteLine("Fail.");
            }
            return null;
        }

        public static JArray GetJsonArray(string url)
        {
            return JArray.Load(GetJsonReader(url));
        }

        public static JObject GetJsonObject(string url)
        {
            return JObject.Load(GetJsonReader(url));
        }

        public static T GetJsonDeserializer<T>(string url)
        {
            JsonReader r = GetJsonReader(url);

            return (new JsonSerializer()).Deserialize<T>(r);
        }

        public static JsonReader GetJsonReader(string url)
        {
            var stream = GetStream(url);
            var sr = new StreamReader(stream);
            var jr = new JsonTextReader(sr);
            return jr;
        }
        [Serializable]
        public class WebExt : WebClient
        {
            public WebExt()
                :base()
            {
                Headers.Clear();

                Headers.Add("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.141 Safari/537.36 OverwolfClient/0.204.0.1");

                Headers.Add("Accept: application/json");
                Headers.Add("Accept-Encoding: gzip");

                Headers.Add("x-api-key: $2a$10$bL4bIL5pUWqfcO7KQtnMReakwtfHbNKh6v1uTpKlzhwoueEJQnPnm");
                Headers.Add("Authorization: OAuth");
                Headers.Add("X-Twitch-Id:");
            }
            public WebRequest GetRequest(Uri url) => base.GetWebRequest(url);
        }

        public static Stream GetStream(string url, int tries = 0)
        {
            int randomWaitTime = getRandomWaitTime() + (tries > 0 ? (int)(Math.Pow(30000.0d, tries * 0.065d + 0.90d)) : 0);
            Console.Write("Sleeping for {0}ms...", randomWaitTime);
            while (DateTime.Now.Subtract(ScraperData.LastRequest).TotalMilliseconds < randomWaitTime)
                Thread.Sleep((randomWaitTime) - (int)DateTime.Now.Subtract(ScraperData.LastRequest).TotalMilliseconds);
            Console.Write("Making request to {0}...", url);

            try
            {
                ScraperData.RequestCount++;
                ScraperData.LastRequest = DateTime.Now;
                Uri uri = new Uri(url);
                HttpWebRequest request = (HttpWebRequest)APIClient.GetRequest(uri);
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                var response = (HttpWebResponse)request.GetResponse();
                var stream = response.GetResponseStream();

                ScraperData.LastRequest = DateTime.Now;

                Console.WriteLine(" Done.");
                return stream;
            }
            catch (WebException e)
            {
                try 
                {
                    if ((e != null && e.Response as HttpWebResponse != null) && ((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.Forbidden || (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.NotFound))
                    {
                        Console.WriteLine(" {0}.", (HttpWebResponse)e.Response);
                        ErrorFile.WriteLine($"[{DateTime.Now}] Response {((HttpWebResponse)e.Response).StatusCode} from url {url}");
                        throw e;
                    }
                } 
                catch (Exception es) 
                {
                    string errstr2 = $"[{DateTime.Now}] Network failed for reason: {es.Message} {es.StackTrace}";
                    Console.WriteLine(errstr2);
                    ErrorFile.WriteLine(errstr2);                    
                    var innerException2 = es.InnerException;
                    while (innerException2 != null)
                    {
                        Console.WriteLine(innerException2.Message);
                        innerException2 = innerException2.InnerException;
                    }
                }

                int retryLimit = 3;

                string errstr = $"[{DateTime.Now}] Network failed for reason: {e.Message} {e.Response}";
                Console.WriteLine(errstr);
                ErrorFile.WriteLine(errstr);

                var innerException = e.InnerException;
                while (innerException != null)
                {
                    Console.WriteLine(innerException.Message);
                    innerException = innerException.InnerException;
                }

                if (tries <= retryLimit)
                {
                    return GetStream(url, tries + 1);
                }
                if (tries > retryLimit)
                {
                    Console.WriteLine($"Could not download page: {url} after {retryLimit} tries");
                    throw e;
                }
                else
                {
                    Console.WriteLine("Would you like to retry this request? ^C to terminate program. Saved database just in case");
                    WriteJson("save.error.json", ScraperData);
                    Console.ReadKey(true);
                    Console.WriteLine("Retrying...");

                    return GetStream(url, 0);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /* Classes for json api */
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Game
        {
            //Json
            public int id;
            public string name;
            public string slug;
            public DateTime dateModified;
            public GameFile[] gameFiles;
            public GameDetectionHint[] gameDetectionHints;
            public FileParsingRule[] fileParsingRules;
            public Section[] categorySections;
            public int maxFreeStorage;
            public int maxPremiumStorage;
            public int maxFileSize;
            public string addonSettingFolderFilter;
            public string addonSettingStartingFolder;
            public string addonSettingFileFilter;
            public string addonSettingFileRemovalFilter;
            public bool supportsAddons;
            public bool supportsPartnerAddons;
            public int supportedClientConfiguration;
            public bool supportsNotifications;
            public int profilerAddonId;
            public int twitchGameId;
            public int clientGameSettingsId;

            //Additional

        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Link
        {
            public string websiteUrl;
            public string wikiUrl;
            public string issuesUrl;
            public string sourceUrl;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Addon
        {
            public Addon()
            {
                links = new Link();
            }

            public int id;
            public string name;
            public Author[] authors;
            public Attachment[] attachments;
            public Attachment[] screenshots;
            [JsonIgnore]
            public IEnumerable<Attachment> Images
            {
                get
                {
                    var list = new List<Attachment>();
                    if ((attachments?.Length ?? 0) > 0)
                        list.AddRange(attachments);
                    if ((screenshots?.Length ?? 0) > 0)
                        list.AddRange(screenshots);
                    return list;
                }
            }
            public Attachment logo;
            public Link links;
            [JsonIgnore]
            public string wikiUrl => links.wikiUrl;
            [JsonIgnore]
            public string sourceUrl => links.sourceUrl;
            [JsonIgnore]
            public string websiteUrl => links.websiteUrl;
            [JsonIgnore]
            public string issueTrackerUrl => links.issuesUrl;
            public int gameId;
            public string summary;
            public int defaultFileId;
            public int mainFileId;
            [JsonConverter(typeof(StrictIntConverter))]
            public int downloadCount;
            public AddonFile[] latestFiles;
            public int[] _latestFiles;
            public int[] addonFiles;
            public Category[] categories;
            public int[] _categories;
            public int status;
            public int primaryCategoryId;
            public string description;
            //categorysection
            public string slug;
            //public AddonFile[] gameVersionLatestFiles;
            //skip latestFileIndexes
            public bool isFeatured;
            public double popularityScore;
            public int gamePopularityRank;
            public string primaryLanguage;
            public string gameSlug;
            public string[] modLoaders;
            public string gameName;
            public string portalName;
            public DateTime dateModified;
            public DateTime dateCreated;
            public DateTime dateReleased;
            public DateTime dateScraped;
            public bool allowModDistribution;
            public bool isAvailable;
            public bool isExperiemental;
            public int classId;
            public int thumbsUpCount;
            public bool sourceAvailable;

            //ScraperFlags
            public bool scraped;
            public enum ScrapeInfoEnum
            {
                EMPTY = 0,
                SEARCH = 1,
                FILES = 2,
                DESCRIPTION = 4,
                ICON = 8,
                IMAGES = 16,
                SOURCE = 32,
                INFOFILES = 64,
                COMPLETE = EMPTY | SEARCH | FILES | DESCRIPTION | ICON | IMAGES | SOURCE | INFOFILES
            };
            public ScrapeInfoEnum scrapeInfo;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Author
        {
            public string name;
            public string url;
            public int projectId;
            public int id;
            public int projectTitleId;
            public string projectTitleTitle;
            public long userId;
            public long twitchId;
        }

        //Also section
        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Category
        {
            public int id;
            public string name;
            public string slug;
            public string url;
            public string iconUrl;
            public string avatarUrl;

            public DateTime dateModified;
            public int parentGameCategoryId = -1;
            public int parentCategoryId;
            public int rootGameCategoryId;
            public int gameId; //
            public int scrapedIndex;

            public bool isClass;
            public int classId;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Section
        {
            public int packageType;
            public string path;
            public string initialInclusionPattern;
            public string extraIncludePattern;
            public int gameCategoryId;
            public int id;
            public string name;
            public string slug;
            public string avatarUrl;
            public DateTime dateModified;
            public int parentGameCategoryId;
            public int rootGameCategoryId;
            public int gameId;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class FileParsingRule
        {
            public string commentStripPattern;
            public string fileExtension;
            public string inclusionPattern;
            public int gameId;
            public int id;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class GameDetectionHint
        {
            public int id;
            public int hintType;
            public string hintPath;
            public string hintKey;
            public int hintOptions;
            public int gameId;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class SortableGameVersion
        {
            public string gameVersionName;
            public string gameVersionPadded;
            public string gameVersion;
            public DateTime gameVersionReleaseDate;
            public int gameVersionTypeId;

            public override bool Equals(object obj)
            {
                if (obj is null || !(obj is SortableGameVersion))
                    return false;

                SortableGameVersion other = (SortableGameVersion)obj;
                return this == other;
            }

            public override int GetHashCode()
            {
                return gameVersion.GetHashCode();
            }

            public static bool operator ==(SortableGameVersion a, SortableGameVersion b)
            {
                return a.gameVersion == b.gameVersion;
            }

            public static bool operator !=(SortableGameVersion a, SortableGameVersion b) => !(a == b);

            public override string ToString()
            {
                return gameVersion;
            }
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Module
        {
            public string foldername;
            public string name;
            public long fingerprint;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class GameFile
        {
            public int id;
            public int gameId;
            public bool isRequired;
            public string fileName;
            public int fileType;
            public int platformType;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Attachment
        {
            public int id;
            public int projectId;
            public int modId;
            public string description;
            public bool isDefault;
            public string thumbnailUrl;
            public string title;
            public string url;
            public int status;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Hash
        {
            public string value;
            public int algo;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Dependency
        {
            public int modId;
            public int relationType;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class AddonFile
        {
            public int id;
            public string displayName;
            public string fileName;
            public DateTime fileDate;
            public long fileLength;
            public int releaseType;
            public int fileStatus;
            public string downloadUrl;
            public bool isAlternate;
            public int alternateFileId;
            public int modId;
            public int baseAddonId;
            public int gameId;
            public int downloadCount;
            public SortableGameVersion[] sortableGameVersions;
            public Hash[] hashes;
            public Dependency[] dependencies;
            public bool isAvailable;
            public bool isServerPack;
            public Module[] modules;
            public long packageFingerprint;
            public long fileFingerprint;
            public string[] gameVersion;
            public string[] gameVersions;
            //installMetadata
            public int serverPackFileId;
            public bool hasInstallScript;
            public DateTime gameVersionDateReleased;
            //gameVersionFlavor
            public string changelog;
            public enum ScrapeInfoEnum
            {
                EMPTY = 0,
                CHANGELOG = 1,
                DOWNLOADED = 2,
                COMPLETE = EMPTY | CHANGELOG | DOWNLOADED
            };
            public ScrapeInfoEnum scrapeInfo;
            public bool dl;
            [JsonIgnore]
            public bool Downloaded { get => dl; set => dl = value; }
            [JsonIgnore]
            public string FileName { get => fileName; }
            [JsonIgnore]
            public string DownloadUrl { get => downloadUrl; }
            [JsonIgnore]
            public string MCVersion
            {
                get
                {
                    return (gameVersion?.FirstOrDefault(n => n.Length > 0)) ?? (gameVersions?.FirstOrDefault(n => n.Length > 0)) ?? "undef";
                }
            }
        }

        public class Pagination
        {
            public int index;
            public int pageSize;
            public int resultCount;
            public int totalCount;
            public float PageCount { get => (float)totalCount / (float)pageSize; }
            public float Completion { get => index == 0 ? 0 : (float)index / (float)totalCount; }
            public float CurrentPage { get => (float)index / (float)pageSize; }
        }

        public class Container<T>
        {
            public T data;
            public Pagination pagination;
        }

        public class ScraperData_
        {
            public int RequestCount;
            public DateTime LastRequest = DateTime.Now.AddSeconds(-60);
            public Game[] Games = null;
            public Category[] Categories = null;
            /// <summary>
            /// ProjectId
            /// AddonId
            /// </summary>
            public Dictionary<int, Addon> Addons
                = new Dictionary<int, Addon>();
            public Dictionary<int, AddonFile> AddonFiles
                = new Dictionary<int, AddonFile>();
            public HashSet<SortableGameVersion> SortableGameVersions
                = new HashSet<SortableGameVersion>();

            public int lastPageIndex = 0;

            [JsonIgnore]
            public int LastAddonId { get => (Addons.Count > 0 ? Addons.Last().Value.id : -1); }
        }

        public static ScraperData_ ScraperData;

        public static string GetAPIUrl(string url = "")
        {
            //return "https://addons-ecs.forgesvc.net/api/v2" + url;
            return "https://api.curseforge.com/v1" + url;
        }

        private static void WriteJson<T>(string path, T data)
        {
            Console.WriteLine($"Saving json to {path} without a lock");
            using (FileStream fstream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1048576))
            using (StreamWriter sw = new StreamWriter(fstream))
            using (JsonWriter jw = new JsonTextWriter(sw))
            {
                JsonSerializer jsonSerializer = new JsonSerializer();
                jsonSerializer.DefaultValueHandling = DefaultValueHandling.Ignore;
                jsonSerializer.Serialize(jw, data);
            }
            Console.WriteLine("Saved.");
        }

        private static void SaveState<T>(string path, T data)
        {
            Console.WriteLine("Waiting for lock on database... ");
            Console.WriteLine($"Saving json to {path}");
            lock (data)
            {
                using (FileStream fstream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1048576))
                using (StreamWriter sw = new StreamWriter(fstream))
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    jsonSerializer.DefaultValueHandling = DefaultValueHandling.Ignore; // Save space
                    {
                        jsonSerializer.Serialize(jw, data);
                    }
                }
            }
            Console.WriteLine("Saved.");
        }

        public static bool RunSaveThread;

        public static void SaveThread()
        {
            Console.WriteLine("Autosaving enabled.");
            int current = 0;
            while (RunSaveThread)
            {
                try
                {

                    Thread.Sleep(ScraperRules.AutoSaveInterval);
                    Console.Write("Saving...");
                    SaveState($"scraper.data.auto.{current++ % 3}.json", ScraperData);
                    Console.WriteLine("Saved.");
                }
                catch (ThreadInterruptedException e)
                {
                    return;
                }
                catch
                {

                }
            }
        }
        public static string PathCombine(string path1, string path2)
        {
            if (Path.IsPathRooted(path2))
            {
                path2 = path2.TrimStart(Path.DirectorySeparatorChar);
                path2 = path2.TrimStart(Path.AltDirectorySeparatorChar);
            }

            return Path.Combine(path1, path2);
        }
        public static class ScraperRules
        {
            public static bool SkipDescription;
            public static bool SkipAddonFiles;
            public static bool SkipChangelog;
            public static bool SkipAddonInfo;
            public static bool SkipWriteInfoFile;
            public static bool SkipImages;
            public static bool FollowScrapeFlag;
            public static bool SkipDownloadJars;
            public static bool SkipPagesScraped;
            public static bool ImmediatelyScrapeFiles;
            public static string OutputLocation;
            public static string gitPath, magickPath, path7z;
            public static string InputFile, AddonIdFile;
            public static bool RetrieveSource;
            public static bool CreateIcon;
            public static bool IterateCategories;
            public static int TimeoutMin = 100, TimeoutSize = 500, SearchIndex = -1, SearchIndexEnd = -1, AutoSaveInterval, SearchLength, RepeatInterval;
            public static bool AutoBackup, DistributeFolders, SmartScrape, DontAutoSave;
            public static string SearchString;
            public static bool Repeat;
            public static bool PageOfNoDataEnd;
        }

        public static void ProcessCommandLineArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "help": Console.WriteLine(@"Available Options: 
Default values unless specified otherwise

Default values are my scrape settings

bool SkipDescription    -   Skip scraping description
bool SkipAddonFiles     -   Unused
bool SkipChangelog      -   Skip scraping AddonFile changelog
bool SkipAddonInfo      -   Unused
bool SkipWriteInfoFile  -   Skip writing info files: .txt, .json, .html
bool SkipImages         -   Skip downloading images
-> bool FollowScrapeFlag-   Follow the scrape flags.. Enabling this saves time but reduces detail upon repeated scrapes. In short, disable when Repeat is enabled
bool SkipDownloadJars   -   Skip downloading jars
bool SkipPagesScraped   -   Unused
-> bool ImmediatelyScrapeFiles Scrape information and files immediately after learning about them disable to scrape AddonFile main page only
string OutputLocation   -   By default: The working directory of this program (" + ScraperRules.OutputLocation + @")
string gitPath          -   By default: git.exe
string magickPath       -   By default: magick.exe (Note on linux, imagemagick does not have a magick program, instead it is just convert)
string 7zPath           -   By default: 7z.exe
-> string InputFile     -   By default: scraper.data.json
string AddonIdFile      -   Empty by default. Overrides search and uses a file containing ids instead.
bool RetrieveSource     -   Retrieve the source code and put it in a tarball. Requires git and 7z
bool CreateIcon         -   Retrieve the Addon logo and create a folder icon (.ico) with desktop.ini. Requires imagemagick
int TimeoutMin          -   Minimum timeout time in milliseconds. 1000ms by default
int TimeoutSize         -   Span of the timeout time in milliseconds. 1000ms by default. y=mx+b timeout=TimeoutSize * (0.0-1.0) + TimeoutMin
int SearchIndex         -   Start index of the search. Default value is 0 or lastPageIndex in database (in terms of addon count, not page count)
int SearchIndexEnd      -   End index of the search. Default value is the number of addons in the search query (in terms of addon count, not page count)
int SearchLength        -   Results per page. Default is 20
-> bool IterateCategories   Iterate all the categories for the current game, rather than using category 0. Important for 10000 entry limit. False by default
bool AutoBackup         -   Automatically backup the InputFile. Requires 7z
bool DontAutoSave       -   Skip Automatically AutoSaving
int AutoSaveInterval    -   Autosave interval. 600000ms by default
int RepeatInterval      -   Repeat interval. 300000ms by default
bool DistributeFolders  -   Unused
bool SmartScrape        -   Unused
string SearchString     -   Search query
    {0} - Page size in items
    {1} - Index in items, not pages
    {2} - IterateCategories
    default value - ""/mods/search?categoryId={2}&classId=6&gameId=432&pageSize={0}&sortField=4&index={1}\""
    sortField : 1=Featured  2=Popularity  3=LastUpdated  4=Name  5=Author  6=TotalDownloads  7=Category  8=GameVersion
    sortOrder : asc=ascending  desc=descending
    gameId : 432=minecraft
    classId : 6=mc-mods
    categoryId : 0=all
bool Repeat             -   Repeat the program after reaching SearchIndexEnd indefinitely
bool PageOfNoDataEnd    -   Reset SearchIndex after receiving a page of no new information

Example command
dotnet ./CurseforgeScraper.dll TimeoutMin 750 TimeoutSize 500 ImmediatelyScrapeFiles FollowScrapeFlag CreateIcon RetrieveSource
"); Environment.Exit(0); break;
                    case "SkipDescription": ScraperRules.SkipDescription = true; break;
                    case "SkipAddonFiles": ScraperRules.SkipAddonFiles = true; break;
                    case "SkipChangelog": ScraperRules.SkipChangelog = true; break;
                    case "SkipAddonInfo": ScraperRules.SkipAddonInfo = true; break;
                    case "SkipWriteInfoFile": ScraperRules.SkipWriteInfoFile = true; break;
                    case "SkipImages": ScraperRules.SkipImages = true; break;
                    case "FollowScrapeFlag": ScraperRules.FollowScrapeFlag = true; break;
                    case "SkipDownloadJars": ScraperRules.SkipDownloadJars = true; break;
                    case "SkipPagesScraped": ScraperRules.SkipPagesScraped = true; break;
                    case "ImmediatelyScrapeFiles": ScraperRules.ImmediatelyScrapeFiles = true; break;
                    case "OutputLocation": if (i + 1 < args.Length) ScraperRules.OutputLocation = args[i + 1]; break;
                    case "gitPath": if (i + 1 < args.Length) ScraperRules.gitPath = args[i + 1]; break;
                    case "magickPath": if (i + 1 < args.Length) ScraperRules.magickPath = args[i + 1]; break;
                    case "7zPath": if (i + 1 < args.Length) ScraperRules.path7z = args[i + 1]; break;
                    case "RetrieveSource": ScraperRules.RetrieveSource = true; break;
                    case "CreateIcon": ScraperRules.CreateIcon = true; break;
                    case "TimeoutMin": if (i + 1 < args.Length) int.TryParse(args[i + 1], out ScraperRules.TimeoutMin); break;
                    case "TimeoutSize": if (i + 1 < args.Length) int.TryParse(args[i + 1], out ScraperRules.TimeoutSize); break;
                    case "InputFile": if (i + 1 < args.Length) ScraperRules.InputFile = args[i + 1]; break;
                    case "AddonIdFile": if (i + 1 < args.Length) ScraperRules.AddonIdFile = args[i + 1]; break;
                    case "AutoBackup": ScraperRules.AutoBackup = true; break;
                    case "AutoSaveInterval": if (i + 1 < args.Length) int.TryParse(args[i + 1], out ScraperRules.AutoSaveInterval); break;
                    case "RepeatInterval": if (i + 1 < args.Length) int.TryParse(args[i + 1], out ScraperRules.RepeatInterval); break;
                    case "DontAutoSave": ScraperRules.DontAutoSave = true; break;
                    case "SearchIndex": if (i + 1 < args.Length) int.TryParse(args[i + 1], out ScraperRules.SearchIndex); break;
                    case "SearchIndexEnd": if (i + 1 < args.Length) int.TryParse(args[i + 1], out ScraperRules.SearchIndexEnd); break;
                    case "SearchLength": if (i + 1 < args.Length) int.TryParse(args[i + 1], out ScraperRules.SearchLength); break;
                    case "IterateCategories": if (i + 1 < args.Length) ScraperRules.IterateCategories = true; break;
                    case "DistributeFolders": ScraperRules.DistributeFolders = true; break;
                    case "SmartScrape": ScraperRules.SmartScrape = true; break;
                    case "SearchString": if (i + 1 < args.Length) ScraperRules.SearchString = args[i + 1]; break;
                    case "Repeat": ScraperRules.Repeat = true; break;
                    case "PageOfNoDataEnd": ScraperRules.PageOfNoDataEnd = true; break;
                }
            }
        }

        public class ModIdList
        {
            public List<int> modIds = new List<int>();
        }

        public static Container<Addon[]> GetModList(ModIdList list)
        {
            try
            {
                string urlreq = GetAPIUrl("/mods");
                //WebExt cpy = APIClient.DeepClone();

                string todeserialize = null;
                Container<Addon[]> ret = null;

                HttpWebRequest req = (HttpWebRequest)APIClient.GetRequest(new Uri(urlreq));
                req.ContentType = "application/json";
                req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                req.Accept = "application/json";
                req.Method = "POST";

                using (StringWriter sw = new StringWriter())
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.DefaultValueHandling = DefaultValueHandling.Ignore;
                    serializer.Serialize(jw, list);

                    Console.WriteLine(todeserialize = sw.ToString());
                }

                req.ContentLength = todeserialize?.Length ?? 0;

                using (StreamWriter sw = new StreamWriter(req.GetRequestStream()))
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.DefaultValueHandling = DefaultValueHandling.Ignore;
                    serializer.Serialize(jw, list);
                    jw.Flush();
                    sw.Flush();


                    var response = (HttpWebResponse)req.GetResponse();
                    var stream = response.GetResponseStream();

                    using (StreamReader sr = new StreamReader(stream))
                    using (JsonReader jr = new JsonTextReader(sr))
                    {
                        JsonSerializer deserializer = new JsonSerializer();
                        ret = deserializer.Deserialize<Container<Addon[]>>(jr);
                    }
                }
                return ret;
            }
            catch
            {
                Console.WriteLine("GetModList Failure. Retry 15s");
                System.Threading.Thread.Sleep(15000);
                return GetModList(list);
            }
        }

        public static void Main(string[] args)
        {
            ScraperData = new ScraperData_();
            ScraperRules.OutputLocation = Environment.CurrentDirectory;
            ScraperRules.gitPath = "git";
            ScraperRules.path7z = "7z";
            ScraperRules.magickPath = "magick";
            //ScraperRules.TimeoutMin = 1000;
            //ScraperRules.TimeoutSize = 1000;
            ScraperRules.InputFile = "scraper.data.json";
            ScraperRules.SearchLength = 20;
            ScraperRules.SearchString = "/mods/search?categoryId=0&classId=6&gameId=432&pageSize={0}&sortField=4&index={1}";
            ScraperRules.AutoSaveInterval = 600000;
            ScraperRules.RepeatInterval = 300000;

            //For debugging
            
            //ScraperRules.CreateIcon = true;
            //ScraperRules.FollowScrapeFlag = true;
            //ScraperRules.ImmediatelyScrapeFiles = true;
            //ScraperRules.RetrieveSource = true;
            //ScraperRules.SearchIndexEnd = 200;
            //ScraperRules.SearchIndex = 0;

            //ScraperRules.AddonIdFile = "idlist.txt";


            ProcessCommandLineArgs(args);

            IdFile = new StreamWriter(File.Open("addonIdFile", FileMode.Append, FileAccess.Write, FileShare.Read));

            if (ScraperRules.AutoBackup && File.Exists(ScraperRules.InputFile))
            {
                var file = new FileInfo(ScraperRules.InputFile);
                ZIPPER7.Run7z($"{file.LastWriteTimeUtc.ToString("yyyyMMddTHHmmss")}.scraper.data.backup", file.FullName, new DirectoryInfo(ScraperRules.OutputLocation), true);
            }

            if (File.Exists(ScraperRules.InputFile))
            {
                using (var streamreader = File.OpenText(ScraperRules.InputFile))
                using (var json = new JsonTextReader(streamreader))
                {
                    ScraperData = (new JsonSerializer()).Deserialize<ScraperData_>(json);
                }
            }

            if (ScraperRules.SearchIndex < 0)
                ScraperRules.SearchIndex = ScraperData.lastPageIndex;

            ScraperData.lastPageIndex = ScraperRules.SearchIndex;

            LinkFile.AutoFlush = true;
            ErrorFile.AutoFlush = true;
            IdFile.AutoFlush = true;

            ErrorFile.WriteLine($"[{DateTime.Now}] Begin log");

            var saveThread = new Thread(SaveThread);
            if (!ScraperRules.DontAutoSave)
            {
                RunSaveThread = true;
                saveThread.Start();
            }

            Console.CancelKeyPress += delegate
            {
                try
                {
                    lock(ScraperData)
                        WriteJson($"{DateTime.Now.ToString("yyyyMMddTHHmmss")}.scraper.data.cancel.json", ScraperData);
                    Environment.Exit(0);
                }
                catch
                {
                    Console.WriteLine("Save failed. Try Ctrl-C sequence again");
                }
            };

            //Set up client
            var handler = new HttpClientHandler();

            handler.ServerCertificateCustomValidationCallback += 
                (sender, certificate, chain, errors) =>
                {
                    return true;
                };

            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) =>
            {
                return true;
            };

            //Capture all games
            if ((ScraperData.Games?.Length ?? 0) < 1) //list of games unlikely to increase
                ScraperData.Games = GetJsonDeserializer<Container<Game[]>>(GetAPIUrl("/games")).data;

            //Capture all categories
            if ((ScraperData.Categories?.Length ?? 0) < 1) //categories unlikely to change
                ScraperData.Categories = GetJsonDeserializer<Container<Category[]>>(GetAPIUrl("/categories?gameId=432")).data;//requires gameId to be specified. temporarily minecraft

            int numberSkipped = 0;
            int categoryIndex = 0;

            bool useIdListFile = (ScraperRules.AddonIdFile?.Length ?? 0) > 0;
            List<int> idsFromFile = new List<int>();
            if (File.Exists(ScraperRules.AddonIdFile))
                idsFromFile.AddRange(File.ReadAllLines(ScraperRules.AddonIdFile).Select(n => int.Parse(n)));

            while (true)
            {
                //Search
                int searchLength = ScraperRules.SearchLength;
                Addon[] addons = null;
                Container<Addon[]> searchResult = null;

                //If all results on a page have no data, reset position
                if (numberSkipped == searchLength && ScraperRules.PageOfNoDataEnd)
                {
                    Console.WriteLine($"Waiting {ScraperRules.RepeatInterval}ms before repeating because of PageOfNoDataEnd");
                    Thread.Sleep(ScraperRules.RepeatInterval);
                    string li = $"[{DateTime.Now}] Repeating";
                    Console.WriteLine(li);
                    ErrorFile.WriteLine(li);

                    ScraperData.lastPageIndex = 0;
                }

                numberSkipped = 0;

                //Preform a search
                try
                {
                    if (useIdListFile)
                    { // Id List mode
                        ModIdList ids = new ModIdList();
                        var currentPage = idsFromFile.Skip(ScraperData.lastPageIndex).Take(searchLength);
                        searchResult = new Container<Addon[]>();
                        var pg = searchResult.pagination = new Pagination();
                        pg.pageSize = searchLength;
                        pg.index = ScraperData.lastPageIndex;
                        pg.totalCount = idsFromFile.Count;
                        pg.resultCount = 0;

                        if (currentPage.Count() < 1)
                            goto skipGet;

                        ids.modIds.AddRange(currentPage); //ids for query

                        searchResult = GetModList(ids); //request addon info from api
                        addons = searchResult.data;

                        pg = searchResult.pagination = new Pagination();
                        pg.pageSize = searchLength;
                        pg.index = ScraperData.lastPageIndex;
                        pg.totalCount = idsFromFile.Count;
                        pg.resultCount = searchResult.data.Length;
                    }
                    else
                    { // Search mode
                        StringBuilder apiUrl = new StringBuilder();
                        int categoryId = (ScraperRules.IterateCategories ? ScraperData.Categories[categoryIndex].id : 0);
                        apiUrl.AppendFormat(ScraperRules.SearchString, searchLength, ScraperData.lastPageIndex, categoryId);
                        //searchResult = GetJsonDeserializer<Container<Addon[]>>(GetAPIUrl($"/mods/search?categoryId=0&classId=6&gameId=432&pageSize={searchLength}&sortField=4&index={ScraperData.lastPageIndex}"));
                        searchResult = GetJsonDeserializer<Container<Addon[]>>(GetAPIUrl(apiUrl.ToString()));
                        addons = searchResult.data;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is WebException)
                    {
                        var webex = ex as WebException;
                        var res = webex.Response as HttpWebResponse;
                        string err = $"[{DateTime.Now}] HTTP Returns [{(res?.StatusCode ?? 0)}] ({webex.Status.ToString()}) {webex.Message}\r\n{webex.StackTrace}";
                        Console.WriteLine(err);
                        ErrorFile.WriteLine(err);
                        Console.ReadKey(true);

                    }
                    else
                    {
                        string err = $"[{DateTime.Now}] {ex.Message}\r\n{ex.StackTrace}";
                        Console.WriteLine(err);
                        ErrorFile.WriteLine(err);
                        Console.ReadKey(true);
                    }
                }

                skipGet:;

                if (searchResult == null)
                    continue;

                Console.WriteLine($"[{(int)(searchResult.pagination.Completion * 10000.0f) / (float)100.0f}%] ( {searchResult.pagination.CurrentPage} / {searchResult.pagination.PageCount} ) pages ( {searchResult.pagination.index} / {searchResult.pagination.totalCount} )");

                //If we reach end of iteration
                int totalItemCount = (searchResult.pagination.totalCount < ScraperRules.SearchIndexEnd)
                     ? searchResult.pagination.totalCount 
                     : (ScraperRules.SearchIndexEnd < 0 ? 
                     searchResult.pagination.totalCount : 
                     ScraperRules.SearchIndexEnd);
                if (ScraperData.lastPageIndex >= totalItemCount || ScraperData.lastPageIndex >= 10000) //New page limit
                {
                    ErrorFile.WriteLine($"[{DateTime.Now}] End of iteration [{ScraperData.lastPageIndex},{totalItemCount},{searchLength},{searchResult.pagination.totalCount},{ScraperRules.SearchIndexEnd}]");
                    Console.WriteLine("Possibly the end of iteration..");

                    if (ScraperRules.IterateCategories) {
                        Console.WriteLine($"End of this category, next category");
                        ScraperData.lastPageIndex = 0;
                        categoryIndex++;
                        continue;
                    }

                    if (ScraperRules.Repeat)
                    {
                        Console.WriteLine($"Waiting {ScraperRules.RepeatInterval}ms seconds before repeating because of Repeat");
                        Thread.Sleep(ScraperRules.RepeatInterval);
                        string li = $"[{DateTime.Now}] Repeating";
                        Console.WriteLine(li);
                        ErrorFile.WriteLine(li);

                        ScraperData.lastPageIndex = 0;
                        continue;
                    }
                    //Console.ReadKey(true);
                    break;
                }

                //Lock the database and iterate the addons from the search result
                lock (ScraperData)
                {
                    bool followFlags = ScraperRules.FollowScrapeFlag;

                    for (int i = 0; i < addons.Length; i++)
                    {
                        Addon addon = addons[i]; //New
                        Addon stale = addons[i]; //Original addon

                        IdFile.WriteLine(addon.id);

                        if (!ScraperData.Addons.ContainsKey(addon.id))
                            ScraperData.Addons.Add(addon.id, addon); //If it doesnt exist in database, add it
                        else
                            if (ScraperData.Addons[addon.id].scrapeInfo == Addon.ScrapeInfoEnum.EMPTY)
                            ScraperData.Addons[addon.id] = addon; //If it exists in the database but is empty, set it
                        else
                            addon = ScraperData.Addons[addon.id]; //If it already exists copy it

                        //determine whether to skip or to scrape
                        Addon.ScrapeInfoEnum currentargsflags =
                            (ScraperRules.SkipDownloadJars ? 0 : Addon.ScrapeInfoEnum.FILES) |
                            (ScraperRules.SkipDescription ? 0 : Addon.ScrapeInfoEnum.DESCRIPTION) |
                            (ScraperRules.SkipImages ? 0 : Addon.ScrapeInfoEnum.IMAGES) |
                            (ScraperRules.RetrieveSource ? Addon.ScrapeInfoEnum.SOURCE : 0) |
                            (ScraperRules.CreateIcon ? Addon.ScrapeInfoEnum.ICON : 0) |
                            (ScraperRules.SkipWriteInfoFile ? 0 : Addon.ScrapeInfoEnum.INFOFILES)
                            ;
                                                                                            //TEMPORARY!!!!
                        bool updatedRecently = stale.dateModified > addon.dateScraped;// || addon.addonFiles.Count() > 49;
                        bool upToCurrentArgs = (currentargsflags & addon.scrapeInfo) == currentargsflags;

                        //Console.WriteLine($"stale.dateModified {stale.dateModified} stale.dateScraped {stale.dateScraped}");
                        //Console.WriteLine($"addon.dateModified {addon.dateModified} addon.dateScraped {addon.dateScraped}");
                        Console.WriteLine($"FollowFlags {followFlags} UpdatedRecently {updatedRecently} ({stale.dateModified} > {addon.dateScraped}) UpToCurrentArgs {upToCurrentArgs}.. Current [{(int)addon.scrapeInfo}] Required [{(int)currentargsflags}]");

                        /*
                         * Follow   Updated Upto
                         * false    false   false   true
                         * false    false   true    true
                         * false    true    false   true
                         * false    true    true    true
                         * true     false   false   true
                         * true     false   true    false
                         * true     true    false   true
                         * true     true    true    true
                         * 
                         */

                        //if (!((!followFlags && updatedRecently) || (!upToCurrentArgs && updatedRecently)))
                        if (followFlags && !updatedRecently && upToCurrentArgs)
                        {
                            Console.WriteLine($"[{addon.id}] {addon.slug} : UP TO DATE");
                            numberSkipped++;
                            continue;
                        }


                        //Transform categories
                        if ((stale.categories?.Length ?? 0) > 0)
                            addon._categories = stale.categories.Select(n => n.id).ToArray();
                        addon.categories = null;

                        //Transform latest files, which are very unlikely to be absent from the complete file list request
                        if ((stale.latestFiles?.Length ?? 0) > 0)
                            addon._latestFiles = stale.latestFiles.Select(n => n.id).ToArray();
                        addon.latestFiles = null;

                        addon.scrapeInfo |= Addon.ScrapeInfoEnum.SEARCH;
                        Console.WriteLine($"[{ScraperData.lastPageIndex + i} / {totalItemCount} : [{categoryIndex} / {ScraperData.Categories.Count()}]] [{addon.id}] {addon.slug} : {addon.name}");
                        //true                           //true                           //false                                                true

                        /*
                         *              Follow      HasDesc     Skip
                         * Scrape       false       false       false
                         * Skip         false       false       true
                         * Scrape       false       true        false
                         * Skip         false       true        true
                         * Scrape       true        false       false
                         * Skip         true        false       true
                         * Skip         true        true        false
                         * Skip         true        true        true
                         * 
                         * follow || (!has || !skip)
                         * !skip && ((follow && !has) || (!follow && has))
                         * (!follow && !skip) || (!has && !skip)
                         * https://www.dcode.fr/boolean-truth-table
                         */


                        bool hasDescription = (addon.scrapeInfo & Addon.ScrapeInfoEnum.DESCRIPTION) != 0;
                        if ((!followFlags && !ScraperRules.SkipDescription) || (!hasDescription && !ScraperRules.SkipDescription))
                        {
                            try
                            {
                                addon.description = GetJsonDeserializer<Container<string>>(GetAPIUrl($"/mods/{addon.id}/description")).data;
                                addon.scrapeInfo |= Addon.ScrapeInfoEnum.DESCRIPTION;
                            }
                            catch (Exception e)
                            {
                                string errstr = $"[{DateTime.Now}] Failed to get description for {addon.id}:{addon.slug} ({e.Message})\r\n{e.StackTrace}";
                                Console.WriteLine(errstr);
                                ErrorFile.WriteLine(errstr);
                            }
                        }

                        //TO-DO: Addon page provides some clues whether to scrape files or not

                        /*      immed   files
                         * 0    0       0
                         * 0    0       1
                         * 1    1       0
                         * 0    1       1
                         * 
                         */

                        //bool hasFiles = (addon.scrapeInfo & Addon.ScrapeInfoEnum.INFOFILES) != 0;

                        if (ScraperRules.ImmediatelyScrapeFiles)
                        {
                            DirectoryInfo addonDirectory = Directory.CreateDirectory(PathCombine(ScraperRules.OutputLocation, $"{addon.slug}"));

                            AddonFile[] addonFiles = null;
                            List<AddonFile> addonFiles_tmp = new List<AddonFile>();

                            //use updatedrecently and if addon has scraped files
                            bool hasFiles = (addon.scrapeInfo & Addon.ScrapeInfoEnum.FILES) != 0;
                            /*
                             * Fol  Has Upd
                             * 0    0   0   1
                             * 0    0   1   1
                             * 0    1   0   1
                             * 0    1   1   1
                             * 1    0   0   1
                             * 1    0   1   1
                             * 1    1   0   0
                             * 1    1   1   1
                             * 
                             */


                            if (!(followFlags && hasFiles && !updatedRecently)) {
                                try
                                {
                                    //TO-DO pagination
                                    //addonFiles = GetJsonDeserializer<Container<AddonFile[]>>(GetAPIUrl($"/mods/{addon.id}/files")).data;
                                    Pagination index = new Pagination();
                                    while (true) {
                                        var container = GetJsonDeserializer<Container<AddonFile[]>>(GetAPIUrl($"/mods/{addon.id}/files?index={index.index}"));
                                        addonFiles_tmp.AddRange(container.data);
                                        index = container.pagination;

                                        if (index.index + index.pageSize >= index.totalCount)
                                            break;

                                        index.index += index.pageSize;
                                    }
                                    addonFiles = addonFiles_tmp.ToArray();
                                    
                                    //addon.scrapeInfo |= Addon.ScrapeInfoEnum.FILES; we can set this later after we know the files have their changelog / jar files
                                }
                                catch (Exception e)
                                {
                                    string errstr = $"[{DateTime.Now}] Failed to get files for {addon.id}:{addon.slug} ({e.Message})\r\n{e.StackTrace}";
                                    Console.WriteLine(errstr);
                                    ErrorFile.WriteLine(errstr);
                                    continue;
                                }
                            }
                            else
                            {
                                //load addon files from database
                                var a = addon.addonFiles.Select(n =>
                                ScraperData.AddonFiles.TryGetValue(n, out AddonFile value) ? (ok: true, value) : (ok: false, null));
                                var b = a.Where(t => t.ok);
                                var c = b.Select(t => t.value);
                                addonFiles = c.ToArray();

                                
                                Console.WriteLine("Loaded files from database");
                            }

                            //if (addonFiles is null || addonFiles.Length < 1)
                            if ((addonFiles?.Length ?? 0) < 1)
                                continue;

                            addon.addonFiles = addonFiles.Select(n => n.id).ToArray();

                            /// ITERATE AND DOWNLOAD
                            //foreach (var addonFile in addonFiles)
                            for (int ii = 0; ii < addonFiles.Length; ii++)
                            {
                                AddonFile addonFile = addonFiles[ii];

                                if (!ScraperData.AddonFiles.ContainsKey(addonFile.id))
                                {
                                    ScraperData.AddonFiles.Add(addonFile.id, addonFile);
                                    LinkFile.WriteLine(addonFile.downloadUrl);

                                    //new files only
                                    addonFile.baseAddonId = addon.id;

                                    foreach (var versionname in addonFile.sortableGameVersions)
                                        if (!ScraperData.SortableGameVersions.Contains(versionname))
                                            ScraperData.SortableGameVersions.Add(versionname);
                                    addonFile.gameVersions = addonFile.sortableGameVersions.Select(n => n.ToString()).ToArray();
                                    addonFile.sortableGameVersions = null;
                                }
                                else
                                {
                                    //Update existing addonFile
                                    //Assuming file can not change
                                    addonFile = ScraperData.AddonFiles[addonFile.id];
                                }

                                bool hasChangelog = (addonFile.scrapeInfo & AddonFile.ScrapeInfoEnum.CHANGELOG) != 0;
                                bool skipChangelog = ScraperRules.SkipChangelog;
                                if ((!followFlags && !skipChangelog) || (!hasChangelog && !skipChangelog))
                                {
                                    try
                                    {
                                        //Console.WriteLine($"FollowFlags {followFlags} SkipChangelog {skipChangelog} HasChangelog {hasChangelog}");
                                        addonFile.changelog = GetJsonDeserializer<Container<string>>(GetAPIUrl($"/mods/{addon.id}/files/{addonFile.id}/changelog")).data;
                                        addonFile.scrapeInfo |= AddonFile.ScrapeInfoEnum.CHANGELOG;
                                    }
                                    catch (Exception e)
                                    {
                                        string errstr = $"[{DateTime.Now}] Failed to get changelog for file {addonFile.id} for addon {addon.id}:{addon.slug} ({e.Message})\r\n{e.StackTrace}";
                                        Console.WriteLine(errstr);
                                        ErrorFile.WriteLine(errstr);
                                    }
                                }

                                var fileName = $"{addonFile.id}-{addonFile.MCVersion}-{addonFile.FileName}";
                                var filePath = PathCombine(addonDirectory.FullName, fileName);
                                bool hasJarFile = (addonFile.scrapeInfo & AddonFile.ScrapeInfoEnum.DOWNLOADED) != 0 || File.Exists(filePath) || addonFile.Downloaded;
                                bool skipFiles = ScraperRules.SkipDownloadJars;

                                if ((!followFlags && !skipFiles) || (!hasJarFile && !skipFiles))
                                {
                                    try
                                    {
                                        //Console.WriteLine($"FollowFlags {followFlags} SkipFiles {skipFiles} HasFile {hasJarFile} ({!followFlags} && {!skipFiles}) || {!hasJarFile} && {!skipFiles})");
                                        CDNClient.DownloadFile(addonFile.DownloadUrl, filePath);
                                        Console.WriteLine($"{addonFile.DownloadUrl} -> {fileName}");
                                        addonFile.Downloaded = true;
                                        addonFile.scrapeInfo |= AddonFile.ScrapeInfoEnum.DOWNLOADED;
                                    }
                                    catch
                                    {
                                        string er = $"[{DateTime.Now}] Failed to download file {addonFile.id} for addon {addon.id}:{addon.slug}";
                                        Console.WriteLine(er);
                                        ErrorFile.WriteLine(er);
                                    }
                                }
                            }
                            addon.scrapeInfo |= Addon.ScrapeInfoEnum.FILES;//We KNOW the files exist. the files must follow their own state


                            ///ICON
                            bool hasIcon = File.Exists(PathCombine(addonDirectory.FullName, $"{addon.slug}.ico")) && (addon.scrapeInfo & Addon.ScrapeInfoEnum.ICON) != 0;
                            bool createIcon = ScraperRules.CreateIcon;
                            if ((!followFlags && createIcon) || (!hasIcon && createIcon))
                                try
                                {
                                    if (addon.logo != null && (addon.logo.url?.Length ?? 0) > 0)
                                    {
                                        var logoFilename = PathCombine(addonDirectory.FullName, Path.GetFileName(new Uri(addon.logo.url).LocalPath));
                                        CDNClient.DownloadFile(addon.logo.url, logoFilename);
                                        IconGenerator.GenerateIcon(new FileInfo(logoFilename), addon.slug, addonDirectory);
                                        Console.WriteLine($"Generated icon for {addon.slug}");
                                        addon.scrapeInfo |= Addon.ScrapeInfoEnum.ICON;
                                    }
                                }
                                catch (Exception e)
                                {
                                    string errstr = $"[{DateTime.Now}] Failed to get icon for {addon.id}:{addon.slug} ({e.Message})\r\n{e.StackTrace}";
                                    Console.WriteLine(errstr);
                                    ErrorFile.WriteLine(errstr);
                                }

                            ///SOURCE
                            bool hasSource = (addon.scrapeInfo & Addon.ScrapeInfoEnum.SOURCE) != 0;
                            bool getSource = ScraperRules.RetrieveSource;
                            if ((!followFlags && getSource) || (!hasSource && getSource))
                                try
                                {
                                    if ((addon.sourceUrl?.Length ?? 0) > 0)
                                    {
                                        SourceRetriever.RetrieveSource(addon.slug, addon.sourceUrl, addonDirectory);
                                        Console.WriteLine($"Retrieved source for {addon.slug}");
                                        addon.sourceAvailable = true; //This is the true source bit
                                    }
                                    addon.scrapeInfo |= Addon.ScrapeInfoEnum.SOURCE; //Set even if there is no source, we looked and didn't error out
                                }
                                catch (Exception e)
                                {
                                    string errstr = $"[{DateTime.Now}] Failed to get source for {addon.id}:{addon.slug} ({e.Message})\r\n{e.StackTrace}";
                                    Console.WriteLine(errstr);
                                    ErrorFile.WriteLine(errstr);
                                }

                            ///IMAGES
                            bool hasImages = (addon.scrapeInfo & Addon.ScrapeInfoEnum.IMAGES) != 0;
                            bool skipImages = ScraperRules.SkipImages;
                            if ((!followFlags && !skipImages) || (!hasImages && !skipImages))
                                try
                                {
                                    var imageDirectory = addonDirectory.CreateSubdirectory("images");
                                    foreach (var image in addon.Images)
                                    {
                                        var imageFilename = PathCombine(imageDirectory.FullName, Path.GetFileName(new Uri(image.url).LocalPath));
                                        CDNClient.DownloadFile(image.url, imageFilename);

                                    }

                                    addon.scrapeInfo |= Addon.ScrapeInfoEnum.IMAGES;
                                }
                                catch (Exception e)
                                {
                                    string errstr = $"[{DateTime.Now}] Failed to get images for {addon.id}:{addon.slug} ({e.Message})\r\n{e.StackTrace}";
                                    Console.WriteLine(errstr);
                                    ErrorFile.WriteLine(errstr);
                                }

                            ///INFO FILE
                            bool hasInfoFile = (addon.scrapeInfo & Addon.ScrapeInfoEnum.INFOFILES) != 0;
                            bool skipInfos = ScraperRules.SkipWriteInfoFile;
                            if ((!followFlags && !skipInfos) || (!hasInfoFile && !skipInfos))
                                try
                                {
                                    //json data
                                    //File.WriteAllText(PathCombine(addonDirectory.FullName, $"{addon.slug}.json"), );
                                    WriteJson<Addon>(PathCombine(addonDirectory.FullName, $"{addon.slug}.json"), addon);
                                    using (FileStream fstream = new FileStream(PathCombine(addonDirectory.FullName, $"{addon.slug}.txt"), FileMode.Create, FileAccess.Write, FileShare.None, 1048576))
                                    using (StreamWriter sw = new StreamWriter(fstream))
                                    {
                                        sw.WriteLine("{0} - {1}", addon.name, addon.slug);
                                        sw.WriteLine();
                                        sw.WriteLine(addon.summary);
                                        sw.WriteLine();
                                        if ((addon.authors?.Length ?? 0) > 0)
                                            sw.WriteLine("{0}\r\n", addon.authors.Select(n => n.name).Aggregate((a, b) => $"{a}, {b}"));
                                        sw.WriteLine("Downloads: {0}", addon.downloadCount);
                                        sw.WriteLine("Project ID: {0}", addon.id);
                                        sw.WriteLine();
                                        sw.WriteLine("Created: {0}", addon.dateCreated.ToString("MMMM dd, yyyy"));
                                        sw.WriteLine("Updated: {0}", addon.dateModified.ToString("MMMM dd, yyyy"));
                                        sw.WriteLine();
                                        if ((addon._categories?.Length ?? 0) > 0)
                                            sw.WriteLine("Categories: {0}\r\n", addon._categories.Select(n => ScraperData.Categories.First(m => m.id == n)).Select(n => n.name).Aggregate((a, b) => $"{a}, {b}"));
                                        if ((addon.addonFiles?.Length ?? 0) > 0)
                                            addon.addonFiles.Select(n => ScraperData.AddonFiles[n])
                                                .OrderByDescending(n => n.id).ToList()
                                                .ForEach(n => sw.WriteLine("{0} {1} {2} {3} {4}", n.MCVersion, n.id, n.downloadCount, n.DownloadUrl, n.fileDate.ToString("MMMM dd, yyyy")));
                                    }
                                    if ((addon.description?.Length ?? 0) > 0)
                                        File.WriteAllText(PathCombine(addonDirectory.FullName, $"description.html"), addon.description);
                                    addon.scrapeInfo |= Addon.ScrapeInfoEnum.INFOFILES;
                                }
                                catch (Exception e)
                                {
                                    string errstr = $"[{DateTime.Now}] Failed to write info for {addon.id}:{addon.slug} ({e.Message})\r\n{e.StackTrace}";
                                    Console.WriteLine(errstr);
                                    ErrorFile.WriteLine(errstr);
                                }

                        }

                        addon.scraped = true;
                        addon.dateScraped = DateTime.UtcNow;
                    }

                    ScraperData.lastPageIndex += searchLength;
                }
            }

            RunSaveThread = false;
            WriteJson("scraper.data.json", ScraperData);
            saveThread.Interrupt();
            saveThread.Join();
        }
    }

    public class StrictIntConverter : JsonConverter
    {
        readonly JsonSerializer defaultSerializer = new JsonSerializer();

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsIntegerType();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                case JsonToken.Float: // Accepts numbers like 4.00
                case JsonToken.Null:
                    return defaultSerializer.Deserialize(reader, objectType);
                default:
                    throw new JsonSerializationException(string.Format("Token \"{0}\" of type {1} was not a JSON integer", reader.Value, reader.TokenType));
            }
        }

        public override bool CanWrite { get { return false; } }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public static class JsonExtensions
    {
        public static bool IsIntegerType(this Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(System.Numerics.BigInteger))
                return true;
            return false;
        }
    }

    public static class Extensions
    {
        public static T DeepClone<T>(this T obj)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
                stream.Position = 0;

                return (T)formatter.Deserialize(stream);
            }
        }
    }

}
