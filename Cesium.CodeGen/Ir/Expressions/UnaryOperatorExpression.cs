using Cesium.CodeGen.Contexts;
using Cesium.CodeGen.Extensions;
using Cesium.CodeGen.Ir.Expressions.Values;
using Cesium.CodeGen.Ir.Types;
using Cesium.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Cesium.CodeGen.Ir.Expressions;

internal class UnaryOperatorExpression : IExpression
{
    private readonly UnaryOperator _operator;
    private readonly IExpression _target;

    internal UnaryOperatorExpression(UnaryOperator @operator, IExpression target)
    {
        _operator = @operator;
        _target = target;
    }

    public UnaryOperatorExpression(Ast.UnaryOperatorExpression expression)
    {
        var (@operator, target) = expression;
        _operator = GetOperatorKind(@operator);
        _target = target.ToIntermediate();
    }

    public IExpression Lower() => new UnaryOperatorExpression(_operator, _target.Lower());

    public void EmitTo(IDeclarationScope scope)
    {
        switch (_operator)
        {
            case UnaryOperator.AddressOf:
                EmitGetAddress(_target);
                break;
            case UnaryOperator.LogicalNot:
                _target.EmitTo(scope);
                scope.Method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                scope.Method.Body.Instructions.Add(Instruction.Create(OpCodes.Ceq));
                break;
            default:
                _target.EmitTo(scope);
                scope.Method.Body.Instructions.Add(GetInstruction());
                break;
        }

        Instruction GetInstruction() => _operator switch
        {
            UnaryOperator.Negation => Instruction.Create(OpCodes.Neg),
            UnaryOperator.BitwiseNot => Instruction.Create(OpCodes.Not),
            _ => throw new WipException(197, $"Unsupported unary operator: {_operator}.")
        };

        void EmitGetAddress(IExpression target)
        {
            if (target is not IValueExpression expression)
                throw new CompilationException($"Required a value expression to get address, got {target} instead.");

            var value = expression.Resolve(scope);
            if (value is not IAddressableValue aValue)
                throw new CompilationException($"Required an addressable value to get address, got {value} instead.");

            aValue.EmitGetAddress(scope);
            scope.Method.Body.Instructions.Add(Instruction.Create(OpCodes.Conv_U));
        }
    }

    public IType GetExpressionType(IDeclarationScope scope) => _operator switch
    {
        UnaryOperator.AddressOf => _target.GetExpressionType(scope).MakePointerType(), // address-of returns T*
        _ => _target.GetExpressionType(scope), // other operators return T
    };

    private static UnaryOperator GetOperatorKind(string @operator) => @operator switch
    {
        "-" => UnaryOperator.Negation,
        "!" => UnaryOperator.LogicalNot,
        "~" => UnaryOperator.BitwiseNot,
        "&" => UnaryOperator.AddressOf,
        _ => throw new WipException(197, $"Unary operator not supported, yet: {@operator}."),
    };
}
