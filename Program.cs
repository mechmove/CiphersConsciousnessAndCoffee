using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace ConsoleApp1
{
	internal class Program
	{
		static void Main(string[] args)
		{
			// this is the final step, lets this go to the end, all binary files will be ready

			string baseDir = @"C:\Users\{your username}\source\repos\CiphersConsciousnessAndCoffee\bin\Debug\net8.0\Lookups\";
			string baseDirWorkspace = @"C:\Users\{your username}\source\repos\CiphersConsciousnessAndCoffee\bin\Debug\net8.0\workspace\";

			if (!Directory.Exists(baseDir))
			{
				Console.WriteLine("Base directory does not exist: " + baseDir);
				return;
			}
			if (!Directory.Exists(baseDirWorkspace))
			{
				Console.WriteLine("Workspace directory does not exist: " + baseDirWorkspace);
				return;
			}

			for (int j = 0; j <= 255; j++)
			{
				string txtfln = baseDir + $@"{j}_index.txt";
				string txtRotorfln = baseDir + $@"{j}.txt";
				string binfln = baseDir + $@"{j}_index.bin";

				if (File.Exists(txtfln)&& File.Exists(txtRotorfln))
				{
					Console.WriteLine($"{DateTime.Now:T} Starting {txtfln}");
					FileConverter.ConvertTextToBinary(txtfln, binfln);

					FileConverter.SortBinary(binfln);

					FileConverter.GetBinaryRowcnt(binfln);

					FileConverter.ConvertRotorTextToBinary(txtRotorfln, baseDir + $@"{j}.bin");
					FileConverter.newSortBinary(baseDir + $@"{j}.bin");
				}
			}

			// create needed folders to support new options.txt config:
			// (if already exists, nothing will happen)
			Directory.CreateDirectory(baseDirWorkspace + "\\chkMovingCipher\\newCollision\\CollisionSearch"); // ensure it exists, even if empty;
			Directory.CreateDirectory(baseDirWorkspace + "\\chkMovingCipher\\newCollision\\CollisionSearchCompleted"); // ensure it exists, even if empty;

			Directory.CreateDirectory(baseDirWorkspace + "\\chkPredestined\\newCollision\\CollisionSearch"); // ensure it exists, even if empty;
			Directory.CreateDirectory(baseDirWorkspace + "\\chkPredestined\\newCollision\\CollisionSearchCompleted"); // ensure it exists, even if empty;

			Directory.CreateDirectory(baseDirWorkspace + "\\chkPlugBoardNew\\newCollision\\CollisionSearch"); // ensure it exists, even if empty;
			Directory.CreateDirectory(baseDirWorkspace + "\\chkPlugBoardNew\\newCollision\\CollisionSearchCompleted"); // ensure it exists, even if empty;

			Directory.CreateDirectory(baseDirWorkspace + "\\chkReflectorOri\\newCollision\\CollisionSearch"); // ensure it exists, even if empty;
			Directory.CreateDirectory(baseDirWorkspace + "\\chkReflectorOri\\newCollision\\CollisionSearchCompleted"); // ensure it exists, even if empty;

			Directory.CreateDirectory(baseDirWorkspace + "\\chkReflectorNew\\newCollision\\CollisionSearch"); // ensure it exists, even if empty;
			Directory.CreateDirectory(baseDirWorkspace + "\\chkReflectorNew\\newCollision\\CollisionSearchCompleted"); // ensure it exists, even if empty;

			Console.WriteLine($"Process completed!");
			Console.ReadKey();


		}
	}
}