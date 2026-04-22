using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;

namespace ConsoleApp1
{
	internal class Program
	{
		static void Main(string[] args)
		{
			string baseDir = @"C:\Users\{your username}\source\repos\CiphersConsciousnessAndCoffee\bin\Debug\net8.0\Lookups\";

			if (!Directory.Exists(baseDir))
			{
				Console.WriteLine("Base directory does not exist: " + baseDir);
				return;
			}

			int RecordCnt =0;
			for (int j = 0; j <= 255; j++)
			{
				string Fln = baseDir + j.ToString() + ".txt";
				if (File.Exists(Fln))
				{
					Console.WriteLine($"Processing {Fln}...");	
					RecordCnt += File.ReadLines(Fln).Count();
				}
			}
			Console.WriteLine($"Total Records: {RecordCnt.ToString("n0")}");	
			Console.ReadKey();
		}
	}
}
