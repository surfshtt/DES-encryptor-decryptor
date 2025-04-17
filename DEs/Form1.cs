using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DEs
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Random rnd = new Random();
            byte[] keyBytes = new byte[8];
            rnd.NextBytes(keyBytes);
            richTextBox1.Text = BitConverter.ToString(keyBytes).Replace("-", "");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                richTextBox2.Text = File.ReadAllText(openFileDialog.FileName);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string key = richTextBox1.Text;
            string plaintext = richTextBox2.Text;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(plaintext))
            {
                MessageBox.Show("Введите все значения в ячейки.",
                                "Пусто", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            while (plaintext.Length % 8 != 0)
                plaintext += " ";

            string encryptedText = DESEncrypt(plaintext, key);
            richTextBox3.Text = encryptedText;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string key = richTextBox1.Text;
            string encryptedText = richTextBox3.Text;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(encryptedText))
            {
                MessageBox.Show("Введите все значения в ячейки.",
                                "Пусто", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string decryptedText = DESDecrypt(encryptedText, key);
            richTextBox4.Text = decryptedText;
        }

        private string DESEncrypt(string plaintext, string key)
        {
            byte[] keyBytes = key.Select(c => (byte)c).ToArray();
            if (keyBytes.Length < 8)
            {
                MessageBox.Show("Ключ должен содержать не менее 8 символов (8 байт). Пожалуйста, заполните корректно ключ.",
                                "Ошибка ключа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }

            StringBuilder steps = new StringBuilder();
            byte[] textBytes = plaintext.Select(c => (byte)c).ToArray();
            steps.AppendLine("Исходные данные: " + plaintext);

            byte[] encryptedBytes = DESAlgorithm(textBytes, keyBytes, true, steps);
            richTextBox5.Text = steps.ToString();
            return BitConverter.ToString(encryptedBytes).Replace("-", "");
        }

        private string DESDecrypt(string encryptedText, string key)
        {
            byte[] keyBytes = key.Select(c => (byte)c).ToArray();
            if (keyBytes.Length < 8)
            {
                MessageBox.Show("Ключ должен содержать не менее 8 символов (8 байт). Пожалуйста, заполните корректно ключ.",
                                "Ошибка ключа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "";
            }

            StringBuilder steps = new StringBuilder();
            byte[] encryptedBytes = Enumerable.Range(0, encryptedText.Length / 2)
                                              .Select(x => Convert.ToByte(encryptedText.Substring(x * 2, 2), 16))
                                              .ToArray();
            steps.AppendLine("Зашифрованные данные: " + encryptedText);

            byte[] decryptedBytes = DESAlgorithm(encryptedBytes, keyBytes, false, steps);
            richTextBox5.Text = steps.ToString();
            return new string(decryptedBytes.Select(b => (char)b).ToArray());
        }

        private byte[] DESAlgorithm(byte[] data, byte[] key, bool encrypt, StringBuilder steps)
        {
            byte[] ip = InitialPermutation(data);
            steps.AppendLine("После начальной перестановки (IP): " + ByteArrayToHex(ip));

            byte[] L = new byte[4];
            byte[] R = new byte[4];
            Array.Copy(ip, 0, L, 0, 4);
            Array.Copy(ip, 4, R, 0, 4);
            steps.AppendLine("Начальное L: " + ByteArrayToHex(L));
            steps.AppendLine("Начальное R: " + ByteArrayToHex(R));

            byte[][] subKeys = GenerateSubKeys(key);
            if (!encrypt)
                Array.Reverse(subKeys);

            for (int round = 0; round < 16; round++)
            {
                steps.AppendLine($"----- Раунд {round + 1} -----");
                steps.AppendLine("L: " + ByteArrayToHex(L));
                steps.AppendLine("R: " + ByteArrayToHex(R));
                steps.AppendLine("Подключ: " + ByteArrayToHex(subKeys[round]));

                byte[] fResult = F(R, subKeys[round]);
                steps.AppendLine("F(R, Подключ): " + ByteArrayToHex(fResult));

                byte[] newR = XOR(L, fResult);
                steps.AppendLine("Новая R (L XOR F): " + ByteArrayToHex(newR));

                L = R;
                R = newR;
                steps.AppendLine();
            }

            byte[] preOutput = new byte[8];
            Array.Copy(R, 0, preOutput, 0, 4);
            Array.Copy(L, 0, preOutput, 4, 4);
            steps.AppendLine("До финальной перестановки: " + ByteArrayToHex(preOutput));

            byte[] fp = FinalPermutation(preOutput);
            steps.AppendLine("После финальной перестановки (FP): " + ByteArrayToHex(fp));
            return fp;
        }

        private string ByteArrayToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "");
        }

        private byte[] InitialPermutation(byte[] data)
        {
            return Permute(data, IP, 8);
        }

        private byte[] FinalPermutation(byte[] data)
        {
            return Permute(data, FP, 8);
        }

        private byte[] Permute(byte[] data, int[] table, int outBytes)
        {
            byte[] output = new byte[outBytes];
            for (int i = 0; i < table.Length; i++)
            {
                int pos = table[i] - 1;
                int bytePos = pos / 8;
                int bitPos = pos % 8;
                int value = (data[bytePos] >> (7 - bitPos)) & 1;
                output[i / 8] |= (byte)(value << (7 - (i % 8)));
            }
            return output;
        }

        private byte[] XOR(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length];
            for (int i = 0; i < a.Length; i++)
                result[i] = (byte)(a[i] ^ b[i]);
            return result;
        }

        private byte[] F(byte[] R, byte[] subKey)
        {
            byte[] expandedR = Permute(R, E, 6);

            byte[] xorResult = XOR(expandedR, subKey);

            int[] xorBits = ByteArrayToBitArray(xorResult, 48);
            int[] sboxOutputBits = new int[32];
            for (int i = 0; i < 8; i++)
            {
                int offset = i * 6;
                int b = 0;
                for (int j = 0; j < 6; j++)
                {
                    b = (b << 1) | xorBits[offset + j];
                }

                int firstBit = (b & 0x20) >> 5;
                int sixthBit = b & 0x01;
                int row = firstBit | sixthBit;

                int col = (b >> 1) & 0x0F;
                int sboxVal = SBox[i, row * 16 + col];

                for (int j = 0; j < 4; j++)
                {
                    sboxOutputBits[i * 4 + j] = (sboxVal >> (3 - j)) & 1;
                }
            }
            byte[] sboxBytes = BitArrayToByteArray(sboxOutputBits);

            byte[] fResult = Permute(sboxBytes, P, 4);
            return fResult;
        }

        private byte[][] GenerateSubKeys(byte[] key)
        {
            byte[][] subKeys = new byte[16][];
            int[] keyBits = ByteArrayToBitArray(key, 64);
            int[] permutedKey = new int[56];
            for (int i = 0; i < keyPerm.Length; i++)
                permutedKey[i] = keyBits[keyPerm[i] - 1];

            int[] C = new int[28];
            int[] D = new int[28];
            Array.Copy(permutedKey, 0, C, 0, 28);
            Array.Copy(permutedKey, 28, D, 0, 28);

            for (int round = 0; round < 16; round++)
            {
                C = LeftShift(C, shifts[round]);
                D = LeftShift(D, shifts[round]);
                int[] CD = new int[56];
                Array.Copy(C, 0, CD, 0, 28);
                Array.Copy(D, 0, CD, 28, 28);
                int[] subKeyBits = new int[48];
                for (int i = 0; i < keyComp.Length; i++)
                    subKeyBits[i] = CD[keyComp[i] - 1];
                subKeys[round] = BitArrayToByteArray(subKeyBits);
            }
            return subKeys;
        }

        private int[] LeftShift(int[] bits, int shift)
        {
            int[] shifted = new int[bits.Length];
            for (int i = 0; i < bits.Length; i++)
                shifted[i] = bits[(i + shift) % bits.Length];
            return shifted;
        }

        private int[] ByteArrayToBitArray(byte[] data, int bitsCount)
        {
            int[] bits = new int[bitsCount];
            for (int i = 0; i < bitsCount; i++)
            {
                int byteIndex = i / 8;
                int bitIndex = 7 - (i % 8);
                bits[i] = (data[byteIndex] >> bitIndex) & 1;
            }
            return bits;
        }

        private byte[] BitArrayToByteArray(int[] bits)
        {
            int byteCount = bits.Length / 8;
            byte[] data = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                for (int j = 0; j < 8; j++)
                    data[i] |= (byte)(bits[i * 8 + j] << (7 - j));
            }
            return data;
        }


        private static readonly int[] IP = {
            58, 50, 42, 34, 26, 18, 10, 2,
            60, 52, 44, 36, 28, 20, 12, 4,
            62, 54, 46, 38, 30, 22, 14, 6,
            64, 56, 48, 40, 32, 24, 16, 8,
            57, 49, 41, 33, 25, 17, 9, 1,
            59, 51, 43, 35, 27, 19, 11, 3,
            61, 53, 45, 37, 29, 21, 13, 5,
            63, 55, 47, 39, 31, 23, 15, 7
        };

        private static readonly int[] FP = {
            40, 8, 48, 16, 56, 24, 64, 32,
            39, 7, 47, 15, 55, 23, 63, 31,
            38, 6, 46, 14, 54, 22, 62, 30,
            37, 5, 45, 13, 53, 21, 61, 29,
            36, 4, 44, 12, 52, 20, 60, 28,
            35, 3, 43, 11, 51, 19, 59, 27,
            34, 2, 42, 10, 50, 18, 58, 26,
            33, 1, 41, 9, 49, 17, 57, 25
        };

        private static readonly int[] keyPerm = {
            57, 49, 41, 33, 25, 17, 9,
            1, 58, 50, 42, 34, 26, 18,
            10, 2, 59, 51, 43, 35, 27,
            19, 11, 3, 60, 52, 44, 36,
            63, 55, 47, 39, 31, 23, 15,
            7, 62, 54, 46, 38, 30, 22,
            14, 6, 61, 53, 45, 37, 29,
            21, 13, 5, 28, 20, 12, 4
        };

        private static readonly int[] keyComp = {
            14, 17, 11, 24, 1, 5,
            3, 28, 15, 6, 21, 10,
            23, 19, 12, 4, 26, 8,
            16, 7, 27, 20, 13, 2,
            41, 52, 31, 37, 47, 55,
            30, 40, 51, 45, 33, 48,
            44, 49, 39, 56, 34, 53,
            46, 42, 50, 36, 29, 32
        };

        private static readonly int[] shifts = {
            1, 1, 2, 2, 2, 2, 2, 2,
            1, 2, 2, 2, 2, 2, 2, 1
        };

        private static readonly int[,] SBox = {
        {
            14, 4, 13, 1, 2, 15, 11, 8, 3, 10, 6, 12, 5, 9, 0, 7,
            0, 15, 7, 4, 14, 2, 13, 1, 10, 6, 12, 11, 9, 5, 3, 8,
            4, 1, 14, 8, 13, 6, 2, 11, 15, 12, 9, 7, 3, 10, 5, 0,
            15, 12, 8, 2, 4, 9, 1, 7, 5, 11, 3, 14, 10, 0, 6, 13
        },
        {
            15, 1, 8, 14, 6, 11, 3, 4, 9, 7, 2, 13, 12, 0, 5, 10,
            3, 13, 4, 7, 15, 2, 8, 14, 12, 0, 1, 10, 6, 9, 11, 5,
            0, 14, 7, 11, 10, 4, 13, 1, 5, 8, 12, 6, 9, 3, 2, 15,
            13, 8, 10, 1, 3, 15, 4, 2, 11, 6, 7, 12, 0, 5, 14, 9
        },
        {
            10, 0, 9, 14, 6, 3, 15, 5, 1, 13, 12, 7, 11, 4, 2, 8,
            13, 7, 0, 9, 3, 4, 6, 10, 2, 8, 5, 14, 12, 11, 15, 1,
            13, 6, 4, 9, 8, 15, 3, 0, 11, 1, 2, 12, 5, 10, 14, 7,
            1, 10, 13, 0, 6, 9, 8, 7, 4, 15, 14, 3, 11, 5, 2, 12
        },
        {
            7, 13, 14, 3, 0, 6, 9, 10, 1, 2, 8, 5, 11, 12, 4, 15,
            13, 8, 11, 5, 6, 15, 0, 3, 4, 7, 2, 12, 1, 10, 14, 9,
            10, 6, 9, 0, 12, 11, 7, 13, 15, 1, 3, 14, 5, 2, 8, 4,
            3, 15, 0, 6, 10, 1, 13, 8, 9, 4, 5, 11, 12, 7, 2, 14
        },
        {
            2, 12, 4, 1, 7, 10, 11, 6, 8, 5, 3, 15, 13, 0, 14, 9,
            14, 11, 2, 12, 4, 7, 13, 1, 5, 0, 15, 10, 3, 9, 8, 6,
            4, 2, 1, 11, 10, 13, 7, 8, 15, 9, 12, 5, 6, 3, 0, 14,
            11, 8, 12, 7, 1, 14, 2, 13, 6, 15, 0, 9, 10, 4, 5, 3
        },
        {
            12, 1, 10, 15, 9, 2, 6, 8, 0, 13, 3, 4, 14, 7, 5, 11,
            10, 15, 4, 2, 7, 12, 9, 5, 6, 1, 13, 14, 0, 11, 3, 8,
            9, 14, 15, 5, 2, 8, 12, 3, 7, 0, 4, 10, 1, 13, 11, 6,
            4, 3, 2, 12, 9, 5, 15, 10, 11, 14, 1, 7, 6, 0, 8, 13
        },
        {
            4, 11, 2, 14, 15, 0, 8, 13, 3, 12, 9, 7, 5, 10, 6, 1,
            13, 0, 11, 7, 4, 9, 1, 10, 14, 3, 5, 12, 2, 15, 8, 6,
            1, 4, 11, 13, 12, 3, 7, 14, 10, 15, 6, 8, 0, 5, 9, 2,
            6, 11, 13, 8, 1, 4, 10, 7, 9, 5, 0, 15, 14, 2, 3, 12
        },
        {
            13, 2, 8, 4, 6, 15, 11, 1, 10, 9, 3, 14, 5, 0, 12, 7,
            1, 15, 13, 8, 10, 3, 7, 4, 12, 5, 6, 11, 0, 14, 9, 2,
            7, 11, 4, 1, 9, 12, 14, 2, 0, 6, 10, 13, 15, 3, 5, 8,
            2, 1, 14, 7, 4, 10, 8, 13, 15, 12, 9, 0, 3, 5, 6, 11
        }
        };

        private static readonly int[] E = {
            32, 1, 2, 3, 4, 5,
            4, 5, 6, 7, 8, 9,
            8, 9, 10, 11, 12, 13,
            12, 13, 14, 15, 16, 17,
            16, 17, 18, 19, 20, 21,
            20, 21, 22, 23, 24, 25,
            24, 25, 26, 27, 28, 29,
            28, 29, 30, 31, 32, 1
        };

        private static readonly int[] P = {
            16, 7, 20, 21,
            29, 12, 28, 17,
            1, 15, 23, 26,
            5, 18, 31, 10,
            2, 8, 24, 14,
            32, 27, 3, 9,
            19, 13, 30, 6,
            22, 11, 4, 25
        };

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
