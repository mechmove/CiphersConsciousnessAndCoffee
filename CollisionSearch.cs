using Hashing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
public static class CollisionSearchService
{
	public enum CheckOpts { chkPredestined = 1, chkMovingCipher = 2,
							chkPlugBoardNew = 3, 
							chkReflectorOri = 4, chkReflectorNew = 5};
	private enum RotorSide : int { Predestined = 0, Calculated = 1 }

	// Assumes these are set appropriately elsewhere
	public static string BasePath = string.Empty;
	public static string BasePathCompleted = string.Empty;
	public static string HardstopFln = string.Empty;

	private static string CompletedFile = string.Empty;
	private static string CompletedFileExtraStatus = string.Empty;
	private static string CompletedFileUniqueHashWords = string.Empty;

	public static int HardStop= 100;
	public static void CollisionSearch(CheckOpts checkOpts, CancellationToken? ct = null)
	{
		string baseDirectory = AppContext.BaseDirectory;
		Directory.CreateDirectory(baseDirectory ); // ensure it exists, even if empty;
		Directory.CreateDirectory(baseDirectory + "\\Lookups"); // ensure it exists, even if empty;

		switch (checkOpts)
		{
			case CheckOpts.chkPredestined:
				BasePath = @"workspace\chkPredestined\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkPredestined\newCollision\CollisionSearchCompleted\";
				Console.WriteLine("CollisionSearch will run with GoCollisionSearch_chkPredestined logic.");
				break;
			case CheckOpts.chkMovingCipher:
				BasePath = @"workspace\chkMovingCipher\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkMovingCipher\newCollision\CollisionSearchCompleted\";
				Console.WriteLine("CollisionSearch will run with GoCollisionSearch_chkMovingCipher logic.");
				break;
			case CheckOpts.chkPlugBoardNew:
				BasePath = @"workspace\chkPlugBoardNew\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkPlugBoardNew\newCollision\CollisionSearchCompleted\";
				Console.WriteLine("CollisionSearch will run with GoCollisionSearch_chk_chkPlugBoardNew logic.");
				break;
			case CheckOpts.chkReflectorOri:
				BasePath = @"workspace\chkReflectorOri\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkReflectorOri\newCollision\CollisionSearchCompleted\";
				Console.WriteLine("CollisionSearch will run with GoCollisionSearch_chkReflectorOri logic.");
				break;
			case CheckOpts.chkReflectorNew:
				BasePath = @"workspace\chkReflectorNew\newCollision\CollisionSearch\";
				BasePathCompleted = @"workspace\chkReflectorNew\newCollision\CollisionSearchCompleted\";
				Console.WriteLine("CollisionSearch will run with GoCollisionSearch_chkReflectorNew logic.");
				break;
			default:
				Console.WriteLine("Invalid CheckOpts value. Stopping.");
				return;
		}

		CompletedFile = Path.Combine(BasePathCompleted, "Completed.txt");
		CompletedFileExtraStatus = Path.Combine(BasePathCompleted, "Completed_RotorFunctionality.txt");
		CompletedFileUniqueHashWords= Path.Combine(BasePathCompleted, "Completed_UniqueHashStatus.txt");
		HardstopFln = Path.Combine(BasePathCompleted, "Hardstop.txt");

		if (File.Exists(HardstopFln))
		{
			HardStop = Convert.ToInt32(File.ReadAllText(HardstopFln));
		}

		DateTime ProcessStartTime = DateTime.Now;

		Int32 currentProcessCnt = 0;
		Int32 TotalProcessCnt = 0;
		if (File.Exists(CompletedFile))
		{
			TotalProcessCnt = File.ReadAllLines(CompletedFile).Count();
			if (TotalProcessCnt >= (HardStop))
			{
				Console.WriteLine($"Hard stop limit of {HardStop} already reached in Completed.txt." + Environment.NewLine + "Stopping search." + Environment.NewLine + Environment.NewLine);
				SummaryOfResults(TotalProcessCnt);
				Console.ReadKey();
				return;
			}
		}

		Directory.CreateDirectory(BasePath);
		Directory.CreateDirectory(BasePathCompleted);

		// Optional: startup sweep of stale pending markers (commented if multiple instances may run)
		foreach (var pending in Directory.GetFiles(BasePath, "*_Pending")) SafeDelete(pending);
		foreach (var pending in Directory.GetFiles(BasePath, "Hits_*")) SafeDelete(pending);

		var options = new ParallelOptions
		{
			MaxDegreeOfParallelism = 1 // do not change this number, as the logic is designed for
									   // single-threaded execution to avoid file contention and
									   // manage resources effectively. If you want to run multiple
									   // instances, consider running separate processes instead of increasing this value.
		};

		// Produce work continuously; each worker picks targets independently
		Parallel.For(0, int.MaxValue, options, (i, state) =>
		{
			if (ct is { IsCancellationRequested: true })
			{
				SummaryOfResults(TotalProcessCnt);
				state.Stop();
				return;
			}

			//Pick a random Int32, since negative numbers are converted to positive, 
			//true range is min = 0, max = 2147483647
			int target = GetRandomInt32(0, 2147483647);

			// Try to claim work for this target with an atomic pending file
			string pendingPath = Path.Combine(BasePath, $"{target}_Pending");
			if (!TryClaimPending(pendingPath))
			{
				// Someone else owns this target; pick another
				return;
			}

			try
			{
				Console.WriteLine($"=============================START===================================");
				// If target already completed, skip
				if (IsCompleted(target))
				{
					return;
				}

				Console.WriteLine($"Generated random Int32 target for CollisionSearch: {target}");
				// Perform the collision search
				currentProcessCnt++;
				TotalProcessCnt++;

				string hitsLocalPath = hitsLocalPath = GoCollision(target, checkOpts);
				Console.WriteLine("total number checked: " + $"{TotalProcessCnt}");
				double TimeElapsed = (DateTime.Now - ProcessStartTime).TotalSeconds;
				Console.WriteLine("total time elapsed: " + Math.Round(TimeElapsed / 60, 2) + " minutes");
				Console.WriteLine("Average items produced every " + Math.Round(TimeElapsed / currentProcessCnt, 2) + " seconds.");
				Console.WriteLine($"=============================END=====================================" + Environment.NewLine);
				// Mark completion (use shared-safe writer for cross-process safety)
				var resultLine = BuildCompletionStatusLine(target, hitsLocalPath);
				SharedFileWriter.WriteLineSafe(resultLine, CompletedFile);
				if (TotalProcessCnt.Equals(HardStop))
				{
					SummaryOfResults(TotalProcessCnt);
					Console.WriteLine($"Hard stop limit of {HardStop} reached. Stopping search.");
					Console.ReadKey();
					return;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error on target {target}: {ex.Message}");
			}
			finally
			{
				// Release ownership
				SafeDelete(pendingPath);
			}
		});
	}

	private static void SummaryOfResults(Int32 TotalProcessCnt)
	{
		Console.WriteLine("Summary of Results: " + Environment.NewLine);
		Console.WriteLine("total rotors number checked: " + $"{TotalProcessCnt}");
		var lines = File.ReadAllLines(CompletedFile);
		Console.WriteLine("calculated rotors outside of 2^31: " + lines.Count(l => l.EndsWith(", Not Found!", StringComparison.Ordinal)));
		lines = File.ReadAllLines(CompletedFileExtraStatus);
		Console.WriteLine("Number of rotors verified as being operational: " + lines.Count(l => l.EndsWith(" has been confirmed as functional!", StringComparison.Ordinal)));
		lines = File.ReadAllLines(CompletedFileUniqueHashWords);
		Console.WriteLine("Number of duplicate hashes: " + lines.Count(l => l.EndsWith(" DUPLICATE!", StringComparison.Ordinal)));

		Console.WriteLine(Environment.NewLine + "CollisionSearch finished or stopped.");

	}
	// Atomic claim: only one thread/process can create this file
	private static bool TryClaimPending(string pendingPath)
	{
		try
		{
			using var fs = new FileStream(pendingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
			return true;
		}
		catch (IOException)
		{
			return false; // already exists
		}
	}

	// Check completion by scanning Completed.txt for the exact target; avoids race by not using Exists+WriteAllText
	private static bool IsCompleted(int target)
	{
		if (!File.Exists(CompletedFile)) return false;
		// Read safely (best-effort; SharedFileWriter handles writes)
		try
		{
			var lines = File.ReadAllLines(CompletedFile);
			Console.WriteLine( "Total Completed: " + lines.Count().ToString());
			return lines.Any(l => l.StartsWith(target.ToString(), StringComparison.Ordinal));
		}
		catch
		{
			// If we can't read, assume not completed to avoid false positives
			return false;
		}
	}

	// Main search logic (refactored path safety and minor correctness tweaks)
	private static string GoCollision(int PredestinedIndex, CheckOpts checkOpts)
	{
		const int radix = 256;
		byte[,] b = CreateMachine(2, radix);

		DateTime StartTime;
		DateTime EndTime;
		
		StartTime = DateTime.Now;
		
		const int RandomArraySize = radix * 32;
		var bNextTarget = new byte[RandomArraySize];
		byte[] bUniqueSource = new byte[256];

		switch (checkOpts)
		{
			case CheckOpts.chkPredestined:
				// this is to check that unique values of microsoft Random with the same seed produce the same hash,
				// and that we can find that hash in the lookup files. It is not expected to find a collision with a
				// different int32, but it is possible due to the pigeonhole principle - we just want to confirm that
				// the search logic works as expected for a known target.
				// thus far, after several thousand random targets, results are consistent with expectations.
				// Seed RNG deterministically by the target int
				CreatePredestinedRotor(ref b, PredestinedIndex, RandomArraySize, radix);
				for (int i = 0; i < radix; i++) {bUniqueSource[i] = b[(int)RotorSide.Predestined, i];}
				break;
			case CheckOpts.chkMovingCipher:
				ConfigureMovingCipherRotor(ref b, RandomArraySize, PredestinedIndex, radix);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			case CheckOpts.chkPlugBoardNew:
				ConfigurePlugBoardNew(ref b, RandomArraySize, PredestinedIndex, radix);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			case CheckOpts.chkReflectorOri:
				//PredestinedIndex = -587820994;// for testing
				ConfigureReflectorOld(ref b, RandomArraySize, PredestinedIndex, radix);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			case CheckOpts.chkReflectorNew:
				ConfigureReflectorNew(ref b, RandomArraySize, PredestinedIndex, radix);
				for (int i = 0; i < radix; i++) { bUniqueSource[i] = b[(int)RotorSide.Calculated, i]; }
				break;
			default:
				break;
		}

		string targetHash = Sha256.ComputeSha256Hash(bUniqueSource);
		string uniqueCurrent = $"{bUniqueSource[0]},{bUniqueSource[127]},{bUniqueSource[255]}";

		string hitsFileName = $"Hits_{PredestinedIndex}.txt";
		string hitsLocalPath = Path.Combine(BasePath, hitsFileName);
		string hitsCompletedPath = Path.Combine(BasePathCompleted, hitsFileName);

		if (File.Exists(CompletedFileUniqueHashWords))
		{
			string[] Hashes = File.ReadAllLines(CompletedFileUniqueHashWords);
			bool Dups = Hashes.Any(l => l.Contains(targetHash));
			if (Dups) { targetHash += " DUPLICATE!"; }
		}
		File.AppendAllText(CompletedFileUniqueHashWords, targetHash + Environment.NewLine);

		File.WriteAllText(
			hitsLocalPath,
			$"{DateTime.Now.ToUniversalTime()} GMT{Environment.NewLine}Start CollisionSearch Target Int:{PredestinedIndex}{Environment.NewLine}Unique Hash:{targetHash}{Environment.NewLine}");

		int dirCount = 0;
		string currentHash = string.Empty;

		// Enumerate directories under Lookups
		string lookupsRoot = "Lookups";
		if (Directory.Exists(lookupsRoot))
		{
			var directories = Directory.EnumerateDirectories(lookupsRoot, "*", SearchOption.TopDirectoryOnly);
			foreach (var dir in directories)
			{
				dirCount++;

				// Each directory is expected to contain Lookups_<byte>.txt files
				string fln = Path.Combine(dir, $"Lookups_{bUniqueSource[0]}.txt");
				if (!File.Exists(fln)) continue;

				string[] lines;
				try
				{
					lines = File.ReadAllLines(fln);
				}
				catch
				{
					continue; // skip unreadable files
				}
				int linNum= 0;
				foreach (var line in lines)
				{
					linNum++;
					int commaIdx = line.IndexOf(',');
					if (commaIdx <= 0) continue;

					if (!int.TryParse(line.Substring(0, commaIdx), out int lookupInt)) continue;

					if (line.Substring(commaIdx + 1).Equals(uniqueCurrent, StringComparison.Ordinal))
					{
						// Recompute unique and hash for the lookupInt
						var rnd = new Random(lookupInt);
						rnd.NextBytes(bNextTarget);
						var bUniqueTarget = bNextTarget.Distinct().ToArray();
						currentHash = Sha256.ComputeSha256Hash(bUniqueTarget);
						if (currentHash.Equals(targetHash, StringComparison.Ordinal))
						{
							File.AppendAllText(
								hitsLocalPath,
								$"BINGO! Match with Int32 = {PredestinedIndex} at lookupInt={lookupInt:N0}, Hash Str:{targetHash}, Fln:{fln}, Line:{linNum} {Environment.NewLine}");
						}
					}
				}
			}
		} else
		{
			File.AppendAllText(hitsLocalPath, $"Lookups root directory not found: {lookupsRoot}{Environment.NewLine}");
		}

		var endStr = $"End CollisionSearch has concluded for Int32 = {PredestinedIndex}";
		File.AppendAllText(hitsLocalPath, $"Directorys Searched = {dirCount},{Environment.NewLine}{endStr}");

		// Copy result to completed path and delete local
		File.Copy(hitsLocalPath, hitsCompletedPath, true);
		SafeDelete(hitsLocalPath);

		Console.WriteLine(endStr);

		EndTime = DateTime.Now;
		Console.Write("Time Elapsed: " + (EndTime - StartTime).TotalSeconds.ToString("0.00") + " seconds" + Environment.NewLine + Environment.NewLine);

		return hitsCompletedPath;
	}

	private static string BuildCompletionStatusLine(int target, string hitsCompletedPath)
		=> $"{target}{ComputeSuccessSuffix(target, hitsCompletedPath)}";

	// If you want the success suffix tied to the Hits file analysis, you could pass it through instead of recomputing.
	private static string ComputeSuccessSuffix(int target, string hitsCompletedPath)
	{
		// Inspect completed hits file to decide success/fail (optional).
		string successStatus = ", Not Found!";
		try
		{
			string[] c = File.ReadAllLines(hitsCompletedPath);
			bool hasBingo = c.Any(l => l.Contains("BINGO!"));
			bool NoDirectoryExists = c.Any(l => l.Contains("Lookups root directory not found:"));
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
	private static void CreatePredestinedRotor(ref byte[,] b, Int32 Seed, int RandomArraySize, int Radix)
	{
		byte[] bNext = new byte[RandomArraySize]; // Need unique numbers o6nly, this is the available pool, larger than required
												  // we need to re-seed each rotor with stored 4 byte number
		System.Random oRandom = new System.Random(Seed);
		oRandom.NextBytes(bNext);
		byte[] bUnique = bNext.Distinct().ToArray();
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
	private static void ConfigureMovingCipherRotor(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix)
        {

		byte[] bNext = new byte[RandomArraySize]; // Need unique numbers o6nly, this is the available pool, larger than required
												  // we need to re-seed each rotor with stored 4 byte number
		System.Random oRandom = new System.Random(PredestinedIndex);
		oRandom.NextBytes(bNext);
		byte[] bUnique = bNext.Distinct().ToArray();
		if (bUnique.Length.Equals(radix))
		{
			for (int Input = 0; Input <= (radix - 1); Input++)
			{
				b[(int)RotorSide.Predestined, Input] = bUnique[Input];
			}
		}

		for (int Input = 0; Input <= (radix - 1); Input++)
            {
                b[(int)RotorSide.Calculated, b[(int)RotorSide.Predestined, Input]] = (byte)Input;
            }
            File.WriteAllText("RotorType.txt", "MovingCipher");
            File.WriteAllText(BasePathCompleted + "MovingCipher_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));

		if (ValidateMovingCipherRotor(b, radix))
		{
			SharedFileWriter.WriteLineSafe("Success: MovingCipherRotor for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!", 
				CompletedFileExtraStatus);
		} else
		{
			SharedFileWriter.WriteLineSafe("Fail: MovingCipherRotor for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!", 
				CompletedFileExtraStatus);
		}
	}

	static bool ValidateMovingCipherRotor(byte[,] b, int radix)
	{
		bool Rtn = true;
		for (int i=0; i<=255;i++)
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

	private static void ConfigurePlugBoardNew(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix)
	{// this is new plugboard, works with input and Calculated (side 1),
	 // Predestined is used to create Calculated, but is not referenced in code
	 //
	 // input	Predestined	Calculated
	 // 0       122         20
	 // 20      111         0

		byte[] bNext = new byte[RandomArraySize]; // Need unique numbers o6nly, this is the available pool, larger than required
												  // we need to re-seed each rotor with stored 4 byte number
		System.Random oRandom = new System.Random(PredestinedIndex);
		oRandom.NextBytes(bNext);
		byte[] bUnique = bNext.Distinct().ToArray();
		if (bUnique.Length.Equals(radix))
		{
			for (int Input = 0; Input <= (radix - 1); Input++)
			{
				b[(int)RotorSide.Predestined, Input] = bUnique[Input];
			}
		}

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

		File.WriteAllText("RotorType.txt", "PlugBoard");
		File.WriteAllText(BasePathCompleted + "PlugBoard_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));

		if (ValidatePlugBoardNew(b, radix))
		{
			SharedFileWriter.WriteLineSafe("Success: PlugBoardNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!",
				CompletedFileExtraStatus);
		}
		else
		{
			SharedFileWriter.WriteLineSafe("Fail: PlugBoardNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!",
				CompletedFileExtraStatus);
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
	private static void ConfigureReflectorNew(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix)
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

		byte[] bNext = new byte[RandomArraySize]; // Need unique numbers only, this is the available pool, larger than required
												  // we need to re-seed each rotor with stored 4 byte number
		System.Random oRandom = new System.Random(PredestinedIndex);
		oRandom.NextBytes(bNext);
		byte[] bUnique = bNext.Distinct().ToArray();
		if (bUnique.Length.Equals(radix))
		{
			for (int Input = 0; Input <= (radix - 1); Input++)
			{
				b[(int)RotorSide.Predestined, Input] = bUnique[Input];
			}
		}

		/*Reflector : phafjdsilcebguwyvkotqzmxrn
		  Reflector : nrxmzqtokvywugbeclisdjfahp*/
		for (int Input = 0; Input <= (radix - 1); Input++)
		{
			b[(int)RotorSide.Calculated,
			b[(int)RotorSide.Predestined, Input]] =
			b[(int)RotorSide.Predestined, (radix - 1) - Input];
		}
		File.WriteAllText("RotorType.txt", "Reflector");
		File.WriteAllText(BasePathCompleted + "Reflector_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));

		if (ValidateReflectorNew(b, radix))
		{
			SharedFileWriter.WriteLineSafe("Success: ReflectorNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!",
				CompletedFileExtraStatus);
		}
		else
		{
			SharedFileWriter.WriteLineSafe("Fail: ReflectorNew for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!",
				CompletedFileExtraStatus);
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

	private static void ConfigureReflectorOld(ref byte[,] b, Int32 RandomArraySize, Int32 PredestinedIndex, int radix)
	{
		//this is the Original Reflector, Predestined = Calculated
		byte[] bNext = new byte[RandomArraySize]; // Need unique numbers only, this is the available pool, larger than required
												  // we need to re-seed each rotor with stored 4 byte number
		System.Random oRandom = new System.Random(PredestinedIndex);
		oRandom.NextBytes(bNext);
		byte[] bUnique = bNext.Distinct().ToArray();
		if (bUnique.Length.Equals(radix))
		{
			for (int Input = 0; Input <= (radix - 1); Input++)
			{
				b[(int)RotorSide.Predestined, Input] = bUnique[Input];
			}
		}

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

		File.WriteAllText("RotorType.txt", "ReflectorOri");
		File.WriteAllText(BasePathCompleted + "ReflectorOri_PredestinedIndex_" + PredestinedIndex.ToString() + ".csv", ExtractRotorIntoCSV(b, radix));

		if (ValidateReflectorOri(b, radix))
		{
			SharedFileWriter.WriteLineSafe("Success: ReflectorOri for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed as functional!",
				CompletedFileExtraStatus);
		}
		else
		{
			SharedFileWriter.WriteLineSafe("Fail: ReflectorOri for PredestinedIndex: " + PredestinedIndex.ToString() + " has been confirmed to be NOT functional!",
				CompletedFileExtraStatus);
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

}
