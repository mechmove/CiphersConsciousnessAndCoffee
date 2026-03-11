using System.Numerics;
using System.Runtime;

namespace PredestinedOrNearInfinite
{
	internal class Program
	{
		private enum RotorSide : int { Predestined = 0, Calculated = 1 }
		static void Main(string[] args)
		{
			int radix = 256;
			int RandomArraySize = radix * 32;

			// Purpose: this program will create lookup files used by branch: SuperPositionsWanttoMeetYou for all possible system.random
			// unique arrays (2^31) containing 256 unique bytes, which are the basis for the rotors used in the encryption process. The
			// lookup files will be organized in a folder structure that allows for efficient searching and retrieval of the corresponding
			// seed for a given rotor. The process will involve generating sequential random byte arrays, checking for uniqueness, and then
			// storing the relevant information (seed and specific bytes) in the appropriate lookup file based on the seed range. This
			// will enable us to quickly identify if any rotors created on the Calculated side match any of the rotors on the Predestined side. 

			// WARNING: THIS PROGRAM WILL RUN FOR DAYS, AND WILL CREATE A LARGE NUMBER OF FILES, SO IT IS RECOMMENDED TO RUN IT ON A MACHINE
			// WITH SUFFICIENT RESOURCES AND TO MONITOR THE PROCESS REGULARLY. IT IS ALSO RECOMMENDED TO ADJUST THE START AND END VALUES IN
			// THE startLookup.txt and endLookup.txt FILES TO MANAGE THE WORKLOAD AND TIME REQUIRED FOR THE GENERATION PROCESS.

			// Usage, copy the following files to your debug folder:
			// startLookup.txt, endLookup.txt then compile program.
			// the range of values is 0 - 2147.
			// If we want to create all data files,
			// use the following values:
			// startLookup.txt = 0, endLookup.txt=2147 
			// you may stop and restart the program at will, just make sure to restart
			// value in startLookup.txt is set to the last value processed, and the
			// endLookup.txt is set to the desired end value for the next run.

			// program desc: first, the program will create all folder structures Lookups_X.txt, increment where x = 0 incremented by
			// 1_000_000 up to 2_147_000_000, each file contains 1 million lines of the format: "Seed,FirstByte,128thByte,256thByte", where
			// Seed is the seed used to create the rotor, and the bytes are the first, 128th and 256th byte of the rotor created with that seed.
			// This allows us to quickly look up the seed for a given rotor by matching the first, 128th and 256th byte of the rotor to the
			// corresponding line in the appropriate file. We can then use that seed to recreate the rotor and verify that it matches the original rotor.
			// This is necessary because we cannot store all possible rotors in memory, but we can store a subset of them in files and look
			// them up as needed.

			// base folder is Lookups
			if (Directory.Exists("Lookups"))
			{
				Console.Write("Folder Exists: Lookups" + Environment.NewLine);
			}
			else
			{
				Directory.CreateDirectory("Lookups");
				Console.Write("Folder Created: Lookups" + Environment.NewLine);
			}

			for (double i = 0; i <= 2_147_000_000; i += 1_000_000)
			{
				string Foldername = @"Lookups\\Lookups_" + i.ToString() + ".txt";
				if (Directory.Exists(Foldername))
				{
					Console.Write("Folder Exists:" + Foldername + Environment.NewLine);
				}
				else
				{
					Directory.CreateDirectory(Foldername);
					Console.Write("Filename Created:" + Foldername + Environment.NewLine);
				}
			}

			var bNextTarget = new byte[256 * 32];
			byte[] bUniqueSource = new byte[256];
			System.Random oRandom = new System.Random();
			//int End = 2_147_483;
			int End = Convert.ToInt16(File.ReadAllText("endLookup.txt").Replace(",", "")); ;
			int start = Convert.ToInt16(File.ReadAllText("startLookup.txt").Replace(",", ""));
			for (int i = start; i <= End; i++)
			{
				CreateLookups(i * 1_000_000, RandomArraySize, radix);
			}

			Console.Write("process completed!" + Environment.NewLine);
			Console.ReadKey();
		}
		private static void CreateLookups(Int32 start, int RandomArraySize, int radix)
		{ // for Main Rotor Creation

			string folderNm = @"Lookups\\Lookups_" + start.ToString() + ".txt";

			String[] OutPut = new String[1_000_000];
			int MatchTarget = radix;
			int EndCtr = 1_000_000;
			if (start.Equals(2_147_000_000))
			{
				EndCtr = 483_648;
				OutPut = new String[EndCtr];
			}

			Int32 Beginning = start;

			DateTime LastDt = DateTime.Now;
			byte[] bNext = new byte[RandomArraySize]; // Need unique numbers only, this is the available pool, larger than required
			for (Int32 i = 0; i < EndCtr; i++)
			{
				System.Random oRandom = new System.Random(start);
				oRandom.NextBytes(bNext);
				byte[] bUnique = bNext.Distinct().ToArray();

				string Stuff = string.Empty;
				if (bUnique.Length.Equals(radix))
				{
					string UniqueCurrent = start.ToString() + "," + bUnique[0] + "," + bUnique[127] + "," + bUnique[255];
					OutPut[i] = UniqueCurrent;
					Console.WriteLine("Fill Array for " + folderNm + ":" + i.ToString() + ":" + bUnique[0] + "," + bUnique[127] + "," + bUnique[255]);
				}
				start++;
			}

			for (int i = 0; i < EndCtr; i++)
			{
				string partialFln = RtnCSVEntry(OutPut[i], 1) + ".txt";

				if (File.Exists(folderNm + "\\" + "Lookups_" + partialFln))
				{
					//Console.Write("File Exists: " + folderNm + "\\" + "Lookups_" + partialFln + Environment.NewLine);
					if (!File.ReadAllText(folderNm + "\\" + "Lookups_" + partialFln).Contains(OutPut[i]))
					{
						File.AppendAllText(folderNm + "\\" + "Lookups_" + partialFln, OutPut[i] + Environment.NewLine);
						//Console.WriteLine("File Updated: " + folderNm + "\\" + "Lookups_" + partialFln + Environment.NewLine);
						Console.WriteLine("Insert for " + folderNm + ":" + i.ToString() + ":" + " File Updated: " + OutPut[i] + Environment.NewLine);
					}
					else
					{
						Console.WriteLine("Insert for " + folderNm + ":" + i.ToString() + ":" + " File already contains entry: " + OutPut[i] + Environment.NewLine);
					}
				}
				else
				{
					File.AppendAllText(folderNm + "\\" + "Lookups_" + partialFln, OutPut[i] + Environment.NewLine);
					Console.WriteLine("Insert for " + folderNm + ":" + i.ToString() + ":" + " File Updated: " + OutPut[i] + Environment.NewLine);
				}
			}
		}

		static string RtnCSVEntry(string CSVLine, int NumCommas)
		{
			for (int j = 1; j <= NumCommas; j = j + 1)
			{
				CSVLine = CSVLine.Substring(CSVLine.IndexOf(",") + 1);
			}
			if (CSVLine.IndexOf(",") > 0)
			{
				return CSVLine.Substring(0, CSVLine.IndexOf(","));
			}
			else
			{
				return CSVLine;
			}
		}
	}
}