using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Documents;
using System.Windows.Forms;
using BsDiff.Core;
using Microsoft.Win32;

namespace CrystalProjectInstaller;

class CrystalProjectAPInstaller
{
	[STAThread]
	private static void Main(string[] args)
	{
		const string originalCrystalProjectExeName = "Crystal Project.exe.original";
		const string crystalProjectExeName = "Crystal Project.exe";
		const string crystalProjectArchipelagoName = "CrystalProjectTemp.exe";
		const string patchName = "CrystalProjectAP.bsdiff";

		Console.WriteLine("Crystal Project Archipelago Installer");

		string installerPath = Directory.GetCurrentDirectory();
		string crystalProjectPath = null;

		//Check if the installer is in the install directory, if it is skip the file selection dialogue
		if (!File.Exists(crystalProjectExeName))
		{
			string initialDirectory = "C:\\";
			try
			{
				RegistryKey steamKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Valve\\Steam");
				string steamDir = steamKey.GetValue("InstallPath") as string;
				StreamReader libraryFoldersReader = File.OpenText(Path.Combine(steamDir, "steamapps", "libraryfolders.vdf"));
				List<string> paths = new List<string>();
				//Parse through folders file and keep track of steam install paths
				while (!libraryFoldersReader.EndOfStream)
				{
					string line = libraryFoldersReader.ReadLine();
					if (line.Contains("\"path\""))
					{
						line = line.Replace("\"path\"", "");
						line = line.Substring(line.IndexOf('"') + 1);
						line = line.Remove(line.Length - 1);
						line = line.Replace("\\\\", "\\");
						paths.Add(line);
					}
				}
				for (int p = 0; p < paths.Count; p++)
				{
					//For some reason games are installed in either "steam" or "steamapps"
					//Not sure what the distinction is but seems like C:// is "steam" and D:// is "steamapps"?
					string path1 = Path.Combine(paths[p], "steam\\common\\Crystal Project");
					if (Directory.Exists(path1))
					{
						initialDirectory = path1;
						break;
					}
					string path2 = Path.Combine(paths[p], "steamapps\\common\\Crystal Project");
					if (Directory.Exists(path2))
					{
						initialDirectory = path2;
						break;
					}
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("An error has occured when trying to find the Crystal Project install directory:");
				Console.Error.WriteLine(e.ToString());
			}

			Console.WriteLine("Please select your Crystal Project.exe file in the dialogue window");
			using System.Windows.Forms.OpenFileDialog openFileDialog = new();
			openFileDialog.InitialDirectory = initialDirectory;
			openFileDialog.Filter = "Crystal Project.exe|Crystal Project.exe";
			openFileDialog.FilterIndex = 1;
			openFileDialog.RestoreDirectory = true;

			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				crystalProjectPath = Path.GetDirectoryName(openFileDialog.FileName);
			}
		}
		else
		{
			Console.Error.WriteLine("\nInstaller located in Crystal Project install directory.  Installer will not run correctly if you dropped it into the Crystal Project directory.\n");
			Exit();
			return;
		}

		if (crystalProjectPath != null)
		{
			string crystalProjectExePath = Path.Combine(crystalProjectPath, crystalProjectExeName);
			string originalCrystalProjectExePath = Path.Combine(crystalProjectPath, originalCrystalProjectExeName);
			string crystalProjectArchipelagoExePath = Path.Combine(crystalProjectPath, crystalProjectArchipelagoName);

			// We used to keep backup files around in the directory, but this is pointless because we have Steam.  It is the backup.
			// Running verify files will restore a mistake in 2 seconds.
			// This code exists now so that if anybody else runs the installer it'll clean up our old backup file.  This is something we can remove by v1.0 release.
			if (File.Exists(originalCrystalProjectExePath))
			{
				File.Delete(originalCrystalProjectExePath);
			}

			try
			{
				string archipelagoBranchHashString = "9a1e47b7fb5198f86b13279beb9f6f50"; //1.6.5 Archipelago Branch hash
				string archipelagoModdedVersionString = "44daa4227d311f6967f20dc621fa3d63"; //v0.7.0 Archipelago Modded hash

				//Open crystal project exe path and compute the hash to make sure that it's the right version
				FileStream crystalProjectBeforeStream = new(crystalProjectExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				using MD5 md5 = MD5.Create();
				string exeBeforeHashString = BitConverter.ToString(md5.ComputeHash(crystalProjectBeforeStream)).Replace("-", "").ToLower();
				crystalProjectBeforeStream.Dispose();

				if (exeBeforeHashString == archipelagoModdedVersionString)
				{
					Console.WriteLine("\nYour Crystal Project.exe is already the archipelago modded version. Proceeding to copy other files.");
				}
				else if (exeBeforeHashString != archipelagoBranchHashString)
				{
					Console.Error.WriteLine("\nCrystal Project.exe file was not the expected version.  Are you in the Archipelago Beta branch on steam?  If yes, try verifying file integrity in Steam and trying again.\n");
					Exit();
					return;
				}
				else
				{
					//Reopen the crystal project exe(should be vanilla at this point) and apply the bsdiff
					FileStream input = new(crystalProjectExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					FileStream output = new(crystalProjectArchipelagoExePath, FileMode.Create);
					BinaryPatchUtility.Apply(input, () => new FileStream(Path.Combine(installerPath, patchName), FileMode.Open, FileAccess.Read, FileShare.Read), output);
					input.Dispose();
					output.Dispose();
					File.Move(crystalProjectArchipelagoExePath, crystalProjectExePath, true);

					FileStream crystalProjectAfterStream = new(crystalProjectExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					string exeAfterHashString = BitConverter.ToString(md5.ComputeHash(crystalProjectAfterStream)).Replace("-", "").ToLower();
					crystalProjectAfterStream.Dispose();

					if (exeAfterHashString != archipelagoModdedVersionString)
					{
						Console.Error.WriteLine("\nSomething went wrong and the final version does not have the correct file hash.  Verify File Integrity in Steam and try again, or contact the developers.\n");
						Exit();
						return;
					}
				}				
			}
			catch (FileNotFoundException ex)
			{
				Console.Error.WriteLine($"\nCould not open '{ex.FileName}'.");
				Console.Error.WriteLine("Make sure all supplied mod files exist in the same directory as the installer.\n");
				Exit();
				return;
			}

			// Don't attempt the copies until after we've finished all other verifications (i.e. file hash)
			DeepCopy(installerPath, crystalProjectPath, new List<string>
			{
				"Bsdiff.Core.dll",
				"CrystalProjectAPInstaller.deps.json",
				"CrystalProjectAPInstaller.dll",
				"CrystalProjectAPInstaller.exe",
				"CrystalProjectAPInstaller.runtimeconfig.json",
				"ICSharpCode.SharpZipLib.dll",
				"LICENSE.txt",
				"README.md",
				patchName });

			Console.WriteLine("\nPatching successful!\n");
		}
		else
		{
			Console.WriteLine("\n'Crystal Project.exe' file not selected, exiting installation process.\n");
		}

		Exit();
	}

	private static void Exit()
	{
		Console.WriteLine("Press any key to close this window...");
		Console.Read();
	}

	private static void DeepCopy(string fromFolder, string toFolder, List<string> exceptionFiles = null)
	{
		string[] files = Directory.GetFiles(fromFolder);
		Directory.CreateDirectory(toFolder);
		foreach (string file in files)
		{
			if (exceptionFiles != null && exceptionFiles.Contains(Path.GetFileName(file))) continue;
			string dest = Path.Combine(toFolder, Path.GetFileName(file));
			File.Copy(file, dest, true);
		}
		string[] folders = Directory.GetDirectories(fromFolder);
		foreach (string folder in folders)
		{
			DeepCopy(folder, Path.Combine(toFolder, Path.GetFileNameWithoutExtension(folder)));
		}
	}
}
