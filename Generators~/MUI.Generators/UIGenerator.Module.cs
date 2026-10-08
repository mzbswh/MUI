using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MUI.Generators
{
    public sealed partial class UIGenerator
    {
        /// <summary>只读取当前程序集的模块声明，验证生成名称，不引入运行时模块系统。</summary>
        private static bool TryGetModule(GenerationContext context, out string generatedNamespace,
            out string registryName)
        {
            generatedNamespace = "MUI.Generated";
            registryName = Identifier(context.Compilation.AssemblyName ?? "Assembly") + "Bindings";
            var attribute = context.Compilation.Assembly.GetAttributes().FirstOrDefault(value =>
                value.AttributeClass?.ToDisplayString() == "MUI.ViewModuleAttribute");
            if (attribute != null)
            {
                generatedNamespace = attribute.ConstructorArguments.Length > 0
                    ? attribute.ConstructorArguments[0].Value as string : null;
                registryName = attribute.ConstructorArguments.Length > 1
                    ? attribute.ConstructorArguments[1].Value as string : "Bindings";
                if (string.IsNullOrWhiteSpace(generatedNamespace) ||
                    !generatedNamespace.Split('.').All(IsModuleIdentifier) || !IsModuleIdentifier(registryName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidContract,
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                        "ViewModule 需要有效的点分命名空间和注册类名；不允许空名称、关键字或转义标识符。"));
                    return false;
                }
            }

            if (context.Compilation.GetTypeByMetadataName(generatedNamespace + "." + registryName) != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidContract,
                    attribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                    "生成的绑定注册入口与现有类型冲突，请通过 ViewModule 指定其他命名空间或类名。"));
                return false;
            }
            return true;
        }

        private static bool IsModuleIdentifier(string value) =>
            !string.IsNullOrEmpty(value) && value[0] != '@' && SyntaxFacts.IsValidIdentifier(value) &&
            SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;
    }
}
