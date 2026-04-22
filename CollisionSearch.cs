using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using static CollisionSearchService;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
public static class CollisionSearchService
{
	public struct RotorRecord
	{
		public uint Key24;   // 24-bit key stored in 32 bits
		public int Index;    // original rotor index
		public byte S1;
		public byte S127;
		public byte S255;
		public byte Pad;     // alignment
	}
	public enum CheckOpts { chkPredestined = 1, chkMovingCipher = 2, chkPlugBoardNew = 3, chkReflectorOri = 4, chkReflectorNew = 5};
	public enum OptionsHitsFile {Yes = 1,No = 2,};
	public enum OptionsRotorDetailCSV {Yes = 1,No = 2,};
	private enum RotorSide : int { Predestined = 0, Calculated = 1 }

	// Assumes these are set appropriately elsewhere
	public static string BasePath = string.Empty;
	public static string BasePathCompleted = string.Empty;
	public static string HardstopFln = string.Empty;
	public static string OptionsFln = string.Empty;
	public static int SearchRange= 2_147_483_647;

	// defaults will be No to output files, but can be overridden by Options.txt file content
	public static OptionsHitsFile optionsHitsFile= OptionsHitsFile.No;
	public static OptionsRotorDetailCSV optionsRotorDetailCSV=OptionsRotorDetailCSV.No;

	private static string CompletedFile = string.Empty;
	private static string CompletedFileExtraStatus = string.Empty;
	private static string CompletedFileUniqueHashWords = string.Empty;
	private static string ErrorFile = string.Empty;

	private static List<string> bufCompletedFile = new List<string>();
	private static List<string> bufCompletedFileExtraStatus = new List<string>();
	private static List<string> bufCompletedFileUniqueHashWords = new List<string>();
	private static List<string> bufCompletedFileUniqueHashWords_tmp = new List<string>();
	private static List<string> bufErrorFile = new List<string>();
	//private static List<string> bufhitsLocalPath = new List<string>();
	private static HashSet<string> bufhitsLocalPath = new HashSet<string>();
	private static List<string> bufConsoleWriteLine= new List<string>();

	public static int HardStop = 100;
	public static int ETAMsgEvery = 500;
	public static int ETAMsgPauseMS = 0;
	public static bool ETABeep = false;
	public static void CollisionSearch(CancellationToken? ct = null)
	{
		string baseDirectory = AppContext.BaseDirectory;
		Directory.CreateDirectory(baseDirectory); // ensure it exists, even if empty;
		Directory.CreateDirectory(baseDirectory + "\\Lookups"); // ensure it exists, even if empty;
		Directory.CreateDirectory(baseDirectory + "\\workspace"); // ensure it exists, even if empty;

		CheckOpts checkOpts = CheckOpts.chkPredestined; 
		OptionsFln = baseDirectory + "\\workspace\\RotorCollisionSearchService.txt";
		if (File.Exists(OptionsFln))
		{
			string[] Opts = File.ReadAllLines(OptionsFln);
			foreach (string s in Opts)
			{
				string value = RtnCSVEntry(s, 0);
				string key = RtnCSVEntry(s,1);
				if (key.Equals("RotorOptions"))
				{//checkOpts with RotorOptions stored in RotorCollisionSearchService.txt
				 //chkMovingCipher; RotorOptions: override CollisionSearchService.CheckOpts
				 //chkPredestined, chkMovingCipher, chkPlugBoardNew, chkReflectorNew, chkReflectorOri
					switch (value)
					{
						case "chkPredestined":
							checkOpts = CheckOpts.chkPredestined;
							break;
						case "chkMovingCipher":
							checkOpts = CheckOpts.chkMovingCipher;
							break;
						case "chkPlugBoardNew":
							checkOpts = CheckOpts.chkPlugBoardNew;
							break;
						case "chkReflectorOri":
							checkOpts = CheckOpts.chkReflectorOri;
							break;
						case "chkReflectorNew":
							checkOpts = CheckOpts.chkReflectorNew;
							break;
					}
				}

				if (key.Equals("MaxSearchRange"))
				{// because test machines do not contain full lookup dataset, test
				 // only known values to confirm that logic is working as expected,
				 // rather than searching full space for unknowns
					int.TryParse(value, out int NewRangeMax);
					SearchRange = NewRangeMax;
				}
			}
		}
		
		GetPathsFromCheckOpts(checkOpts);

		if (File.Exists(OptionsFln))
		{
			string[] Opts = File.ReadAllLines(OptionsFln);
			foreach (string s in Opts)
			{
				string value = RtnCSVEntry(s, 0);
				string key = RtnCSVEntry(s, 1);

				// if this file exists, hardstop is overridden by value contained in this file:
				//100000; HardStop desc: stop process after this many predestined options are checked
				//No; HitsYesNo desc: do you want the HitsFile? example: Hits_56477970.txt
				//No; RotorDetailYesNo desc: do you want the rotor detail? 

				if (key.Equals("HardStop"))
				{
					int.TryParse(value, out int hs);
					HardStop = hs;
				}
				if (key.Equals("HitsYesNo"))
				{
					if (value.Equals("Yes"))
					{
						optionsHitsFile = OptionsHitsFile.Yes;

					}
				}
				if (key.Equals("RotorDetailYesNo"))
				{
					if (value.Equals("Yes"))
					{
						optionsRotorDetailCSV = OptionsRotorDetailCSV.Yes;

					}
				}
				if (key.Equals("ETA_MsgEvery"))
				{
					int.TryParse(value, out int _ETAMsgEvery);
					ETAMsgEvery = _ETAMsgEvery;
					if (ETAMsgEvery.Equals(0))
					{// cannot be zero, set it back to default if zero
						ETAMsgEvery = 500;
						Console.WriteLine("ETA_MsgEvery cannot be zero. It has been reset to default of 500.");

				}
				}
				if (key.Equals("ETA_PauseMilliSeconds"))
				{
					int.TryParse(value, out int _ETAMsgPause);
					ETAMsgPauseMS= _ETAMsgPause;
				}

				if (key.Equals("ETABeep"))
				{
					if (value.Equals("Yes"))
					{
						ETABeep = true;

					}
				}


			}
		}

		if (File.Exists(CompletedFile))
		{
			bufCompletedFile = File.ReadAllLines(CompletedFile).ToList();
		}
		if (File.Exists(CompletedFileExtraStatus))
		{
			bufCompletedFileExtraStatus = File.ReadAllLines(CompletedFileExtraStatus).ToList();
		}
		if (File.Exists(CompletedFileUniqueHashWords))
		{// we might need to convert old format hex hashes to Base64
			bufCompletedFileUniqueHashWords = File.ReadAllLines(CompletedFileUniqueHashWords).ToList();
			if (bufCompletedFileUniqueHashWords.Count>0)
			{
				if (bufCompletedFileUniqueHashWords[0].Length.Equals(64))
				{// this is hex, the old format, need to convert to base64 prior to continuing....
					int ctr = 1;
					Span<byte> bytes = stackalloc byte[32];
					foreach (string oldHex in bufCompletedFileUniqueHashWords) 
					{
						bytes = Convert.FromHexString(oldHex);
						bufCompletedFileUniqueHashWords_tmp.Add(Convert.ToBase64String(bytes));
						Console.WriteLine("converting to Base64 " + ctr + "/" + bufCompletedFileUniqueHashWords.Count);
						ctr++;
					}
					bufCompletedFileUniqueHashWords = bufCompletedFileUniqueHashWords_tmp.ToList();
					bufCompletedFileUniqueHashWords_tmp.Clear();
				}
			}
		}
		if (File.Exists(ErrorFile))
		{
			bufErrorFile = File.ReadAllLines(ErrorFile).ToList();
		}

		// iterate all user config settings to console:
		Console.WriteLine("Configuration Settings:" + Environment.NewLine);
		Console.WriteLine("App Path:" + AppDomain.CurrentDomain.BaseDirectory);
        Console.WriteLine("Working Path: " + BasePath);
		Console.WriteLine("SearchRange 0- " + SearchRange.ToString("n0"));
		Console.WriteLine("CheckOpts: " + checkOpts.ToString());
		Console.WriteLine("HardStop: " + HardStop.ToString("n0"));
		Console.WriteLine("OptionsHitsFile: " + optionsHitsFile.ToString());
		Console.WriteLine("OptionsRotorDetailCSV: " + optionsRotorDetailCSV.ToString());
		Console.WriteLine("ETA_MsgEvery: " + ETAMsgEvery.ToString("n0"));
		Console.WriteLine("ETA_PauseMilliSeconds: " + ETAMsgPauseMS.ToString("n0"));
		Console.WriteLine("Beep:" + ETABeep.ToString());
		Console.Write(Environment.NewLine);

		//Console.WriteLine("BasePathCompleted: " + BasePathCompleted);
		//Console.WriteLine("CompletedFile: " + CompletedFile);
		//Console.WriteLine("CompletedFileExtraStatus: " + CompletedFileExtraStatus);
		//Console.WriteLine("CompletedFileUniqueHashWords: " + CompletedFileUniqueHashWords);
		//Console.WriteLine("ErrorFile: " + ErrorFile);
		//Console.WriteLine("HardstopFln: " + HardstopFln);
		//Console.WriteLine("OptionsFln: " + OptionsFln);

		Console.WriteLine("Starting CollisionSearch at " + bufCompletedFile.Count().ToString("n0") + "...");
		Console.Write(Environment.NewLine);

		DateTime ProcessStartTime = DateTime.Now;

		Int32 currentProcessCnt = 0;
		Int32 TotalProcessCnt = 0;
		TotalProcessCnt = bufCompletedFile.Count();
		if (TotalProcessCnt >= (HardStop))
		{
			Console.WriteLine($"Hard stop limit of {HardStop:N0} already reached in Completed.txt." + Environment.NewLine + "Stopping search." + Environment.NewLine + Environment.NewLine);
			SummaryOfResults(TotalProcessCnt);
			Console.ReadKey();
			return;
		}

		Directory.CreateDirectory(BasePath);
		Directory.CreateDirectory(BasePathCompleted);

		// Optional: startup sweep of stale pending markers (commented if multiple instances may run)
		foreach (var pending in Directory.GetFiles(BasePath, "*_Pending")) SafeDelete(pending);
		foreach (var pending in Directory.GetFiles(BasePath, "Hits_*")) SafeDelete(pending);

		while (true)
		{
			if (ct is { IsCancellationRequested: true })
			{
				SummaryOfResults(TotalProcessCnt);
				return;
			}

			int target=0;
			try
			{
				bufConsoleWriteLine.Add($"=============================START===================================");
                bufConsoleWriteLine.Add("Current Time: " + DateTime.Now.ToLongTimeString());
				target = RtnUnused();

				bufConsoleWriteLine.Add($"Generated random Int32 target for CollisionSearch: {target}");
				// Perform the collision search
				currentProcessCnt++;
				TotalProcessCnt++;

				GoCollision(target, checkOpts);
				bufConsoleWriteLine.Add("total number checked: " + $"{TotalProcessCnt}");
				double TimeElapsed = (DateTime.Now - ProcessStartTime).TotalSeconds;
				bufConsoleWriteLine.Add("total time elapsed: " + Math.Round(TimeElapsed / 60, 2) + " minutes");
				bufConsoleWriteLine.Add("Average items produced every " + Math.Round(TimeElapsed / currentProcessCnt, 2) + " seconds.");

				if (TotalProcessCnt % ETAMsgEvery == 0)
				{
					if (ETABeep) Console.Beep(1000, 500);
                    DateTime ETA = DateTime.Now.AddSeconds((HardStop - TotalProcessCnt) * Math.Round(TimeElapsed / currentProcessCnt, 2));
					bufConsoleWriteLine.Add("ETA to reach " + HardStop.ToString("n0") + ": " + ETA.ToLongTimeString() + ", " + ETA.ToLongDateString());
					bufConsoleWriteLine.Add($"=============================END=====================================" + Environment.NewLine);
					Thread.Sleep(ETAMsgPauseMS);
					Console.WriteLine(string.Join(Environment.NewLine, bufConsoleWriteLine));
				}

				// Mark completion (use shared-safe writer for cross-process safety)
				var resultLine = BuildCompletionStatusLine(target, bufhitsLocalPath);
				bufhitsLocalPath.Clear();	
				bufCompletedFile.Add(resultLine);
				if (TotalProcessCnt.Equals(HardStop))
				{
					SummaryOfResults(TotalProcessCnt);
					Console.WriteLine($"Hard stop limit of {HardStop} reached. Stopping search.");
					Console.ReadKey();
					bufConsoleWriteLine.Clear();
					return;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error on target {target}: {ex.Message}");
			}
			bufConsoleWriteLine.Clear();
		};
	}
	static void GetPathsFromCheckOpts(CheckOpts checkOpts)
	{
		switch (checkOpts)
		{
			case CheckOpts.chkPredestined:
				BasePath = @"workspace\chkPredestined\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkPredestined\newCollision\CollisionSearchCompleted\";
				break;
			case CheckOpts.chkMovingCipher:
				BasePath = @"workspace\chkMovingCipher\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkMovingCipher\newCollision\CollisionSearchCompleted\";
				break;
			case CheckOpts.chkPlugBoardNew:
				BasePath = @"workspace\chkPlugBoardNew\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkPlugBoardNew\newCollision\CollisionSearchCompleted\";
				break;
			case CheckOpts.chkReflectorOri:
				BasePath = @"workspace\chkReflectorOri\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkReflectorOri\newCollision\CollisionSearchCompleted\";
				break;
			case CheckOpts.chkReflectorNew:
				BasePath = @"workspace\chkReflectorNew\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkReflectorNew\newCollision\CollisionSearchCompleted\";
				break;
			default:
				Console.WriteLine("Invalid CheckOpts value. Stopping.");
				return;
		}
		CompletedFile = Path.Combine(BasePathCompleted, "Completed.txt");
		CompletedFileExtraStatus = Path.Combine(BasePathCompleted, "Completed_RotorFunctionality.txt");
		CompletedFileUniqueHashWords = Path.Combine(BasePathCompleted, "Completed_UniqueHashStatus.txt");
		ErrorFile = Path.Combine(BasePathCompleted, "ErrorFile.txt");
		HardstopFln = Path.Combine(BasePathCompleted, "Hardstop.txt");
		OptionsFln = Path.Combine(BasePathCompleted, "Options.txt");
	}
	private static void SummaryOfResults(Int32 TotalProcessCnt)
	{
		Console.WriteLine("Summary of Results: " + Environment.NewLine);
		Console.WriteLine("total rotors number checked: " + $"{TotalProcessCnt.ToString("N0")}");
		Console.WriteLine("predestined rotors inside of 2^31: " + bufCompletedFile.Count(l => l.EndsWith(", Match!", StringComparison.Ordinal)).ToString("N0"));
		Console.WriteLine("calculated rotors outside of 2^31: " + bufCompletedFile.Count(l => l.EndsWith(", Not Found!", StringComparison.Ordinal)).ToString("N0"));
		Console.WriteLine("Number of rotors verified as being operational: " + bufCompletedFileExtraStatus.Count(l => l.EndsWith(" has been confirmed as functional!", StringComparison.Ordinal)).ToString("N0"));
		Console.WriteLine("Number of duplicate hashes: " + bufCompletedFileUniqueHashWords.Count(l => l.EndsWith(" DUPLICATE!", StringComparison.Ordinal)).ToString("N0"));
		Console.WriteLine("Total number of errors: " + bufErrorFile.Count().ToString("n0"));

		Console.WriteLine(Environment.NewLine + "CollisionSearch finished or stopped.");

		File.WriteAllLines(CompletedFile, bufCompletedFile);
		File.WriteAllLines(CompletedFileExtraStatus, bufCompletedFileExtraStatus);
		File.WriteAllLines(CompletedFileUniqueHashWords, bufCompletedFileUniqueHashWords);
		File.WriteAllLines(ErrorFile, bufErrorFile);

	}
	private static int RtnUnused()
	{
		//Pick a random Int32, since negative numbers are converted to positive, 
		//true range is min = 0, max = 2147483647
		int Rtn;
		while (true)
		{
			Rtn = GetRandomInt32(0, SearchRange);
			if (bufCompletedFile.Any(l => l.StartsWith(Rtn.ToString(), StringComparison.Ordinal)).Equals(false)) {break;}
		}

		bufConsoleWriteLine.Add("Total Completed: " + bufCompletedFile.Count().ToString("N0"));
		return Rtn;
	}

	// Main search logic (refactored path safety and minor correctness tweaks)
	private static void GoCollision(int PredestinedIndex, CheckOpts checkOpts)
	{
		Span<byte> hash32 = stackalloc byte[32];
		Span<byte> bUnique256 = stackalloc byte[256];
		Span<byte> bUniqueSource = stackalloc byte[256];
		using var sha = SHA256.Create();

		const int radix = 256;
		byte[,] b = CreateMachine(2, radix);

		DateTime StartTime;
		DateTime EndTime;

		StartTime = DateTime.Now;

		const int RandomArraySize = radix * 32;
		byte[] bNextTarget = new byte[RandomArraySize];

		switch (checkOpts)
		{
			case CheckOpts.chkPredestined:
				// this is to check that unique values of microsoft Random with the same seed produce the same hash,
				// and that we can find that hash in the lookup files. It is not expected to find a collision with a
				// different int32, but it is possible due to the pigeonhole principle - we just want to confirm that
				// the search logic works as expected for a known target.
				// thus far, after several thousand random targets, results are consistent with expectations.
				// Seed RNG deterministically by the target int
				CreatePredestinedRotor(ref b, PredestinedIndex, RandomArraySize, radix, bUnique256);
				if (optionsRotorDetailCSV.Equals(OptionsRotorDetailCSV.Yes)) 
				{File.WriteAllText(BasePathCompleted + "PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));}
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Predestined, i]; }
				break;
			case CheckOpts.chkMovingCipher:
				ConfigureMovingCipherRotor(ref b, RandomArraySize, PredestinedIndex, radix, bUnique256);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			case CheckOpts.chkPlugBoardNew:
				ConfigurePlugBoardNew(ref b, RandomArraySize, PredestinedIndex, radix, bUnique256);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			case CheckOpts.chkReflectorOri:
				//PredestinedIndex = -587820994;// for testing
				ConfigureReflectorOld(ref b, RandomArraySize, PredestinedIndex, radix, bUnique256);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			case CheckOpts.chkReflectorNew:
				ConfigureReflectorNew(ref b, RandomArraySize, PredestinedIndex, radix, bUnique256);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			default:
				break;
		}

		sha.TryComputeHash(bUniqueSource, hash32,out int bytesWr);
		string targetHash = Convert.ToBase64String(hash32);

		string uniqueCurrent = $"{bUniqueSource[0]},{bUniqueSource[127]},{bUniqueSource[255]}";
        //3,78,18
        var parts = uniqueCurrent.Split(',');

        byte b1 = byte.Parse(parts[0], CultureInfo.InvariantCulture);
        byte b127 = byte.Parse(parts[1], CultureInfo.InvariantCulture);
        byte b255 = byte.Parse(parts[2], CultureInfo.InvariantCulture);

        uint _24bit = ((uint)b1 << 16) | ((uint)b127 << 8) | b255;

		string hitsFileName = $"Hits_{PredestinedIndex}.txt";
		string hitsCompletedPath = Path.Combine(BasePathCompleted, hitsFileName);

		bool Dups = bufCompletedFileUniqueHashWords.Any(l => l.Contains(targetHash));
		if (Dups) { targetHash += " DUPLICATE!"; }
		bufCompletedFileUniqueHashWords.Add(targetHash);

		bufhitsLocalPath.Add($"{DateTime.Now.ToUniversalTime()} GMT{Environment.NewLine}Start CollisionSearch Target Int:{PredestinedIndex}{Environment.NewLine}Unique Hash:{targetHash}");

		string currentHash = string.Empty;
		string errormsg = string.Empty;
		int FilesSearched = 1;

		// ref a single file under Lookups
		string lookupsRoot = "Lookups";
		if (Directory.Exists(lookupsRoot))
		{
			string flnIndex = lookupsRoot + "\\" + $"{bUniqueSource[0]}" + "_index" + ".bin";

			if (File.Exists(flnIndex))
			{
				if (SearchMappedFileIndex(flnIndex, _24bit).Equals(true))
				{
					string flnRotor = lookupsRoot + "\\" + $"{bUniqueSource[0]}.bin";
					if (!File.Exists(flnRotor))
					{ // we might be on a different machine without binary data
						FilesSearched = 0;
						errormsg = DateTime.Now.ToString() + flnRotor + " not found, cannot search!";
						Console.WriteLine(errormsg);
						bufErrorFile.Add(errormsg);
					}

					FileInfo fileinfo = new FileInfo(flnRotor);
					using var mmf = MemoryMappedFile.CreateFromFile(flnRotor, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
					using var accessor = mmf.CreateViewAccessor(0, fileinfo.Length, MemoryMappedFileAccess.Read);
					
					bool firstOnly = true;
					if (checkOpts.Equals(CheckOpts.chkPredestined)) firstOnly= false;// finding all matches for predestined check is useful to
																					 // confirm that all collisions are found for a given int32,
																					 // since we expect some int32 values to produce the same 3
																					 // -byte unique hash (pigeonhole principle). For other
																					 // checks, finding the first match is sufficient, since we
																					 // are not expecting collisions with different int32 values,
																					 // but we want to confirm that the search logic works as
																					 // expected for known targets.
					SearchMappedFileRotor(uniqueCurrent, PredestinedIndex, flnRotor, _24bit, bNextTarget, targetHash, flnRotor, firstOnly, sha, hash32);
				} else
				{

				}
            }
            else
            {
                FilesSearched = 0;
                bufhitsLocalPath.Add($"Lookups root directory not found: {lookupsRoot}");
            }

        }

        var endStr = $"End CollisionSearch has concluded for Int32 = {PredestinedIndex}";
		bufhitsLocalPath.Add($"Files Searched = {FilesSearched},{Environment.NewLine}{endStr}");

		if (optionsHitsFile.Equals(OptionsHitsFile.Yes))
		{
			// write result to completed path
			File.WriteAllLines(hitsCompletedPath, bufhitsLocalPath);

		} else
		{
			if (checkOpts.Equals(CheckOpts.chkPredestined))
			{	// check to see if there were duplicates in hitsLocalPath, then
				// copy it into hitsCompletedPath
				if (bufhitsLocalPath.Count(l => l.Contains("BINGO!")) > 1)
					File.WriteAllLines(hitsCompletedPath, bufhitsLocalPath);
			}
		}
			bufConsoleWriteLine.Add(endStr);	

		EndTime = DateTime.Now;
		//Console.Write("Time Elapsed: " + (EndTime - StartTime).TotalSeconds.ToString("0.00") + " seconds" + Environment.NewLine + Environment.NewLine);
		bufConsoleWriteLine.Add("Time Elapsed: " + (EndTime - StartTime).TotalSeconds.ToString("0.00") + " seconds" + Environment.NewLine + Environment.NewLine);
	}
	public static void SearchMappedFileRotor(string uniqueCurrent,int PredestinedIndex, string filePath, uint target, byte[] bNextTarget,string targetHash, string fln, bool FirstOnly,SHA256 sha, Span<byte> hash32)
	{
		//Binary search is a highly efficient search algorithm that finds the position of a target value within a sorted array.
		//Its time complexity is expressed as O(log N), which means the number of steps required to find an element grows very
		//slowly as the dataset size(N) increases.

		FileInfo fileinfo = new FileInfo(filePath);
		using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
		using var accessor = mmf.CreateViewAccessor(0, fileinfo.Length, MemoryMappedFileAccess.Read);

		long count = fileinfo.Length / 12; // number of Int32 entries

		long low = 0;
		long high = count - 1;
		
		var currentOut = new StringBuilder();
		var lastOut = new StringBuilder();
		try
		{
			while (low <= high)
			{
				long mid = (low + high) >> 1;
				uint key = ReadRecord(accessor, mid).Key24;
				long recordIndex=0;
				if (key == target)
				{// found the first match — now expand outward
					long ori = mid * 12;
					long first = mid;
					long last = mid;
					//Scan backward until the key changes
					while (first > 0)
					{
						uint k = ReadRecord(accessor, first - 1).Key24;
						if (k != key) break;
						first--;
					}
					//Scan forward until the key changes
					while (last < count - 1)
					{
						uint k = ReadRecord(accessor, last + 1).Key24;
						if (k != key) break;
						last++;
					}
					for (long i=first; i <= last; i++)
					{
						string currentHash=string.Empty;
						recordIndex = i;
						RotorRecord currentRotor = ReadRecord(accessor, recordIndex);
						try
						{
							var rnd = new Random(currentRotor.Index);
							rnd.NextBytes(bNextTarget);
							Span<byte> bUniqueTarget = stackalloc byte[256];
							Distinct(ref bUniqueTarget, bNextTarget);

							sha.TryComputeHash(bUniqueTarget, hash32, out int bytesWr);
							currentHash = Convert.ToBase64String(hash32);

							if (currentHash.Equals(targetHash, StringComparison.Ordinal))
							{
								currentOut = new StringBuilder($"BINGO! Match with Int32 = {currentRotor.Index} at lookupInt={currentRotor.Index:N0}, " +
									$"Hash Str:{targetHash}, Fln:{fln}, Line:{recordIndex}");

								if (!lastOut.Equals(currentOut))
								{
									bufhitsLocalPath.Add(currentOut.ToString());

									if (FirstOnly) return;  // for predestined check, we want to find all matches, but since
															// the search order is no longer sequential, there might be
															// multiple matches even if there are only 2 duplicates.
															//
															// For other checks, we want to confirm that the search logic works as expected for known targets,
															// so finding the first match is sufficient.
								}
							}

							lastOut = currentOut;
						}
						catch (Exception ex)
						{
							string errormsg = DateTime.Now.ToString() + fln + " could not be processed for predestined index " +
							currentRotor.Index.ToString() + ", hash of Calculated side: " + currentHash.ToString() + ", skipping file";
							Console.WriteLine(errormsg);
							bufErrorFile.Add(errormsg);
						}
					}
				}

				if (key < target)
					low = mid + 1;
				else
					high = mid - 1;
			}
		}
		catch (Exception ex)
		{

		}
	}

	public static bool SearchMappedFileIndex(string filePath, uint target)
	{
		//Binary search is a highly efficient search algorithm that finds the position of a target value within a sorted array.
		//Its time complexity is expressed as O(log N), which means the number of steps required to find an element grows very
		//slowly as the dataset size(N) increases.

		int t = unchecked((int)target);

		FileInfo fileinfo = new FileInfo(filePath);
		using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
		using var accessor = mmf.CreateViewAccessor(0, fileinfo.Length, MemoryMappedFileAccess.Read);

		long count = fileinfo.Length / 4; // number of Int32 entries

		long low = 0;
		long high = count - 1;

		long O_log_N_Cntr = 0;// for analysis
		while (low <= high)
		{
			O_log_N_Cntr++;
			long mid = (low + high) >> 1;
			int value = accessor.ReadInt32(mid * 4);

			if (value == t)
				return true;

			// For DESCENDING sorted files:
			//if (value > t)
			//	low = mid + 1;
			//else
			//	high = mid - 1;

			// For ASCENDING sorted files:
			if (value < t)
				low = mid + 1;
			else
				high = mid - 1;

		}

		return false;
	}
	private static string BuildCompletionStatusLine(int target, HashSet<string> bufhitsLocalPath)
		=> $"{target}{ComputeSuccessSuffix(target, bufhitsLocalPath)}";

	// If you want the success suffix tied to the Hits file analysis, you could pass it through instead of recomputing.
	private static string ComputeSuccessSuffix(int target, HashSet<string> bufhitsLocalPath)
	{
		// Inspect completed hits file to decide success/fail (optional).
		string successStatus = ", Not Found!";
		try
		{
			bool hasBingo = bufhitsLocalPath.Any(l => l.Contains("BINGO!"));
			bool NoDirectoryExists = bufhitsLocalPath.Any(l => l.Contains("Lookups root directory not found:"));
			if (NoDirectoryExists) successStatus = ", No Directory Found!";
			if (hasBingo) successStatus = ", Match!";
		}
		catch { /* ignore */ }

		return successStatus;
	}

	private static void SafeDelete(string path)
	{
		try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
	}
	static int GetRandomInt32(int minValue, int maxValue)
	{
		if (minValue >= maxValue) throw new ArgumentOutOfRangeException();
		var diff = (long)maxValue - minValue;
		var uint32Buffer = new byte[4];
		using var rng = RandomNumberGenerator.Create();
		while (true)
		{
			rng.GetBytes(uint32Buffer);
			uint rand = BitConverter.ToUInt32(uint32Buffer, 0);
			long remainder = rand % diff;
			if (rand - remainder + (diff - 1) >= rand) // unbiased
				return (int)(minValue + remainder);
		}
	}
	private static byte[,] CreateMachine(int Sides, int Radix)
	{
		byte[,] e = new byte[Sides, Radix];
		return e;
	}
	private static void ConfigureMovingCipherRotor(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix, Span<byte> bUnique)
	{

		CreatePredestinedRotor(ref b, PredestinedIndex, RandomArraySize, radix, bUnique); 
		
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			b[(int)RotorSide.Calculated, b[(int)RotorSide.Predestined, Input]] = (byte)Input;
		}

		if (optionsRotorDetailCSV.Equals(OptionsRotorDetailCSV.Yes))
		{
			File.WriteAllText(BasePathCompleted + "MovingCipher_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));
		}

		if (ValidateMovingCipherRotor(b, radix))
		{
			bufCompletedFileExtraStatus.Add("Success: MovingCipherRotor for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!");
			//SharedFileWriter.WriteLineSafe("Success: MovingCipherRotor for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!", CompletedFileExtraStatus);
		}
		else
		{
			bufCompletedFileExtraStatus.Add("Fail: MovingCipherRotor for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!");
			//SharedFileWriter.WriteLineSafe("Fail: MovingCipherRotor for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!", CompletedFileExtraStatus);
		}
	}

	static bool ValidateMovingCipherRotor(byte[,] b, int radix)
	{
		bool Rtn = true;
		for (int i = 0; i <= 255; i++)
		{
			//Input Predestined Calculated
			//0		149			232
			//149	249			0

			if (!i.Equals(b[(int)RotorSide.Calculated, b[(int)RotorSide.Predestined, i]]))
			{
				Console.WriteLine($"ValidateMovingCipherRotor failed at Input={i}");
				Rtn = false;
			}
		}
		return Rtn;
	}

	private static void ConfigurePlugBoardNew(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix, Span<byte> bUnique)
	{// this is new plugboard, works with input and Calculated (side 1),
	 // Predestined is used to create Calculated, but is not referenced in code
	 //
	 // input	Predestined	Calculated
	 // 0       122         20
	 // 20      111         0

		CreatePredestinedRotor(ref b, PredestinedIndex, RandomArraySize, radix, bUnique);

		byte[,] bHolding = CreateMachine(1, radix);
		/* PlugBoard : igousbtrcpnmefwhqlkavzdyxj
		 * PlugBoard : giuobsrtpcmnfehwlqakzvydjx*/
		for (int Input = 0; Input <= (radix - 2); Input += 2)
		{
			bHolding[(int)RotorSide.Predestined, Input] =
			   b[(int)RotorSide.Predestined, Input + 1];
			bHolding[(int)RotorSide.Predestined, Input + 1] =
			   b[(int)RotorSide.Predestined, Input];
		}
		// now update b
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			b[(int)RotorSide.Calculated, b[(int)RotorSide.Predestined, Input]] =
			 bHolding[(int)RotorSide.Predestined, Input];
		}

		if (optionsRotorDetailCSV.Equals(OptionsRotorDetailCSV.Yes))
		{
			File.WriteAllText(BasePathCompleted + "PlugBoard_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));
		}

		if (ValidatePlugBoardNew(b, radix))
		{
			bufCompletedFileExtraStatus.Add("Success: PlugBoardNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!");
			//SharedFileWriter.WriteLineSafe("Success: PlugBoardNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!", CompletedFileExtraStatus);
		}
		else
		{
			bufCompletedFileExtraStatus.Add("Fail: PlugBoardNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!");
			//SharedFileWriter.WriteLineSafe("Fail: PlugBoardNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!", CompletedFileExtraStatus);
		}

	}
	static bool ValidatePlugBoardNew(byte[,] b, int radix)
	{
		bool Rtn = true;
		// this is new plugboard, works with input and Calculated (side 1),
		// Predestined is used to create Calculated, but is not referenced in code
		//
		// Input Predestined Calculated
		// 3	 81			 119
		// 119	 105	     3

		int start, startCalculated, end;
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			start = Input;
			startCalculated = b[(int)RotorSide.Calculated, Input];
			end = b[(int)RotorSide.Calculated, startCalculated];

			if (!start.Equals(end))
			{
				return false;
			}
		}

		return Rtn;
	}
	private static void ConfigureReflectorNew(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix, Span<byte> bUnique)
	{// this is the new Reflector:
	 // 1. Take opposing inputs (0 and 255)
	 // 2. Take Predestined at input 255 (223)
	 //    and assign to Calculated at input 125,
	 //    which is Predestined at Predestined 0
	 // 3. Resume inwards with input (1 and 254), etc. etc.
	 //
	 // input	Predestined	Calculated
	 //  0	        125	        135
	 //  125	    224	        223
	 //  255	    223	        116

		// input	Predestined	Calculated
		//  1          102         138
		//  102        151         222
		//  254        222         155

		CreatePredestinedRotor(ref b, PredestinedIndex, RandomArraySize, radix, bUnique);

		/*Reflector : phafjdsilcebguwyvkotqzmxrn
		  Reflector : nrxmzqtokvywugbeclisdjfahp*/
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			b[(int)RotorSide.Calculated,
			b[(int)RotorSide.Predestined, Input]] =
			b[(int)RotorSide.Predestined, (radix - 1) - Input];
		}

		if (optionsRotorDetailCSV.Equals(OptionsRotorDetailCSV.Yes))
		{
			File.WriteAllText(BasePathCompleted + "Reflector_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));
		}

		if (ValidateReflectorNew(b, radix))
		{
			bufCompletedFileExtraStatus.Add("Success: ReflectorNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!");
			//SharedFileWriter.WriteLineSafe("Success: ReflectorNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!",CompletedFileExtraStatus);
		}
		else
		{
			bufCompletedFileExtraStatus.Add("Fail: ReflectorNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!");
			//SharedFileWriter.WriteLineSafe("Fail: ReflectorNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!",CompletedFileExtraStatus);
		}


	}

	static bool ValidateReflectorNew(byte[,] b, int radix)
	{
		bool Rtn = true;
		// this is the new Reflector:
		// 1. Take opposing inputs (0 and 255)
		// 2. Take Predestined at input 255 (223)
		//    and assign to Calculated at input 125,
		//    which is Predestined at Predestined 0
		// 3. Resume inwards with input (1 and 254), etc. etc.
		//
		//Input Predestined Calculated
		//0		192			72
		//192	142			134
		//255	134			244

		int start, middlePredestined, middleCalculated, end;
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			start = Input;
			middlePredestined = b[(int)RotorSide.Predestined, Input];
			middleCalculated = b[(int)RotorSide.Calculated, middlePredestined];
			end = b[(int)RotorSide.Predestined, (radix - 1 - Input)];
			if (!middleCalculated.Equals(end))
			{
				return false;
			}
		}

		return Rtn;
	}

	private static void ConfigureReflectorOld(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix, Span<byte> bUnique)
	{
		//this is the Original Reflector, Predestined = Calculated
		CreatePredestinedRotor(ref b, PredestinedIndex, RandomArraySize, radix, bUnique);

		byte[,] bHolding = CreateMachine(2, radix);
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			bHolding[(int)RotorSide.Predestined, radix - Input - 1] =
			   b[(int)RotorSide.Predestined, Input];
			bHolding[(int)RotorSide.Calculated, radix - Input - 1] =
			   b[(int)RotorSide.Calculated, Input];
		}

		/*     
		Reflector : phafjdsilcebguwyvkotqzmxrn
		Reflector : nrxmzqtokvywugbeclisdjfahp*/
		int Query = radix - 1;
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			bHolding[(int)RotorSide.Calculated, (radix - Input - 1)] =
			bHolding[(int)RotorSide.Predestined, Input];
		}

		// now update b
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			b[(int)RotorSide.Predestined, bHolding[0, Input]] =
		 bHolding[(int)RotorSide.Calculated, Input];

			b[(int)RotorSide.Calculated,
		 bHolding[(int)RotorSide.Calculated, Input]] =
		 bHolding[(int)RotorSide.Predestined, Input];
		}

		if (optionsRotorDetailCSV.Equals(OptionsRotorDetailCSV.Yes))
		{
			File.WriteAllText(BasePathCompleted + "ReflectorOri_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));
		}

		if (ValidateReflectorOri(b, radix))
		{
			bufCompletedFileExtraStatus.Add("Success: ReflectorOri for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!");
			//SharedFileWriter.WriteLineSafe("Success: ReflectorOri for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!", CompletedFileExtraStatus);
		}
		else
		{
			bufCompletedFileExtraStatus.Add("Fail: ReflectorOri for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!");
			//SharedFileWriter.WriteLineSafe("Fail: ReflectorOri for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!",CompletedFileExtraStatus);
		}
	}

	static bool ValidateReflectorOri(byte[,] b, int radix)
	{
		bool Rtn = true;
		// this is the old Reflector:
		// This logic was really an accident, I ended up updating both
		// calulated and predestined sides with new calculated values, both sides the same
		// no wonder it looked odd, but it worked.
		// the question now is, does either side (which are identical) come from
		// the large space of permutations or is it a subset of the predestined orders?
		// answer, both sides are from the large space of permutations, and the pattern is
		// stable across all inputs, so it is a valid reflector,
		// albeit with an unusual construction pattern.

		// But first, check to see the old reflector pattern is satified for all entry points:

		// Input Predestined Calculated
		// 0	 12			 12
		// 12	 0			 0

		int start, startPredestined, startCalculated, endPredestined, endCalculated;
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			start = Input;
			startPredestined = b[(int)RotorSide.Predestined, start];
			startCalculated = b[(int)RotorSide.Calculated, start];
			endPredestined = b[(int)RotorSide.Predestined, startCalculated];
			endCalculated = b[(int)RotorSide.Calculated, startCalculated];

			if (!startPredestined.Equals(startCalculated)) return false;
			if (!endPredestined.Equals(endCalculated)) return false;
			if (!Input.Equals(endCalculated)) return false;
		}

		return Rtn;
	}
	private static string ExtractRotorIntoCSV(byte[,] b, int radix)
	{
		string Out = "Input,Predestined,Calculated" + Environment.NewLine;
		for (int i = 0; i < radix; i++)
		{
			Out += i + "," + b[0, i] + "," + b[1, i] + Environment.NewLine;
		}
		return Out;
	}
	private static string RtnCSVEntry(string CSVLine, int NumSemis)
	{
		for (int j = 1; j <= NumSemis; j = j + 1)
		{
			CSVLine = CSVLine.Substring(CSVLine.IndexOf(";") + 1);
		}
		if (CSVLine.IndexOf(";") > 0)
		{
			return CSVLine.Substring(0, CSVLine.IndexOf(";"));
		}
		else
		{
			return CSVLine;
		}
	}
	public static RotorRecord ReadRecord(MemoryMappedViewAccessor acc, long recordIndex)
	{
		long pos = recordIndex * 12;

		RotorRecord r;
		r.Key24 = acc.ReadUInt32(pos);
		r.Index = acc.ReadInt32(pos + 4);
		r.S1 = acc.ReadByte(pos + 8);
		r.S127 = acc.ReadByte(pos + 9);
		r.S255 = acc.ReadByte(pos + 10);
		r.Pad = acc.ReadByte(pos + 11);

		return r;
	}
	private static void CreatePredestinedRotor(ref byte[,] b, Int32 Seed, int RandomArraySize, int Radix, Span<byte> bUnique)
	{
		byte[] bNext = new byte[RandomArraySize]; // Need unique numbers o6nly, this is the available pool, larger than required
												  // we need to re-seed each rotor with stored 4 byte number
		System.Random oRandom = new System.Random(Seed);
		oRandom.NextBytes(bNext);
		Distinct(ref bUnique, bNext);

		if (bUnique.Length.Equals(Radix))
		{
			for (int Input = 0; Input <= (Radix - 1); Input++)
			{
				b[(int)RotorSide.Predestined, Input] = bUnique[Input];
			}
		}
		else
		{
			string Out = "Rotor does not contain " + Radix.ToString() + " distinct numbers! Seed = " + Seed.ToString("N0") + Environment.NewLine;
			Console.Write(Out);
			Console.ReadKey();
		}

	}
	public static void Distinct(ref Span<byte> output, byte[] input)
	{
		Span<bool> seen = stackalloc bool[256];
		int pos = 0;

		for (int i = 0; i < input.Length && pos < 256; i++)
		{
			byte v = input[i];
			if (!seen[v])
			{
				seen[v] = true;
				output[pos++] = v;
			}
		}

		// shrink the span to the number of unique bytes
		output = output[..pos];
	}
}
