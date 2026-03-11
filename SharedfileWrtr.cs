using System;
using System.IO;
using System.Threading;

public class SharedFileWriter
{// code is from Google search "C# write to file from multiple processes safely"
	private static Mutex fileMutex;
	public static void WriteLineSafe(string line, string filePath)
	{
		// Name the Mutex so it is shared across processes
		string mutexName = "Global\\MySharedFileMutex";

		try
		{
			// Create or open the named Mutex
			fileMutex = new Mutex(false, mutexName);

			// Wait until the Mutex is free (current process acquires ownership)
			fileMutex.WaitOne();

			// All file operations must occur within the Mutex lock
			using (FileStream fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
			using (StreamWriter sw = new StreamWriter(fs))
			{
				//sw.WriteLine($"{DateTime.Now}: {line}");
				sw.WriteLine($"{line}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An error occurred: {ex.Message}");
		}
		finally
		{
			// Release the Mutex to allow other processes to acquire it
			if (fileMutex != null)
			{
				fileMutex.ReleaseMutex();
				fileMutex.Dispose(); // Best practice to dispose of the Mutex when done with the operation
			}
		}
	}
}

