using Cesium.Ast;

namespace Cesium.CodeGen.Extensions;

public static class DirectDeclaratorEx // TODO[F]: Delete this class
{
    public static string GetIdentifier(this IDirectDeclarator declarator) => declarator switch
    {
        IdentifierDirectDeclarator id => id.Identifier,
        { Base: {} @base } => @base.GetIdentifier(),
        _ => throw new ArgumentException(
            $"Attempt to extract an identifier from a declarator of incorrect shape: {declarator}.")
    };
}
