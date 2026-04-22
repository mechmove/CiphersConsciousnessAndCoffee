using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ConsoleApp1
{
	internal class Program
	{
		static void Main(string[] args)
		{
			// this is a one‑time utility to convert the raw triples in the Lookups folder into 24‑bit keys for
			// faster lookup later.
			// this program must run to the end, re-starting will cause duplicates in the output files, therefore
			// delete all output "_index.txt" files prior to re-running if you want a clean run.
			string baseDir = @"C:\Users\{your username}\source\repos\CiphersConsciousnessAndCoffee\bin\Debug\net8.0\Lookups\";
			if (!Directory.Exists(baseDir))
			{
				Console.WriteLine("Base directory does not exist: " + baseDir);
				return;
			}

			for (int j = 0; j <= 255; j++)
			{
				string fln = baseDir +  j.ToString() + ".txt";
				if (!File.Exists(fln))
					continue;

				Console.WriteLine($"{DateTime.Now:T} Starting {fln}");

				// 1) Deduplicate triples using a HashSet
				var uniqueTriples = new HashSet<string>(StringComparer.Ordinal);

				using (var sr = new StreamReader(fln))
				{
					string? line;
					int currentRecord = 0;

					while ((line = sr.ReadLine()) != null)
					{
						currentRecord++;
						if (currentRecord % 100_000 == 0)
						{
							Console.WriteLine($"{DateTime.Now:T} Reading {fln} ({currentRecord:n0})");
						}

						// Split once, reuse
						var parts = line.Split(',');
						//if (parts.Length <= 31) continue; // safety

						// fields 1, 2, 31 (0‑based indexing)
						string s1 = parts[1];
						string s127 = parts[2];
						string s255 = parts[3];

						string raw = $"{s1},{s127},{s255}";
						uniqueTriples.Add(raw); // HashSet handles dedup
					}
				}

				Console.WriteLine($"{DateTime.Now:T} Unique triples for {fln}: {uniqueTriples.Count:n0}");

				// 2) Convert triples to 24‑bit keys using bit‑shifts
				var keys = new List<uint>(uniqueTriples.Count);

				int i = 0;
				foreach (var triple in uniqueTriples)
				{
					if (i % 100_000 == 0)
					{
						Console.WriteLine($"{DateTime.Now:T} Converting {fln} ({i:n0}/{uniqueTriples.Count:n0})");
					}
					i++;

					// triple is "a,b,c"
					var parts = triple.Split(',');
					if (parts.Length != 3) continue;

					byte b1 = byte.Parse(parts[0], CultureInfo.InvariantCulture);
					byte b127 = byte.Parse(parts[1], CultureInfo.InvariantCulture);
					byte b255 = byte.Parse(parts[2], CultureInfo.InvariantCulture);

					uint key = ((uint)b1 << 16) | ((uint)b127 << 8) | b255;
					keys.Add(key);
				}

				// 3) Write out as text (for now) – one key per line
				string outFile = baseDir + j.ToString() + "_index.txt";
				using (var sw = new StreamWriter(outFile, true))
				{
					foreach (var key in keys)
					{
						sw.WriteLine(key.ToString(CultureInfo.InvariantCulture));
					}
				}

				Console.WriteLine($"{DateTime.Now:T} Wrote {outFile} ({keys.Count:n0} keys)");
			}

			Console.WriteLine($"Process completed!");
			Console.ReadKey();
		}
	}
}