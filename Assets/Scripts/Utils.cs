using System;
using System.IO;
using System.Runtime.InteropServices;

public class Utils
{
    public static void WriteArrayToFile(Player.HistoryEntry[] dataArray, string filePath)
    {
        var byteSpan = MemoryMarshal.AsBytes(dataArray.AsSpan());
        File.WriteAllBytes(filePath, byteSpan.ToArray());
    }

    public static Player.HistoryEntry[] ReadArrayFromFile(string filePath)
    {
        byte[] fileBytes = File.ReadAllBytes(filePath);
        return MemoryMarshal.Cast<byte, Player.HistoryEntry>(fileBytes).ToArray();
    }
        
}