using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;
class FileRenamer
{
	static void Main()
	{
		// the purpose of this program is to analyze the output of the collision search and extract the index, hash and seed to create a csv file
		// that can be used to analyze the results in excel. The program will look for files with the name "Hits_*.txt" in the specified directory,
		// extract the index, hash and seed from the file and then look for a corresponding file with the name "PredestinedIndex_*.csv" that contains
		// the same index. If a corresponding file is found, the program will write the index, hash and seed to a new csv file. If no corresponding
		// file is found, the program will print a message to the console.

		// You may just copy the contents of Out by setting a breakpoint and copying the value and paste into Grok to get a Shannon Entropy analysis
		// 

		// Define the target directory path. Use the @ symbol to handle backslashes easily.
		string directoryPath = @"E:\repos\TomsFTU\bin\Debug\net8.0-windows\Received\1000+ moving cipher runs\"; // **CHANGE THIS PATH**

		if (!Directory.Exists(directoryPath))
		{
			Console.WriteLine("Directory not found: " + directoryPath);
			return;
		}

		//Console.WriteLine("Renaming files in: " + directoryPath);

		// Get all files in the directory
		DirectoryInfo oDInfo = new DirectoryInfo(directoryPath);
		string fileSpec = "Hits_*.txt"; // Get all files with any extension
		FileInfo[] files = oDInfo.GetFiles(fileSpec).OrderBy(p => p.CreationTime).ToArray();

		int i = 0;
		string Out = string.Empty;
		foreach (FileInfo file in files)
		{
			i++;
			//#	Seed	Hash (SHA-256)	Timestamp
			//"3/9/2026 2:12:28 AM GMT"
			//"Start CollisionSearch Target Int:1374182650"
			//"Unique Hash:d4d696b73100e858ccfaaff00123c535aba9ca699029a7b155ee1eddd7f79766"
			string[] contents = File.ReadAllLines(file.FullName); // Check if the file is accessible (not locked by another process)	
			string IndextoSearch = contents[1].Substring(contents[1].IndexOf(":") + 1);
			FileInfo[] filesInfo = oDInfo.GetFiles("*PredestinedIndex_" + IndextoSearch + ".csv").OrderBy(p => p.CreationTime).ToArray();
			if (filesInfo.Count().Equals(1))
			{
				Out += Environment.NewLine + i.ToString() + "," + contents[1].Substring(contents[1].IndexOf(":") + 1) + "," + contents[2].Substring(12) + "," + contents[0];
			}
			else
			{
				Console.WriteLine(filesInfo.Length + " files found for index: " + IndextoSearch + " in file: " + file.Name);
				Console.ReadKey();
			}
		}
		Console.WriteLine("process completed.");
		Console.ReadKey();

	}
}

