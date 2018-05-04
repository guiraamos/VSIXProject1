namespace VSIXProject1
{
    public class Arquivo
    {
        public static string LerClasse(string path)
        {
            return System.IO.File.ReadAllText(path);
        }
    }
}
