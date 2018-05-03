namespace VSIXProject1
{
    public class Arquivo
    {
        public static void LerArquivo(string path)
        {
            string[] lines = System.IO.File.ReadAllLines(path);

            foreach (string line in lines)
            {
                
            }

        }
    }
}
