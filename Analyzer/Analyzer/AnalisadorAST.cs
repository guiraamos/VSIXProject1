using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CodeAnalysisApp
{
    public class AnalisadorAST
    {
        public static void Analisar(string classeText)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(classeText);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var helloWorldDeclaration = (NamespaceDeclarationSyntax)root.Members[0];
            var classe = (ClassDeclarationSyntax)helloWorldDeclaration.Members[0];

            foreach (MethodDeclarationSyntax method in classe.Members)
            {
                foreach (var item in method.ChildNodes())
                {
                    if(item.GetType().Name == "BlockSyntax")
                    {
                        foreach (var variaveis in item.ChildNodes())
                        {
                            if(variaveis.GetType().Equals(typeof(LocalDeclarationStatementSyntax)))
                            {

                            }
                        }
                    }
                }
            }

        }
    }
}
