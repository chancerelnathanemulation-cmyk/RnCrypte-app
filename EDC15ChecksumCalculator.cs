using System;
using System.Collections.Generic;

namespace RNPerf
{
    public class EDC15ChecksumCalculator
    {
        public struct ChecksumBlock
        {
            public uint StartAddress;
            public uint EndAddress;
            public uint ChecksumAddress;
        }

        public enum EDC15Version
        {
            TDI41_V1,
            TDI41_V2,
            TDI2002,
            UNKNOWN
        }

        private EDC15Version detectedVersion = EDC15Version.UNKNOWN;

        // ═══════════════════════════════════════════════
        // BLOCS CHECKSUMS V4.1 (12 blocs)
        // ═══════════════════════════════════════════════
        private static readonly ChecksumBlock[] BLOCKS_V41 = new ChecksumBlock[]
        {
            new ChecksumBlock { StartAddress = 0x10000, EndAddress = 0x14000, ChecksumAddress = 0x13FFC },
            new ChecksumBlock { StartAddress = 0x14000, EndAddress = 0x4C000, ChecksumAddress = 0x4BFFC },
            new ChecksumBlock { StartAddress = 0x4C000, EndAddress = 0x50000, ChecksumAddress = 0x4FFFC },
            new ChecksumBlock { StartAddress = 0x50000, EndAddress = 0x50B80, ChecksumAddress = 0x50B7C },
            new ChecksumBlock { StartAddress = 0x50B80, EndAddress = 0x5C000, ChecksumAddress = 0x5BFFC },
            new ChecksumBlock { StartAddress = 0x5C000, EndAddress = 0x60000, ChecksumAddress = 0x5FFFC },
            new ChecksumBlock { StartAddress = 0x60000, EndAddress = 0x60B80, ChecksumAddress = 0x60B7C },
            new ChecksumBlock { StartAddress = 0x60B80, EndAddress = 0x6C000, ChecksumAddress = 0x6BFFC },
            new ChecksumBlock { StartAddress = 0x6C000, EndAddress = 0x70000, ChecksumAddress = 0x6FFFC },
            new ChecksumBlock { StartAddress = 0x70000, EndAddress = 0x70B80, ChecksumAddress = 0x70B7C },
            new ChecksumBlock { StartAddress = 0x70B80, EndAddress = 0x7C000, ChecksumAddress = 0x7BFFC },
        };

        // ═══════════════════════════════════════════════
        // BLOCS CHECKSUMS V4.1 v2 (8 blocs)
        // ═══════════════════════════════════════════════
        private static readonly ChecksumBlock[] BLOCKS_V41_V2 = new ChecksumBlock[]
        {
            new ChecksumBlock { StartAddress = 0x10000, EndAddress = 0x14000, ChecksumAddress = 0x13FFC },
            new ChecksumBlock { StartAddress = 0x14000, EndAddress = 0x58000, ChecksumAddress = 0x57FFC },
            new ChecksumBlock { StartAddress = 0x58000, EndAddress = 0x58B80, ChecksumAddress = 0x58B7C },
            new ChecksumBlock { StartAddress = 0x58B80, EndAddress = 0x64000, ChecksumAddress = 0x63FFC },
            new ChecksumBlock { StartAddress = 0x64000, EndAddress = 0x70000, ChecksumAddress = 0x6FFFC },
            new ChecksumBlock { StartAddress = 0x70000, EndAddress = 0x70B80, ChecksumAddress = 0x70B7C },
            new ChecksumBlock { StartAddress = 0x70B80, EndAddress = 0x7C000, ChecksumAddress = 0x7BFFC },
        };

        private const ushort SEED_A_MAGIC = 0x8631;
        private const ushort SEED_B_MAGIC = 0xEFCD;
        private const ushort MAGIC_CONST_1 = 0xDAAD;
        private const ushort MAGIC_CONST_2 = 0x79CF;
        private const ushort MAGIC_CONST_3 = 0x1033;

        // ═══════════════════════════════════════════════
        // MAIN METHOD
        // ═══════════════════════════════════════════════
        public bool FixAllChecksums(byte[] fileBuffer, out string report)
        {
            report = "";

            if (fileBuffer == null || fileBuffer.Length < 0x7C000)
            {
                report = "❌ Fichier trop petit (min 0x7C000 bytes)";
                return false;
            }

            try
            {
                DetectVersion(fileBuffer);
                report += $"📍 Version détectée : {detectedVersion}\n\n";

                ChecksumBlock[] blocks = GetBlocksForVersion();
                int fixedCount = 0;

                foreach (var block in blocks)
                {
                    bool wasFixed = FixChecksumBlock(fileBuffer, block, out string blockReport);
                    report += blockReport;
                    if (wasFixed) fixedCount++;
                }

                report += $"\n✅ {fixedCount} bloc(s) corrigé(s)";
                return true;
            }
            catch (Exception ex)
            {
                report = $"❌ Erreur : {ex.Message}";
                return false;
            }
        }

        private bool FixChecksumBlock(byte[] buffer, ChecksumBlock block, out string report)
        {
            report = "";
            uint oldChecksum = ReadUInt32LE(buffer, block.ChecksumAddress);
            uint newChecksum = CalculateChecksum(buffer, block.StartAddress, block.EndAddress - 4);

            WriteUInt32LE(buffer, block.ChecksumAddress, newChecksum);

            bool wasFixed = (oldChecksum != newChecksum);

            report = $"📦 Bloc 0x{block.StartAddress:X5}-0x{block.EndAddress:X5}\n";
            report += $"   Old: 0x{oldChecksum:X8} → New: 0x{newChecksum:X8}\n";
            report += wasFixed ? "   ✅ CORRIGÉ\n\n" : "   ✓ OK\n\n";

            return wasFixed;
        }

        // ═══════════════════════════════════════════════
        // ALGO CHECKSUM (Bosch EDC15)
        // ═══════════════════════════════════════════════
        private uint CalculateChecksum(byte[] buffer, uint startAddr, uint endAddr)
        {
            ushort seed_a = 0, seed_b = 0;
            uint count = startAddr / 2;
            uint endCount = endAddr / 2;
            uint bufferAddr = startAddr;

            if (count == endCount)
                return ((uint)seed_a | ((uint)seed_b << 16));

            seed_a = 0;
            seed_b = 0;

            if (startAddr == 0x8000)
            {
                seed_a = (ushort)(seed_a ^ 0xD565);
                seed_b = (ushort)(seed_b + 0x308a);
            }

            while (count < endCount)
            {
                ushort word1 = ReadUInt16LE(buffer, bufferAddr);
                seed_a ^= word1;

                ushort var_3 = (ushort)(seed_b & 0xF);
                count++;
                bufferAddr += 2;

                // Rotation seed_a
                if ((seed_b & 0xF) > 0)
                {
                    ushort var_4 = 0;
                    for (int i = 0; i < var_3; i++)
                    {
                        var_4 = (ushort)((seed_a >> 15) & 1);
                        seed_a = (ushort)((seed_a << 1) | var_4);
                    }
                }

                ushort word2 = ReadUInt16LE(buffer, bufferAddr);
                seed_b -= word2;
                seed_b = (ushort)(seed_a ^ seed_b);

                bufferAddr += 2;
                count++;

                if (count > endCount)
                    break;

                ushort word3 = ReadUInt16LE(buffer, bufferAddr);
                bufferAddr += 4;
                seed_a += (ushort)((0xFFFF - word3 + MAGIC_CONST_1));

                ushort word4 = ReadUInt16LE(buffer, bufferAddr - 2);
                ushort word5 = ReadUInt16LE(buffer, bufferAddr - 4);
                seed_b ^= (ushort)(word4 + word5);

                ushort var_4_b = (ushort)(seed_a & 0xF);
                count += 2;

                if ((seed_a & 0xF) > 0)
                {
                    for (int i = 0; i < var_4_b; i++)
                    {
                        uint temp = (uint)((seed_b >> 1) | ((seed_b & 1) << 15));
                        seed_b = (ushort)temp;
                    }
                }
            }

            if (startAddr == 0)
            {
                seed_a -= MAGIC_CONST_2;
                seed_b -= MAGIC_CONST_3;
            }

            uint checksum = ((uint)seed_a | ((uint)seed_b << 16));
            return checksum;
        }

        // ═══════════════════════════════════════════════
        // UTILS
        // ═══════════════════════════════════════════════
        private ushort ReadUInt16LE(byte[] buffer, uint address)
        {
            return (ushort)(buffer[address] | (buffer[address + 1] << 8));
        }

        private uint ReadUInt32LE(byte[] buffer, uint address)
        {
            return (uint)(buffer[address]
                       | (buffer[address + 1] << 8)
                       | (buffer[address + 2] << 16)
                       | (buffer[address + 3] << 24));
        }

        private void WriteUInt16LE(byte[] buffer, uint address, ushort value)
        {
            buffer[address] = (byte)(value & 0xFF);
            buffer[address + 1] = (byte)((value >> 8) & 0xFF);
        }

        private void WriteUInt32LE(byte[] buffer, uint address, uint value)
        {
            buffer[address] = (byte)(value & 0xFF);
            buffer[address + 1] = (byte)((value >> 8) & 0xFF);
            buffer[address + 2] = (byte)((value >> 16) & 0xFF);
            buffer[address + 3] = (byte)((value >> 24) & 0xFF);
        }

        private void DetectVersion(byte[] buffer)
        {
            uint val1 = ReadUInt32LE(buffer, 0x13FFC);
            uint val2 = ReadUInt32LE(buffer, 0x4BFFC);

            detectedVersion = (val1 != 0 && val2 != 0) ? EDC15Version.TDI41_V1 : EDC15Version.TDI41_V2;
        }

        private ChecksumBlock[] GetBlocksForVersion()
        {
            return detectedVersion == EDC15Version.TDI41_V1 ? BLOCKS_V41 : BLOCKS_V41_V2;
        }
    }
}
