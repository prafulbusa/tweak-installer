﻿//Copyright 2018 josephwalden
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


using jLib;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tweak_Installer;
using WinSCP;

namespace Unjailbreaker
{
    class Program
    {
        static bool install = false, uninstall = false, convert = false, manual = false, jtool = false, update = true, uicache = false, respring_override = false, uicache_override = false, onlyPerformSSHActions = false;
        static string convert_path(string i)
        {
            return i.Replace("\\", "/").Replace(" ", "\\ ").Replace("(", "\\(").Replace(")", "\\)").Replace("'", "\\'").Replace("@", "\\@");
        }
        static void finish(Session session)
        {
            if (uicache && !uicache_override)
            {
                Console.WriteLine("Running uicache (may take up to 30 seconds)");
                session.ExecuteCommand("uicache"); //respring
            }
            if (!respring_override)
            {
                Console.WriteLine("Respringing...");
                session.ExecuteCommand("killall -9 SpringBoard"); //respring
            }
            session.Close();
            //Console.ReadLine();
        }
        static void createDirIfDoesntExist(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
        static void deleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
        static void emptyDir(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }
        static void moveDirIfPresent(string source, string dest, string parent = null)
        {
            if (Directory.Exists(source))
            {
                if (parent != null)
                {
                    Directory.CreateDirectory(parent);
                }
                Directory.Move(source, dest);
            }
        }
        static void Main(string[] args)
        {
            if (!File.Exists("settings"))
            {
                string[] def = new string[3];
                File.WriteAllLines("settings", def);
            }
            if (args.Contains("convert")) convert = true;
            if (args.Contains("uninstall")) uninstall = true;
            if (args.Contains("install")) install = true;
            if (args.Contains("manual")) manual = true;
            if (args.Contains("dont-update")) update = false;
            if (args.Contains("dont-refresh")) uicache_override = true;
            if (args.Contains("dont-respring")) respring_override = true;
            if (args.Contains("no-install")) onlyPerformSSHActions = true;

            if (update)
            {
                //check for updates
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        string version = client.DownloadString("https://raw.githubusercontent.com/josephwalden13/tweak-installer/master/bin/Debug/version.txt");
                        string current = File.ReadAllText("version.txt");
                        if (current != version)
                        {
                            Console.WriteLine($"Version {version.Replace("\n", "")} released. Please download it from https://github.com/josephwalden13/tweak-installer/releases\nPress enter to continue...");
                            Console.ReadLine();
                        }
                    }
                }
                catch { }
            }

            string[] data = File.ReadAllLines("settings"); //get ssh settings
            for (int i = 0; i != data.Length; i++)
            {
                data[i] = data[i].Split('#')[0];
            }
            string ip = data[0];
            string user = data[1];
            string password = data[2];

            Console.WriteLine("Connecting");
            SessionOptions sessionOptions = new SessionOptions
            {
                Protocol = Protocol.Sftp,
                HostName = ip,
                UserName = user,
                Password = password,
                GiveUpSecurityAndAcceptAnySshHostKey = true
            };
            Session session = new Session();
            session.Open(sessionOptions);

            if (onlyPerformSSHActions)
            {
                uicache = true;
                finish(session);
                return;
            }

            if (session.FileExists("/usr/lib/SBInject"))
            {
                convert = true;
                if (!session.FileExists("/System/Library/Themes"))
                {
                    session.CreateDirectory("/System/Library/Themes");
                    session.ExecuteCommand("touch /System/Library/Themes/dont-delete");
                    Console.WriteLine("Themes folder missing. Touching /System/Library/Themes/dont-delete to prevent this in future");
                }
                jtool = true;
            }
            if (session.FileExists("/jb/"))
            {
                jtool = true;
            }

            if (manual)
            {
                createDirIfDoesntExist("files");
                Console.WriteLine("Please move rootfs file into 'files' and press enter to continue");
                Console.ReadLine();
            }
            else
            {
                emptyDir("files");
                emptyDir("temp");
                deleteIfExists("data.tar");
                List<string> debs = new List<string>();
                foreach (string i in args)
                {
                    if (i.Contains(".deb"))
                    {
                        debs.Add(i);
                    }
                    if (i.Contains(".ipa"))
                    {
                        debs.Add(i);
                    }
                    if (i.Contains(".zip"))
                    {
                        debs.Add(i);
                    }
                }
                foreach (string deb in debs)
                {
                    if (deb.Contains(".deb"))
                    {
                        emptyDir("temp");
                        Console.WriteLine("Extracting " + deb);
                        using (ArchiveFile archiveFile = new ArchiveFile(deb))
                        {
                            archiveFile.Extract("temp");
                        }
                        var p = Process.Start(@"7z.exe", "e temp\\data.tar." + (File.Exists("temp\\data.tar.lzma") ? "lzma" : "gz") + " -o.");
                        p.WaitForExit();
                        using (ArchiveFile archiveFile = new ArchiveFile("data.tar"))
                        {
                            archiveFile.Extract("files");
                        }
                        emptyDir("temp");
                        deleteIfExists("data.tar");
                    }
                    else if (deb.Contains(".ipa"))
                    {
                        emptyDir("temp");
                        Console.WriteLine("Extracting IPA " + deb);
                        using (ArchiveFile archiveFile = new ArchiveFile(deb))
                        {
                            archiveFile.Extract("temp");
                        }
                        createDirIfDoesntExist("files\\Applications");
                        foreach (string app in Directory.GetDirectories("temp\\Payload\\"))
                        {
                            Directory.Move(app, "files\\Applications\\" + new DirectoryInfo(app).Name);
                        }
                    }
                    else
                    {
                        emptyDir("temp");
                        Console.WriteLine("Extracting Zip " + deb);
                        using (ArchiveFile archiveFile = new ArchiveFile(deb))
                        {
                            archiveFile.Extract("temp");
                        }
                        if (Directory.Exists("temp\\bootstrap\\"))
                        {
                            Console.WriteLine("Found bootstrap");
                            if (Directory.Exists("temp\\bootstrap\\Library\\SBInject\\"))
                            {
                                createDirIfDoesntExist("files\\usr\\lib\\SBInject");
                                foreach (string file in Directory.GetFiles("temp\\bootstrap\\SBInject\\"))
                                {
                                    File.Move(file, "files\\usr\\lib\\SBInject\\" + new FileInfo(file).Name);
                                }
                                foreach (string file in Directory.GetDirectories("temp\\bootstrap\\SBInject\\"))
                                {
                                    Directory.Move(file, "files\\usr\\lib\\SBInject\\" + new DirectoryInfo(file).Name);
                                }
                                Directory.Delete("temp\\bootstrap\\SBInject", true);
                            }
                            moveDirIfPresent("temp\\bootstrap\\Library\\Themes\\", "files\\System\\Library\\Themes\\", "files\\System\\Library\\");
                            foreach (string dir in Directory.GetDirectories("temp"))
                            {
                                FileSystem.MoveDirectory(dir, "files\\" + new DirectoryInfo(dir).Name, true);
                            }
                            foreach (string file in Directory.GetFiles("temp"))
                            {
                                File.Copy(file, "files\\" + new FileInfo(file).Name, true);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Unrecognised format. Determining ability to install");
                            List<string> exts = new List<string>();
                            foreach (string i in Directory.GetFiles("temp"))
                            {
                                string ext = new FileInfo(i).Extension;
                                if (!exts.Contains(ext)) exts.Add(ext);
                            }
                            if (exts.Count == 2 && exts.Contains(".dylib") && exts.Contains(".plist"))
                            {
                                Console.WriteLine("Substrate Addon. Installing");
                                createDirIfDoesntExist("files\\usr\\lib\\SBInject");
                                foreach (string i in Directory.GetFiles("temp"))
                                {
                                    File.Copy(i, "files\\usr\\lib\\SBInject\\" + new FileInfo(i).Name, true);
                                }
                                moveDirIfPresent("files\\Library\\PreferenceBundles\\", "files\\bootstrap\\Library\\PreferenceBundles\\");
                                moveDirIfPresent("files\\Library\\PreferenceLoader\\", "files\\bootstrap\\Library\\PreferenceLoader\\");
                            }
                            else
                            {
                                Console.WriteLine("Unsafe to install. To install this tweak you must do so manually. Press enter to continue...");
                                Console.ReadLine();
                            }
                        }
                    }
                }
            }
            string name = "script";
            if (convert) //convert to electra format
            {
                Console.WriteLine("Converting to electra tweak format");
                createDirIfDoesntExist("files\\bootstrap");
                createDirIfDoesntExist("files\\bootstrap\\Library");
                if (Directory.Exists("files\\Library\\MobileSubstrate\\"))
                {
                    createDirIfDoesntExist("files\\usr\\lib\\SBInject");
                    foreach (string file in Directory.GetFiles("files\\Library\\MobileSubstrate\\DynamicLibraries\\"))
                    {
                        File.Move(file, "files\\usr\\lib\\SBInject\\" + new FileInfo(file).Name);
                    }
                    foreach (string file in Directory.GetDirectories("files\\Library\\MobileSubstrate\\DynamicLibraries\\"))
                    {
                        Directory.Move(file, "files\\usr\\lib\\SBInject\\" + new DirectoryInfo(file).Name);
                    }
                    Directory.Delete("files\\Library\\MobileSubstrate", true);
                }
                moveDirIfPresent("files\\Library\\Themes\\", "files\\System\\Library\\Themes\\", "files\\System\\Library\\");
                moveDirIfPresent("files\\Library\\PreferenceBundles\\", "files\\bootstrap\\Library\\PreferenceBundles\\");
                moveDirIfPresent("files\\Library\\PreferenceLoader\\", "files\\bootstrap\\Library\\PreferenceLoader\\");
            }
            Crawler c = new Crawler("files", true); //gets all files in the tweak
            c.Remove("DS_STORE");
            string s = "";
            c.Files.ForEach(i =>
                       {
                           s += ("rm " + convert_path(i) + "\n"); //creates uninstall script for tweak (used if uninstall == true)
                       });
            File.WriteAllText("files\\" + name + ".sh", s); //add uninstall script to install folder
            if (args.Length > 0)
            {
                if (install)
                {
                    Console.WriteLine("Installing");
                    if (Directory.Exists("files\\Applications") && jtool)
                    {
                        File.Copy("plat.ent", "files\\plat.ent", true);
                    }
                    if (Directory.Exists("files\\Applications\\electra.app"))
                    {
                        var f = MessageBox.Show("Please do not try this");
                        Environment.Exit(0);
                    }
                    foreach (string dir in Directory.GetDirectories("files"))
                    {
                        session.PutFiles(dir, "/"); //put directories
                    }
                    foreach (string file in Directory.GetFiles("files"))
                    {
                        session.PutFiles(file, "/"); //put files
                    }
                    if (Directory.Exists("files\\Applications") && jtool)
                    {
                        Console.WriteLine("Signing applications");
                        foreach (var app in Directory.GetDirectories("files\\Applications\\"))
                        {
                            Crawler crawler = new Crawler(app);
                            c.Files.ForEach(i =>
                            {
                                if (i.Contains("\\Applications\\"))
                                {
                                    uicache = true;
                                    bool sign = false;
                                    if (new FileInfo(i).Name.Split('.').Length < 2) sign = true;
                                    if (!sign)
                                    {
                                        if (i.Split('.').Last() == "dylib") sign = true;
                                    }
                                    i = convert_path(i);
                                    if (sign)
                                    {
                                        session.ExecuteCommand("jtool -e arch -arch arm64 " + i);
                                        session.ExecuteCommand("mv " + i + ".arch_arm64 " + i);
                                        session.ExecuteCommand("jtool --sign --ent /plat.ent --inplace " + i);
                                    }
                                }
                            });
                        }
                    }
                    finish(session);
                }
                else if (uninstall)
                {
                    Console.WriteLine("Uninstalling");
                    session.PutFiles("files\\script.sh", "/");
                    session.ExecuteCommand("sh /script.sh");
                    if (Directory.Exists("files\\Applications"))
                    {
                        uicache = true;
                        foreach (string app in Directory.GetDirectories("files\\Applications\\"))
                        {
                            session.ExecuteCommand("rm -rf /Applications/" + new DirectoryInfo(app).Name);
                        }
                    }
                    Console.WriteLine("Locating and removing *some* empty folders");
                    session.ExecuteCommand("find /System/Library/Themes/ -type d -empty -delete");
                    session.ExecuteCommand("find /usr/ -type d -empty -delete");
                    session.ExecuteCommand("find /Applications/ -type d -empty -delete");
                    session.ExecuteCommand("find /Library/ -type d -empty -delete");
                    session.ExecuteCommand("find /bootstrap/ -type d -empty -delete");
                    finish(session);
                }
            }
        }
    }
}
