using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace CurseforgeScraper
{
    public class IconGenerator
    {
        public static void GenerateIcon(FileInfo sourceImage, string identifier, DirectoryInfo outputFolder)
        {
            if (sourceImage is null || outputFolder is null)
                return;
            if (!sourceImage.Exists || !outputFolder.Exists)
                return;

            using (Process magickProcess = new Process())
            {
                magickProcess.StartInfo.UseShellExecute = false;
                magickProcess.StartInfo.FileName = Program.ScraperRules.magickPath;
                magickProcess.StartInfo.WorkingDirectory = outputFolder.FullName;
                magickProcess.StartInfo.Arguments = $"convert -resize 128x128 -density 128x128 -verbose \"{sourceImage.FullName}\" \"{identifier}.ico\"";
                magickProcess.Start();
                magickProcess.WaitForExit(30000);
                if (magickProcess.ExitCode != 0)
                {
                    Console.WriteLine("Failed to generate thumbnail");
                } 
                else
                {
                    INIGenerator.GenerateINI(identifier, outputFolder);
                }
            }
        }
    }

    public class INIGenerator
    {
        public static void GenerateINI(string identifier, DirectoryInfo outputFolder)
        {
            try
            {
                var iniPath = (Program.PathCombine(outputFolder.FullName, "desktop.ini"));
            using (StreamWriter sw = new StreamWriter(iniPath, false))
            {
                sw.WriteLine("[.ShellClassInfo]");
                sw.WriteLine("IconResource={0},0", identifier + ".ico");
                sw.WriteLine("IconFile={0}", identifier + ".ico");
                sw.WriteLine("IconIndex=0");
                sw.WriteLine("[ViewState]");
                sw.WriteLine("Mode=");
                sw.WriteLine("Vid=");
                sw.WriteLine("FolderType=Generic");
            }
                File.SetAttributes(iniPath, File.GetAttributes(iniPath) | FileAttributes.Hidden | FileAttributes.System);
                //set the folder as system
                File.SetAttributes(outputFolder.FullName, File.GetAttributes(outputFolder.FullName) | FileAttributes.System);
            }
            catch
            {
                //prolly just bogus
            }
        }
    }

    public class SourceRetriever
    {
        public static void RetrieveSource(string identifier, string sourceurl, DirectoryInfo outputFolder)
        {
            using (Process gitProcess = new Process())
            {
                var sourceFolder = Program.PathCombine(outputFolder.FullName, "source");
                if (!Directory.Exists(sourceFolder))
                    outputFolder.CreateSubdirectory("source");
                gitProcess.StartInfo.UseShellExecute = false;
                gitProcess.StartInfo.FileName = Program.ScraperRules.gitPath;
                gitProcess.StartInfo.WorkingDirectory = sourceFolder;
                gitProcess.StartInfo.Arguments = $"clone --recursive --mirror \"{sourceurl}\"";
                gitProcess.Start();
                Console.WriteLine("Times out in 1200 seconds");
                if (!gitProcess.WaitForExit(1200000))
                    gitProcess.Kill();
                if (!gitProcess.HasExited || gitProcess.ExitCode != 0)
                {
                    Console.WriteLine("Failed to clone {0} ({1}) ({2})", identifier, sourceurl, gitProcess.ExitCode);
                }
                else
                {
                    Console.WriteLine("Cloned {0} ({1}) successfully", identifier, sourceurl);
                    ZIPPER7.Run7z(identifier, sourceFolder, outputFolder);
                }
            }
        }
    }

    public class DescriptionGenerator
    {

    }

    public class ZIPPER7
    {
        public static void Run7z(string identifier, string file, DirectoryInfo outputFolder, bool overrided = false)
        {
            using (Process _7zProcess = new Process())
            {
                var folderName = file;
                _7zProcess.StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    FileName = Program.ScraperRules.path7z,
                    WorkingDirectory = outputFolder.FullName,
                    Arguments = $"a \"{identifier}-source.tar\" \"{folderName}\" -ttar -y {(overrided ? "" : "-sdel")} "
                };
                _7zProcess.Start();
                Console.WriteLine("Times out in 1200 seconds");
                if (!_7zProcess.WaitForExit(1200000))
                    _7zProcess.Kill();

                if (!_7zProcess.HasExited || _7zProcess.ExitCode != 0)
                {
                    Console.WriteLine("Failed to archive repository {0} marking as failed", file);
                }
                else
                {
                    Console.WriteLine("Archived {0} successfully", file);
                }
            }
        }
    }
}
