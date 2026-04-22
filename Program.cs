using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;

internal class Program
{
	// this program is to delete original lookup files and folders as to avoid recycling in windows.
	static void Main(string[] args)
	{
		string baseDir = @"C:\Users\{your username}\source\repos\CiphersConsciousnessAndCoffee\bin\Debug\net8.0\Lookups\";
		if (!Directory.Exists(baseDir))
		{
			Console.WriteLine("Base directory does not exist: " + baseDir);
			return;
		}

		for (long i = 0; i <= 2_147_000_000; i += 1_000_000)
		{
			Console.WriteLine($"Iteration {i.ToString("n0")}");
			for (int j = 0; j <= 255; j++)
			{
				string currentFln = baseDir + "Lookups_" + i.ToString() + ".txt\\Lookups_" + j.ToString() + ".txt";
				if (File.Exists(currentFln))
				{
					Console.WriteLine("deleting " + currentFln);
					File.Delete(currentFln);
				}
			}
			if (Directory.Exists(baseDir + "Lookups_" + i.ToString() + ".txt"))
			{
				Console.WriteLine("deleting " + baseDir + "Lookups_" + i.ToString() + ".txt");
				Directory.Delete(baseDir + "Lookups_" + i.ToString() + ".txt", true);
			}
		}

		Console.WriteLine($"Process completed!");
		Console.ReadKey();


	}

}
