using System;
using System.IO;
using System.Text;

namespace VSIXProject1
{
    public class Arquivo
    {
        public static string LerClasse(string path)
        {
            return File.ReadAllText(path);
        }

        private void CriaArquivo(string text, string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                // Create the file.
                using (FileStream fs = File.Create(path))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes(text);
                    fs.Write(info, 0, info.Length);
                }

                using (StreamReader sr = File.OpenText(path))
                {
                    string s = "";
                    while ((s = sr.ReadLine()) != null)
                    {
                        Console.WriteLine(s);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
