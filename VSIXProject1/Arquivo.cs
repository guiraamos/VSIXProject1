using System;
using System.IO;
using System.Text;
using Analyzer;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace VSIXProject1
{
    public class Arquivo
    {
        public static string LerClasse(string path)
        {
            return File.ReadAllText(path);
        }

        public static void CriarArquivo(string newClass, EnvDTE.Project selectedProject, Microsoft.Build.Evaluation.Project projectEvalution, string nameFile)
        {
            string filePath = Path.Combine(selectedProject.Properties.Item("FullPath").Value.ToString(), "Service", nameFile);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                using (FileStream fs = File.Create(filePath))
                {
                    Byte[] title = new UTF8Encoding(true).GetBytes(newClass);
                    fs.Write(title, 0, title.Length);
                }

                
                TFSAction(workspace => workspace.PendDelete(filePath), filePath);
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.ToString());
            }
        }
        public static void CheckoutFile(string filePath)
        {
            TFSAction((workspace) => workspace.PendEdit(filePath), filePath);
        }
        private static void TFSAction(Action<Workspace> action, string filePath)
        {
            var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(filePath);
            if (workspaceInfo == null)
            {
                Console.WriteLine("Failed to initialize workspace info");
                return;
            }
            using (var server = new TfsTeamProjectCollection(workspaceInfo.ServerUri))
            {
                var workspace = workspaceInfo.GetWorkspace(server);
                action(workspace);
            }
        }

    }
}
