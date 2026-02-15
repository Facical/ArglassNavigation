using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ARNavExperiment.Utils
{
    public class CSVWriter : IDisposable
    {
        private StreamWriter writer;
        private readonly object lockObj = new object();
        private bool disposed;

        public string FilePath { get; private set; }

        public CSVWriter(string filePath, string[] headers)
        {
            FilePath = filePath;
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine(string.Join(",", headers));
            writer.Flush();
        }

        public void WriteRow(params string[] values)
        {
            lock (lockObj)
            {
                if (disposed) return;
                var sb = new StringBuilder();
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(EscapeCSV(values[i]));
                }
                writer.WriteLine(sb.ToString());
                writer.Flush();
            }
        }

        private string EscapeCSV(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        public void Dispose()
        {
            lock (lockObj)
            {
                if (!disposed)
                {
                    disposed = true;
                    writer?.Close();
                    writer = null;
                }
            }
        }
    }
}
