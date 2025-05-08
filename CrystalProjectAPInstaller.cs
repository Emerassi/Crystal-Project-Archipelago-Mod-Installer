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
		const string crystalProjectExeName = "Crystal Project.exe";
		const string crystalProjectArchipelagoName = "CrystalProjectAP.exe";
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

			Console.WriteLine("Please select a file in the dialogue window");
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
			Console.Error.WriteLine("Installer located in Crystal Project install directory");
			Console.Error.WriteLine("This method of installation is no longer supported. Please do not move any files before installing the mod");
			Exit();
			return;
		}

		if (crystalProjectPath != null)
		{
			string crystalProjectExePath = Path.Combine(crystalProjectPath, crystalProjectExeName);
			string crystalProjectArchipelagoExePath = Path.Combine(crystalProjectPath, crystalProjectArchipelagoName);
			string apAssetsPath = Path.Combine(crystalProjectPath, "archipelago-assets");
			string vanillaLocation = Path.Combine(apAssetsPath, crystalProjectExeName);
			//Copy mod files to Crystal Project directory
			if (Directory.Exists(apAssetsPath))
			{
				//Replace the modded exe with the vanilla one to apply the bsdiff
				Console.WriteLine("Mod already installed, removing and reinstalling");
				File.Copy(vanillaLocation, crystalProjectExePath, true);
				Directory.Delete(apAssetsPath, true);
			}
			DeepCopy(installerPath, crystalProjectPath, new List<string> { "LICENSE.txt", "CrystalProjectAPInstaller.exe", patchName });
			try
			{
				//Open crystal project exe path and compute the hash to make sure that it's the right version/vanilla
				FileStream hammerwatchExeStream = new(crystalProjectExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				using MD5 md5 = MD5.Create();
				string exeHashString = BitConverter.ToString(md5.ComputeHash(hammerwatchExeStream)).Replace("-", "").ToLower();
				hammerwatchExeStream.Dispose();

				if (exeHashString != "ddf8414912a48b5b2b77873a66a41b57") //Vanilla hash
				{
					Console.Error.WriteLine("Vanilla Hammerwatch exe not found, please reinstall Hammerwatch");
					Exit();
					return;
				}
				File.Copy(crystalProjectExePath, vanillaLocation);

				//Reopen the crystal project exe (should be vanilla at this point) and apply the bsdiff
				FileStream input = new(crystalProjectExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				FileStream output = new(crystalProjectArchipelagoExePath, FileMode.Create);
				BinaryPatchUtility.Apply(input, () => new FileStream(Path.Combine(installerPath, patchName), FileMode.Open, FileAccess.Read, FileShare.Read), output);
				input.Dispose();
				output.Dispose();
				File.Move(crystalProjectArchipelagoExePath, crystalProjectExePath, true);
			}
			catch (FileNotFoundException ex)
			{
				Console.Error.WriteLine($"Could not open '{ex.FileName}'.");
				Console.Error.WriteLine("Make sure all supplied mod files exist in the same directory as the installer");
				Exit();
				return;
			}
			Console.WriteLine("Patching successful!");
		}
		else
		{
			Console.WriteLine("No valid file selected, exiting installation process");
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
