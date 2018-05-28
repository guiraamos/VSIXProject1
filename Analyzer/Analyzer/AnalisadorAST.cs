using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Analyzer;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;

namespace CodeAnalysisApp
{
    public class AnalisadorAST
    {
        private static readonly string NameSpacetextService = "Service";
        private static readonly string[] DependenciasClasseService = { "System.Collections.Generic", "System.Net.Http", "System.Threading.Tasks", "MicroServiceNet", "Microsoft.Extensions.Logging", "Pivotal.Discovery.Client" };
        private static readonly string[] DependenciasInterfaceService = { "System.Collections.Generic", "System.Net.Http", "System.Threading.Tasks", "MicroServiceNet" };

        public static PretendingClass Analisar(string classeText)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(classeText);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var namespaceDeclaration = (NamespaceDeclarationSyntax)root.Members[0];
            var classe = (ClassDeclarationSyntax)namespaceDeclaration.Members[0];

            var pretendingClass = new PretendingClass() { Name = Util.TrataNome(classe.Identifier.ValueText) + "Service" };

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
                        if (value.Contains("http"))
                        {
                            var rota = value.Split('/');
                            var NameHostMicroService = "";
                            if (rota[rota.Length - 1] == "\"")
                            {
                                pretendingMethod.MicroServiceRoute = rota[rota.Length - 2].Replace("\"", "");
                                NameHostMicroService = value.Replace(rota[rota.Length - 2] + rota[rota.Length - 1], "");
                            }
                            else
                            {
                                pretendingMethod.MicroServiceRoute = rota[rota.Length - 1].Replace("\"", "");
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
            foreach (string dependence in DependenciasInterfaceService)
            {
                @namespace = @namespace.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(dependence)));
            }

            //  Cria a classe
            var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration("I" + pretendingClass.Name);

            // Torna a classe pública
            interfaceDeclaration = interfaceDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            // Adiciona a herânca MicroServiceBase a classe
            interfaceDeclaration = interfaceDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("IMicroService")));

            // Add a tag MicroServiceHost com o valor do HOST encontrado
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroServiceHost"), SyntaxFactory.ParseAttributeArgumentList("(\"" + BuscarMicroServico(pretendingClass.NameHostMicroService) + "\")"));
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList<AttributeSyntax>().Add(attribute));
            interfaceDeclaration = interfaceDeclaration.AddAttributeLists(attributeList);

            // Cria  assinatura
            foreach (var method in pretendingClass.Methods)
            {
                var attributeMethod = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MicroService"), SyntaxFactory.ParseAttributeArgumentList("(\"" + method.MicroServiceRoute + "\", TypeRequest." + method.RequestType + ")"));
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

        private static string BuscarMicroServico(string URL)
        {

            var arrayURL = URL.Split('/')
                         .Select(x => x.Trim())
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .ToArray();

            var client = new HttpClient();

            try
            {
                var responseContent = client.GetAsync("http://localhost:8761/eureka/vips/" + arrayURL[1]).Result.Content.ReadAsStringAsync().Result;

                XmlDocument xDoc = new XmlDocument();
                xDoc.LoadXml(responseContent);

                string name = xDoc.GetElementsByTagName("name")[0].InnerText;

                if (String.IsNullOrWhiteSpace(name))
                    return URL;

                return name;
            }
            catch (Exception)
            {
                return URL;
            }


        }


        static string CreateClassService(PretendingClass pretendingClass)
        {
            //Cria um namespace da classe (namespace CodeGenerationSample)
            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(NameSpacetextService)).NormalizeWhitespace();

            // Add as dependencias da classe
            foreach (string dependence in DependenciasClasseService)
            {
                @namespace = @namespace.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(dependence)));
            }

            //  Cria a classe
            var classDeclaration = SyntaxFactory.ClassDeclaration(pretendingClass.Name);

            // Torna a classe pública
            classDeclaration = classDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            // Adiciona a herânca MicroServiceBase a classe
            classDeclaration = classDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(String.Format("MicroService<{0}>", pretendingClass.Name))));
            classDeclaration = classDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("I" + pretendingClass.Name)));

            //Cria Contrutor da Classe
            ConstructorInitializerSyntax ciWithParseArgList = SyntaxFactory.ConstructorInitializer(
                SyntaxKind.BaseConstructorInitializer,
                SyntaxFactory.ParseArgumentList("(client, logFactory){}"));

            var constructorDeclaration = SyntaxFactory
                .ConstructorDeclaration(pretendingClass.Name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("client"))
                    .WithType(SyntaxFactory.ParseTypeName("IDiscoveryClient")))
                .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("logFactory"))
                    .WithType(SyntaxFactory.ParseTypeName("ILoggerFactory")))
                .WithInitializer(ciWithParseArgList);


            // Add the field, the property and method to the class.
            classDeclaration = classDeclaration.AddMembers(constructorDeclaration);



            foreach (var method in pretendingClass.Methods)
            {
                var bodyMethod = SyntaxFactory.ParseStatement(String.Format("return Execute({0}, parameters);", method.Name));

                var methodDeclaration = SyntaxFactory

                    .MethodDeclaration(SyntaxFactory.ParseTypeName("Task<HttpResponseMessage>"), method.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("parameters"))
                            .WithType(SyntaxFactory.ParseTypeName("List<KeyValuePair<string, string>>"))
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
