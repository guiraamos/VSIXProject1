using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;
using Analyzer;

namespace CodeAnalysisApp
{
    public class AnalisadorAST
    {
        private static readonly string NameSpacetextService = "Service";
        private static readonly string[] DependenciasService = { "System.Collections.Generic", "System.Composition", "System.Net.Http", "MicroServiceNet", "System.Threading.Tasks", "System.Web.Mvc"};

        public static PretendingClass Analisar(string classeText)
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
                pretendingClass.Interface = CreateInterfaceService(pretendingClass);
                pretendingClass.Classe = CreateClassService(pretendingClass);

                return pretendingClass;
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
                                pretendingMethod.RequestType = "Get";

                            if (tipoRequisicao.Equals("PostAsync"))
                                pretendingMethod.RequestType = "Post";

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
                                pretendingMethod.MicroServiceRoute = rota[rota.Length-2].Replace("\"", "");
                                NameHostMicroService = value.Replace(rota[rota.Length - 2]+ rota[rota.Length - 1], "");
                            }
                            else
                            {
                                pretendingMethod.MicroServiceRoute = rota[rota.Length-1].Replace("\"", "");
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


        static string CreateInterfaceService(PretendingClass pretendingClass)
        {
            //Cria um namespace da classe (namespace CodeGenerationSample)
            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(NameSpacetextService)).NormalizeWhitespace();

            // Add as dependencias da classe
            foreach (string dependence in DependenciasService)
            {
                @namespace = @namespace.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(dependence)));
            }

            //  Cria a classe
            var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration("I" + pretendingClass.Name + "Service");

            // Torna a classe pública
            interfaceDeclaration = interfaceDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));


            // Add a tag MicroServiceHost com o valor do HOST encontrado
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroServiceHost"), SyntaxFactory.ParseAttributeArgumentList("(\"" + pretendingClass.NameHostMicroService + "\")"));
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attribute));
            interfaceDeclaration = interfaceDeclaration.AddAttributeLists(attributeList);


            // Create a method
            foreach (var method in pretendingClass.Methods)
            {
                var attributeMethod = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroService"), SyntaxFactory.ParseAttributeArgumentList("(\"" + method.MicroServiceRoute + "\")"));
                var attributeListMethod = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attributeMethod));

                var methodDeclaration = SyntaxFactory

                    .MethodDeclaration(SyntaxFactory.ParseTypeName("Task<HttpResponseMessage>"), method.Name)
                    .AddAttributeLists(attributeListMethod)
                    .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("parameters"))
                            .WithType(SyntaxFactory.ParseTypeName("List<KeyValuePair<string, string>>"))
                            .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));


                // Add the field, the property and method to the class.
                interfaceDeclaration = interfaceDeclaration.AddMembers(methodDeclaration);
            }

            // Add the class to the namespace.
            @namespace = @namespace.AddMembers(interfaceDeclaration);

            // Normalize and get code as string.
            var code = @namespace
                .NormalizeWhitespace()
                .ToFullString();

            // Output new code to the console.
            return code;
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
            classDeclaration = classDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("I" + pretendingClass.Name + "Service")));


            // Add a tag MicroServiceHost com o valor do HOST encontrado
            var attribute1 = SyntaxFactory.Attribute(SyntaxFactory.ParseName("Export"), SyntaxFactory.ParseAttributeArgumentList($"(typeof(I{pretendingClass.Name}Service))"));
            var attributeList1 = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attribute1));
            classDeclaration = classDeclaration.AddAttributeLists(attributeList1);

            var attribute2 = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroServiceHost"), SyntaxFactory.ParseAttributeArgumentList("(\"" + pretendingClass.NameHostMicroService + "\")"));
            var attributeList2 = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attribute2));
            classDeclaration = classDeclaration.AddAttributeLists(attributeList2);

            // Create a method
            foreach (var method in pretendingClass.Methods)
            {
                var attributeMethod = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroService"), SyntaxFactory.ParseAttributeArgumentList("(\"" + method.MicroServiceRoute + "\")"));
                var attributeListMethod = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attributeMethod));

                var bodyMethod = SyntaxFactory.ParseStatement(String.Format("return Execute<{0}>({1}, HttpVerbs.{2}, parameters);", pretendingClass.Name, method.Name, method.RequestType));

                var methodDeclaration = SyntaxFactory

                    .MethodDeclaration(SyntaxFactory.ParseTypeName("IRestResponse"), method.Name)
                    .AddAttributeLists(attributeListMethod)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("parameters"))
                            .WithType(SyntaxFactory.ParseTypeName("List<KeyValuePair<strin, string>>"))
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
