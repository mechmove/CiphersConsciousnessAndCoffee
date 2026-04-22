using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;

namespace ConsoleApp1
{
	internal class Program
	{
		// This is the first step in converting existing data files produced from the original branch:CreateDataFiles.
		// This MUST run until completed, otherwise you will need to delete new files and start over.
		// Change the baseDir to match your own folder structure.
		static void Main(string[] args)
		{
			string baseDir = @"C:\Users\{your username}\source\repos\CiphersConsciousnessAndCoffee\bin\Debug\net8.0\Lookups\";

			if (!Directory.Exists( baseDir))
			{
				Console.WriteLine("Base directory does not exist: " + baseDir);
				return;
			}

			for (long i = 0; i <= 2_147_000_000; i += 1_000_000)
			{
				Console.WriteLine($"Iteration {i.ToString("n0")}");
				for (int j = 0; j <= 255; j++)
				{
					string end = "Lookups_" + i.ToString() + ".txt\\Lookups_" + j.ToString() + ".txt";
					string currentFln = Path.Combine(baseDir, end);
					if (File.Exists(currentFln))
					{
						string Fln = baseDir + j.ToString() + ".txt";
						//Console.WriteLine("Appending " + currentFln +" to " + Fln);
						File.AppendAllLines(Fln, File.ReadAllLines(currentFln));
					}
				}
			}
			Console.WriteLine($"Process completed!");
			Console.ReadKey();

		}

	}
}
