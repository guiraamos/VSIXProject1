using System;
using System.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Analyzer;
using System.IO;
using System.Text;

namespace CodeAnalysisApp
{
    public class AnalisadorAST
    {
        private static readonly string NameSpacetextService = "Service";
        private static readonly string[] DependenciasService = { "System.Collections.Generic", "MicroServiceNet", "RestSharp" };

        public static string Analisar(string classeText)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(classeText);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var namespaceDeclaration = (NamespaceDeclarationSyntax)root.Members[0];
            var classe = (ClassDeclarationSyntax)namespaceDeclaration.Members[0];

            var pretendingClass = new PretendingClass() { Name = classe.Identifier.ValueText };

            foreach (var member in classe.Members)
            {
                if (member is MethodDeclarationSyntax)
                {
                    var method = (MethodDeclarationSyntax)member;
                    var pretendingMethod = new Method() { Name = method.Identifier.ValueText };

                    foreach (var item in method.ChildNodes())
                    {
                        if (item is BlockSyntax)
                        {
                            NavegaEntreNodosDoMetodo(item.ChildNodes(), pretendingClass, pretendingMethod);
                        }
                    }
                }
            }

            if (pretendingClass.Methods.Count > 0)
            {
                return CreateClassService(pretendingClass);
            }

            return null;
        }

        private static void NavegaEntreNodosDoMetodo(IEnumerable<SyntaxNode> nodes, PretendingClass pretendingClass, Method pretendingMethod)
        {
            foreach (var nodo in nodes)
            {
                if (nodo is VariableDeclarationSyntax)
                {
                    IdentifierNameSyntax id = (IdentifierNameSyntax)nodo.ChildNodes().FirstOrDefault(n => n is IdentifierNameSyntax);
                    VariableDeclaratorSyntax declaracao = (VariableDeclaratorSyntax)nodo.ChildNodes().FirstOrDefault(n => n is VariableDeclaratorSyntax);

                    List<string> tipoRequisicaoList = new List<string>() { "GetAsync", "PostAsync" };

                    foreach (var tipoRequisicao in tipoRequisicaoList)
                    {
                        if (declaracao.Initializer.Value.ToString().Contains(tipoRequisicao))
                        {
                            if (tipoRequisicao.Equals("GetAsync"))
                                pretendingMethod.RequestType = "GET";

                            if (tipoRequisicao.Equals("PostAsync"))
                                pretendingMethod.RequestType = "POST";

                            pretendingClass.Methods.Add(pretendingMethod);
                            TrataRotaMethodo(declaracao, pretendingClass, pretendingMethod);
                        }
                    }

                }

                NavegaEntreNodosDoMetodo(nodo.ChildNodes(), pretendingClass, pretendingMethod);
            }
        }

        private static void TrataRotaMethodo(SyntaxNode node, PretendingClass pretendingClass, Method pretendingMethod)
        {
            foreach (var item in node.ChildNodes())
            {
                if (item is ArgumentListSyntax)
                {
                    foreach (ArgumentSyntax argument in item.ChildNodes())
                    {
                        var value = argument.Expression.ToString();
                        if(value.Contains("http"))
                        {
                            var rota = value.Split('/');
                            var NameHostMicroService = "";
                            if (rota[rota.Length-1] == "\"")
                            {
                                pretendingMethod.MicroServiceRoute = rota[rota.Length-2];
                                NameHostMicroService = value.Replace(rota[rota.Length - 2]+ rota[rota.Length - 1], "");
                            }
                            else
                            {
                                pretendingMethod.MicroServiceRoute = rota[rota.Length-1];
                                NameHostMicroService = value.Replace(rota[rota.Length - 1], "");
                            }

                            pretendingClass.NameHostMicroService = NameHostMicroService.Replace("\"", "");
                        }
                    }
                }
                else
                {
                    TrataRotaMethodo(item, pretendingClass, pretendingMethod);
                }
            }
        }


        static string CreateClassService(PretendingClass pretendingClass)
        {
            //Cria um namespace da classe (namespace CodeGenerationSample)
            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(NameSpacetextService)).NormalizeWhitespace();

            // Add as dependencias da classe
            foreach (string dependence in DependenciasService)
            {
                @namespace = @namespace.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(dependence)));
            }

            //  Cria a classe
            var classDeclaration = SyntaxFactory.ClassDeclaration(pretendingClass.Name);

            // Torna a classe pública
            classDeclaration = classDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            // Adiciona a herânca MicroServiceBase a classe
            classDeclaration = classDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("MicroServiceBase")));


            // Add a tag MicroServiceHost com o valor do HOST encontrado
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroServiceHost"), SyntaxFactory.ParseAttributeArgumentList("(\"" + pretendingClass.NameHostMicroService + "\")"));
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attribute));
            classDeclaration = classDeclaration.AddAttributeLists(attributeList);


            // Create a method
            foreach (var method in pretendingClass.Methods)
            {
                var attributeMethod = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroService"), SyntaxFactory.ParseAttributeArgumentList("(\"" + method.MicroServiceRoute + "\")"));
                var attributeListMethod = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attributeMethod));

                var bodyMethod = SyntaxFactory.ParseStatement(String.Format("return Execute<{0}>({1}, Method.{2}, parameters);",pretendingClass.Name, method.Name, method.RequestType));

                var methodDeclaration = SyntaxFactory

                    .MethodDeclaration(SyntaxFactory.ParseTypeName("IRestResponse"), method.Name)
                    .AddAttributeLists(attributeListMethod)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("parameters"))
                            .WithType(SyntaxFactory.ParseTypeName("List<KeyValuePair<object, object>>"))
                            .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))))
                    .WithBody(SyntaxFactory.Block(bodyMethod));


                // Add the field, the property and method to the class.
                classDeclaration = classDeclaration.AddMembers(methodDeclaration);
            }

            // Add the class to the namespace.
            @namespace = @namespace.AddMembers(classDeclaration);

            // Normalize and get code as string.
            var code = @namespace
                .NormalizeWhitespace()
                .ToFullString();

            // Output new code to the console.
            return code;
        }


        
    }
}
