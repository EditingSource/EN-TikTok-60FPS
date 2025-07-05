//
// C# MP4 Patcher - A utility for patching timescale and duration in MP4 atoms.
// 2025 Editing News, Evilfy.
// Distributed under the terms of the GNU General Public License v3.0.
// See https://www.gnu.org/licenses/gpl-3.0.html for details.
//

using System;
using System.Buffers.Binary;
using System.IO;

namespace Mp4Tools
{
    /// <summary>
    /// Provides static methods to patch timescale and duration values in MP4 container atoms (like 'mvhd' and 'mdhd').
    /// </summary>
    public static class VideoPatcher
    {
        /// <summary>
        /// Patches an MP4 file by modifying the timescale and duration in 'mvhd' and 'mdhd' atoms.
        /// </summary>
        /// <param name="inputFilename">The path to the source MP4 file.</param>
        /// <param name="outputFilename">The path where the patched MP4 file will be saved.</param>
        /// <param name="scaleFactor">An optional factor to scale the duration. If null, a default factor targeting 30000 timescale is used.</param>
        /// <param name="logAction">An optional action to receive log messages during the process.</param>
        /// <returns>True if the file was patched and saved successfully; otherwise, false.</returns>
        public static bool PatchMp4(string inputFilename, string outputFilename, double? scaleFactor = null, Action<string>? logAction = null)
        {
            logAction?.Invoke($"Starting patch process for: {Path.GetFileName(inputFilename)}");

            byte[] data;
            try
            {
                data = File.ReadAllBytes(inputFilename);
                logAction?.Invoke($"Successfully read {data.Length} bytes from the input file.");
            }
            catch (IOException ex)
            {
                logAction?.Invoke($"[ERROR] Failed to read input file '{inputFilename}': {ex.Message}");
                return false;
            }

            // In C#, byte[] is mutable, so modifications are made in-place.
            int patchedMvhd = PatchAtom("mvhd", data, scaleFactor, logAction);
            int patchedMdhd = PatchAtom("mdhd", data, scaleFactor, logAction);

            int totalPatched = patchedMvhd + patchedMdhd;
            logAction?.Invoke($"Patching complete. Total atoms patched: {totalPatched}.");

            if (totalPatched == 0)
            {
                logAction?.Invoke("[WARNING] No 'mvhd' or 'mdhd' atoms were found or patched. The output file might be identical to the input.");
            }

            try
            {
                File.WriteAllBytes(outputFilename, data);
                logAction?.Invoke($"Patched file successfully written to: {outputFilename}");
                return true;
            }
            catch (IOException ex)
            {
                logAction?.Invoke($"[ERROR] Failed to write output file '{outputFilename}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds and patches a specific atom within the byte data of an MP4 file.
        /// </summary>
        /// <returns>The number of atoms that were successfully patched.</returns>
        private static int PatchAtom(string atomName, byte[] data, double? scaleFactor, Action<string>? logAction)
        {
            int patchCount = 0;
            int startIndex = 0;
            byte[] atomBytes = System.Text.Encoding.UTF8.GetBytes(atomName);

            while (true)
            {
                int foundIndex = IndexOfSequence(data, atomBytes, startIndex);
                if (foundIndex == -1)
                {
                    break; // No more occurrences of the atom name.
                }

                // The atom header (size and type) is located 4 bytes before the name.
                // atom header = 4 bytes size + 4 bytes type. We found the type.
                int headerOffset = foundIndex - 4;
                if (headerOffset < 0)
                {
                    startIndex = foundIndex + 4; // Move past this invalid finding.
                    continue;
                }

                // Read the atom's total size (a 4-byte big-endian integer).
                if (headerOffset + 4 > data.Length)
                {
                    startIndex = foundIndex + 4;
                    continue;
                }
                int boxSize = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(headerOffset, 4));

                // A valid atom must be at least 8 bytes (size + type).
                if (boxSize < 8)
                {
                    startIndex = foundIndex + 4;
                    continue;
                }

                // The version byte is at offset 8 from the start of the atom.
                if (headerOffset + 8 >= data.Length)
                {
                    startIndex = foundIndex + 4;
                    continue;
                }
                byte version = data[headerOffset + 8];

                if (version == 0) // 32-bit duration
                {
                    const int timescaleOffset32 = 20;
                    const int durationOffset32 = 24;

                    // Ensure the full atom data is within the file bounds.
                    if (headerOffset + durationOffset32 + 4 > data.Length)
                    {
                        startIndex = foundIndex + 4;
                        continue;
                    }

                    uint oldTimescale = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(headerOffset + timescaleOffset32, 4));
                    uint oldDuration = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(headerOffset + durationOffset32, 4));

                    double chosenScale = scaleFactor ?? (30000.0 / oldTimescale);
                    uint newTimescale = (uint)(oldTimescale * chosenScale);
                    uint newDuration = (uint)(oldDuration * chosenScale);

                    BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(headerOffset + timescaleOffset32, 4), newTimescale);
                    BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(headerOffset + durationOffset32, 4), newDuration);
                    
                    logAction?.Invoke($"  -> Patched '{atomName}' (v0) at offset 0x{headerOffset:X8}: timescale {oldTimescale}->{newTimescale}, duration {oldDuration}->{newDuration}");
                    patchCount++;
                }
                else if (version == 1) // 64-bit duration
                {
                    const int timescaleOffset64 = 28;
                    const int durationOffset64 = 32;

                    // Ensure the full atom data is within the file bounds.
                    if (headerOffset + durationOffset64 + 8 > data.Length)
                    {
                        startIndex = foundIndex + 4;
                        continue;
                    }

                    uint oldTimescale = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(headerOffset + timescaleOffset64, 4));
                    ulong oldDuration = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(headerOffset + durationOffset64, 8));

                    double chosenScale = scaleFactor ?? (30000.0 / oldTimescale);
                    uint newTimescale = (uint)(oldTimescale * chosenScale);
                    ulong newDuration = (ulong)(oldDuration * chosenScale);

                    BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(headerOffset + timescaleOffset64, 4), newTimescale);
                    BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(headerOffset + durationOffset64, 8), newDuration);
                    
                    logAction?.Invoke($"  -> Patched '{atomName}' (v1) at offset 0x{headerOffset:X8}: timescale {oldTimescale}->{newTimescale}, duration {oldDuration}->{newDuration}");
                    patchCount++;
                }
                else
                {
                    logAction?.Invoke($"  -> Found '{atomName}' at offset 0x{headerOffset:X8} with unknown version {version}; skipping.");
                }

                // Move search start past the current atom to find the next one.
                startIndex = foundIndex + 4;
            }
            return patchCount;
        }

        /// <summary>
        /// Finds the first occurrence of a byte sequence (pattern) within a byte array (buffer).
        /// </summary>
        /// <param name="buffer">The byte array to search within.</param>
        /// <param name="pattern">The byte sequence to find.</param>
        /// <param name="startIndex">The starting position for the search.</param>
        /// <returns>The zero-based index of the first occurrence of the pattern, or -1 if not found.</returns>
        private static int IndexOfSequence(byte[] buffer, byte[] pattern, int startIndex)
        {
            if (buffer == null || pattern == null || buffer.Length == 0 || pattern.Length == 0 || pattern.Length > buffer.Length - startIndex)
            {
                return -1;
            }

            for (int i = startIndex; i <= buffer.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
