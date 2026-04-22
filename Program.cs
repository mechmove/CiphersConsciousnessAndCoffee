using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Net.NetworkInformation;
using System.Threading;

class Program
{
	static void Main(string[] args)
	{
		var cts = new CancellationTokenSource();
		var token = cts.Token;

		// Background key listener
		Task.Run(() =>
		{
			Console.WriteLine("Press 'E' to exit..." + Environment.NewLine);
			ConsoleKeyInfo keyInfo;
			do
			{
				keyInfo = Console.ReadKey(true); // 'true' hides the key
			} while (keyInfo.Key != ConsoleKey.E);
			cts.Cancel();
		});

		try
		{
			CollisionSearchService.CollisionSearch(token);
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Search cancelled.");
		}

		Console.Write("All tests are completed, check your results and press any key to close this box" + Environment.NewLine);
		Console.ReadKey();
	}

}

