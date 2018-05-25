using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using CodeAnalysisApp;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Build.Evaluation;
using System.Text;
using Analyzer;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.Client;
using System.Reflection;

namespace VSIXProject1
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class Command
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("8a5231d6-3331-4277-8773-dc797fd01302");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private Command(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static Command Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new Command(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            string message = "Refactoy concluido com sucesso !";
            string title = "Refactory - Microserviço";

            IntPtr hierarchyPointer, selectionContainerPointer;
            Object selectedObject = null;
            IVsMultiItemSelect multiItemSelect;
            uint projectItemId;

            IVsMonitorSelection monitorSelection =
                    (IVsMonitorSelection)Package.GetGlobalService(
                    typeof(SVsShellMonitorSelection));

            monitorSelection.GetCurrentSelection(out hierarchyPointer,
                                                 out projectItemId,
                                                 out multiItemSelect,
                                                 out selectionContainerPointer);

            IVsHierarchy selectedHierarchy = Marshal.GetTypedObjectForIUnknown(
                                                 hierarchyPointer,
                                                 typeof(IVsHierarchy)) as IVsHierarchy;

            if (selectedHierarchy != null)
            {
                ErrorHandler.ThrowOnFailure(selectedHierarchy.GetProperty(
                                                  projectItemId,
                                                  (int)__VSHPROPID.VSHPROPID_ExtObject,
                                                  out selectedObject));
            }

            EnvDTE.Project selectedProject = selectedObject as EnvDTE.Project;

            string projectPath = selectedProject.FullName;

            var classesEncontradas = GetAllProjectFiles(selectedProject.ProjectItems, ".cs");

            var projectEvalution = new Microsoft.Build.Evaluation.Project(projectPath);

            if (!Directory.Exists(Path.Combine(selectedProject.Properties.Item("FullPath").Value.ToString(), "Service")))
            {
                selectedProject.ProjectItems.AddFolder("Service");

                projectEvalution.AddItem("Folder", 
                    Path.Combine(selectedProject.Properties.Item("FullPath").Value.ToString(), "Service"));
            }


            foreach (string classe in classesEncontradas)
            {
                var newClass = AnalisadorAST.Analisar(Arquivo.LerClasse(classe));

                if (newClass != null)
                {
                    Arquivo.CriarArquivo(newClass.Interface, selectedProject, projectEvalution, "I" + newClass.Name + ".cs");
                    Arquivo.CriarArquivo(newClass.Classe, selectedProject, projectEvalution, newClass.Name + ".cs");
                }
            }


            //Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                this.ServiceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        

        public static List<string> GetAllProjectFiles(ProjectItems projectItems, string extension)
        {
            List<string> returnValue = new List<string>();

            foreach (EnvDTE.ProjectItem projectItem in projectItems)
            {
                if (projectItem.GetType().Name == "OAFolderItem")
                {
                    returnValue.AddRange(GetAllProjectFiles(projectItem.ProjectItems, extension));
                }
                else
                {
                    string fileName = projectItem.Name;

                    if (Path.GetExtension(fileName).ToLower() == extension)
                        returnValue.Add(projectItem.Properties.Item("FullPath").Value.ToString());
                }
            }

            return returnValue;
        }
    }
}
