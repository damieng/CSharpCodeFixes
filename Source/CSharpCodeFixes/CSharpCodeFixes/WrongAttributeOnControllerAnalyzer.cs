using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace CSharpCodeFixes
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WrongAttributeOnControllerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "WrongAttributeOnController";

        private const string webTechMixTitle = "Do not mix MVC and WebApi attributes and controllers";
        private const string webTechMixDescription = "The attribute '{0}' is part of the {1} and will not function on the controller '{2}' which uses {3}.";
        private static readonly DiagnosticDescriptor webTechMixDiagnosticDescriptor = new DiagnosticDescriptor(DiagnosticId, webTechMixTitle, webTechMixDescription,
            "Security", DiagnosticSeverity.Warning, true, "Mixing attributes from ASP.NET MVC or WebApi with controllers of the opposite technology stack are ignored.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(webTechMixDiagnosticDescriptor, webTechMixDiagnosticDescriptor);

        public override void Initialize(AnalysisContext context) => context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);

        enum WebTechnology { None, WebApi, Mvc };

        protected void AnalyzeType(SymbolAnalysisContext context)
        {
            var potentialController = (INamedTypeSymbol)context.Symbol;
            if (potentialController.AllInterfaces.Length == 0) return;

            var controllerTechnology = DetermineWebTechnology(potentialController.AllInterfaces);
            if (controllerTechnology == WebTechnology.None) return;

            var controllerAttributes = potentialController.GetAttributes();
            foreach (var controllerAttribute in controllerAttributes)
            {
                var attributeTechnology = DetermineWebTechnology(controllerAttribute.AttributeClass.AllInterfaces);
                if (attributeTechnology != WebTechnology.None && attributeTechnology != controllerTechnology)
                    context.ReportDiagnostic(Diagnostic.Create(webTechMixDiagnosticDescriptor, potentialController.Locations[0], 
                        controllerAttribute.AttributeClass.Name, attributeTechnology, potentialController.Name, controllerTechnology));
            }

            foreach (var method in potentialController.GetMembers().Where(m => m.Kind == SymbolKind.Method))
            {
                foreach (var methodAttribute in method.GetAttributes())
                {
                    var attributeTechnology = DetermineWebTechnology(methodAttribute.AttributeClass.AllInterfaces);
                    if (attributeTechnology != WebTechnology.None && attributeTechnology != controllerTechnology)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(webTechMixDiagnosticDescriptor, method.Locations[0],
                            methodAttribute.AttributeClass.Name,
                            attributeTechnology, potentialController.Name, controllerTechnology));
                        break;
                    }
                }
            }
        }

        private static WebTechnology DetermineWebTechnology(ImmutableArray<INamedTypeSymbol> interfaces)
        {
            foreach (var fullName in interfaces.Select(i => i.ToString()))
            {
                if (fullName.StartsWith("System.Web.Http"))
                    return WebTechnology.WebApi;
                if (fullName.StartsWith("System.Web.Mvc"))
                    return WebTechnology.Mvc;
            }

            return WebTechnology.None;
        }
    }
}