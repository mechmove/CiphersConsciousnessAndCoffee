using System;
using System.IO;

public class FileConverter
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
	public static void ConvertTextToBinary(string inputTextPath, string outputBinaryPath)
	{
		using (StreamReader sr = new StreamReader(inputTextPath))
		using (BinaryWriter bw = new BinaryWriter(File.Open(outputBinaryPath, FileMode.Create)))
		{
			string line;
			while ((line = sr.ReadLine()) != null)
			{
				if (int.TryParse(line, out int number))
				{
					bw.Write(number); // Writes 4 bytes (Int32)
				}
			}
		}
	}

	public static void SortBinary(string inputBinaryPath)
	{
		List<int> numbers = new List<int>();
		numbers = File.ReadAllBytes(inputBinaryPath).Select((b, i) => new { Byte = b, Index = i })
			.GroupBy(x => x.Index / 4)
			.Select(g => BitConverter.ToInt32(g.Select(x => x.Byte).ToArray(), 0))
			.ToList();
		numbers.Sort();
		File.WriteAllBytes(inputBinaryPath, numbers.SelectMany(n => BitConverter.GetBytes(n)).ToArray());
	}

	public static void GetBinaryRowcnt(string inputBinaryPath)
	{
		List<int> numbers = new List<int>();
		int Cnt = File.ReadAllBytes(inputBinaryPath).Length/4;
	}

	public static void ConvertRotorTextToBinary(string inputPath, string outputPath)
	{
		using var sr = new StreamReader(inputPath);
		using var bw = new BinaryWriter(File.Open(outputPath, FileMode.Create));

		string? line;
		while ((line = sr.ReadLine()) != null)
		{
			var parts = line.Split(',');
			if (parts.Length != 4)
				continue;

			int index = int.Parse(parts[0]);
			byte s1 = byte.Parse(parts[1]);
			byte s127 = byte.Parse(parts[2]);
			byte s255 = byte.Parse(parts[3]);
			uint _24bit = ((uint)s1 << 16) | ((uint)s127 << 8) | s255;

			bw.Write(_24bit);
			bw.Write(index);
			bw.Write(s1);
			bw.Write(s127);
			bw.Write(s255);
			bw.Write((byte)0); // padding for 8‑byte alignment
		}
	}
	public static void newSortBinary(string inputBinaryPath)
	{
		var list = new List<RotorRecord>();

		using var br = new BinaryReader(File.OpenRead(inputBinaryPath));
		while (br.BaseStream.Position < br.BaseStream.Length)
		{
			RotorRecord r;
			r.Key24 = br.ReadUInt32();
			r.Index = br.ReadInt32();
			r.S1 = br.ReadByte();
			r.S127 = br.ReadByte();
			r.S255 = br.ReadByte();
			r.Pad = br.ReadByte();
			list.Add(r);
		}
		br.Dispose();
		list.Sort((a, b) => a.Key24.CompareTo(b.Key24));

		File.WriteAllBytes(inputBinaryPath, list.SelectMany(n =>
		{
			var bytes = new List<byte>();
			bytes.AddRange(BitConverter.GetBytes(n.Key24));
			bytes.AddRange(BitConverter.GetBytes(n.Index));
			bytes.Add(n.S1);
			bytes.Add(n.S127);
			bytes.Add(n.S255);
			bytes.Add(n.Pad);
			return bytes;
		}).ToArray());
	}
}
