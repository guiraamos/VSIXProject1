using System;
using System.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CodeAnalysisApp
{
    public class AnalisadorAST
    {
        private static readonly string NameSpacetextService = "Service";
        private static readonly string[] DependenciasService = { "System.Collections.Generic", "MicroServiceNet", "RestSharp" };

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
                    if (item is BlockSyntax)
                    {
                        NavegaEntreNodosDoMetodo(item.ChildNodes());
                    }
                }
            }

        }

        private static void NavegaEntreNodosDoMetodo(IEnumerable<SyntaxNode> nodes)
        {
            foreach (var nodo in nodes)
            {
                if (nodo is VariableDeclarationSyntax)
                {
                    IdentifierNameSyntax id = (IdentifierNameSyntax)nodo.ChildNodes().FirstOrDefault(n => n is IdentifierNameSyntax);
                    VariableDeclaratorSyntax declaracao = (VariableDeclaratorSyntax)nodo.ChildNodes().FirstOrDefault(n => n is VariableDeclaratorSyntax);

                    if (declaracao.Initializer.Value.ToString().Contains("PostAsync"))
                    {
                        MontaNovaClassePost(nodo);
                    }

                    if (declaracao.Initializer.Value.ToString().Contains("GetAsync"))
                    {
                        MontaNovaClasseGet(nodo);
                    }
                }
                NavegaEntreNodosDoMetodo(nodo.ChildNodes());
            }
        }

        private static void MontaNovaClasseGet(SyntaxNode nodo)
        {
            throw new System.NotImplementedException();
        }

        private static void MontaNovaClassePost(SyntaxNode nodo)
        {
            throw new System.NotImplementedException();
        }


        static void CreateClassService(string nameClass, string hostMicroServico, string nameOfmethod)
        {
            //Cria um namespace da classe (namespace CodeGenerationSample)
            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(NameSpacetextService)).NormalizeWhitespace();

            // Add as dependencias da classe
            foreach (string dependence in DependenciasService)
            {
                @namespace = @namespace.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(dependence)));
            }

            //  Cria a classe
            var classDeclaration = SyntaxFactory.ClassDeclaration(nameClass);

            // Torna a classe pública
            classDeclaration = classDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            // Adiciona a herânca MicroServiceBase a classe
            classDeclaration = classDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("MicroServiceBase")));


            // Add a tag MicroServiceHost com o valor do HOST encontrado
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroServiceHost"), SyntaxFactory.ParseAttributeArgumentList(hostMicroServico));
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attribute));
            classDeclaration = classDeclaration.AddAttributeLists(attributeList);



            // Create a method
            var methodDeclaration = SyntaxFactory
                .MethodDeclaration(SyntaxFactory.ParseTypeName("IRestResponse"), nameOfmethod)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("parameters"))
                        .WithType(SyntaxFactory.ParseTypeName(typeof(List<KeyValuePair<object, object>>).FullName))
                        .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))));


            // Add the field, the property and method to the class.
            classDeclaration = classDeclaration.AddMembers(methodDeclaration);

            // Add the class to the namespace.
            @namespace = @namespace.AddMembers(classDeclaration);










            // Normalize and get code as string.
            var code = @namespace
                .NormalizeWhitespace()
                .ToFullString();

            // Output new code to the console.
            Console.WriteLine(code);
        }
    }
}
