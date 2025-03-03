using Cesium.CodeGen.Contexts;
using Cesium.CodeGen.Extensions;
using Cesium.CodeGen.Ir.Expressions.Constants;
using Cesium.CodeGen.Ir.Types;
using Cesium.Core;
using Mono.Cecil.Cil;

namespace Cesium.CodeGen.Ir.Expressions.BinaryOperators;

internal class ComparisonBinaryOperatorExpression: BinaryOperatorExpression
{
    internal ComparisonBinaryOperatorExpression(IExpression left, BinaryOperator @operator, IExpression right)
        : base(left, @operator, right)
    {
        if (!Operator.IsComparison())
            throw new AssertException($"Operator {Operator} is not comparison.");
    }

    public ComparisonBinaryOperatorExpression(Ast.ComparisonBinaryOperatorExpression expression)
        : base(expression)
    {
    }

    public override IExpression Lower() => Operator switch
    {
        BinaryOperator.GreaterThanOrEqualTo => new ComparisonBinaryOperatorExpression(
            new ComparisonBinaryOperatorExpression(Left.Lower(), BinaryOperator.LessThan, Right.Lower()),
            BinaryOperator.EqualTo,
            new ConstantExpression(new IntegerConstant("0"))
        ),
        BinaryOperator.LessThanOrEqualTo => new ComparisonBinaryOperatorExpression(
            new ComparisonBinaryOperatorExpression(Left.Lower(), BinaryOperator.GreaterThan, Right.Lower()),
            BinaryOperator.EqualTo,
            new ConstantExpression(new IntegerConstant("0"))
        ),
        BinaryOperator.NotEqualTo => new ComparisonBinaryOperatorExpression(
            new ComparisonBinaryOperatorExpression(Left.Lower(), BinaryOperator.EqualTo, Right.Lower()),
            BinaryOperator.EqualTo,
            new ConstantExpression(new IntegerConstant("0"))
        ),
        _ => new ComparisonBinaryOperatorExpression(Left.Lower(), Operator, Right.Lower()),
    };

    public override void EmitTo(IDeclarationScope scope)
    {
        var leftType = Left.GetExpressionType(scope);
        var rightType = Right.GetExpressionType(scope);

        if ((!scope.CTypeSystem.IsNumeric(leftType) && leftType is not PointerType)
            || (!scope.CTypeSystem.IsNumeric(rightType) && rightType is not PointerType))
            throw new CompilationException($"Unable to compare {leftType} to {rightType}");

        var commonType = scope.CTypeSystem.GetCommonNumericType(leftType, rightType);

        Left.EmitTo(scope);
        EmitConversion(scope, leftType, commonType);

        Right.EmitTo(scope);
        EmitConversion(scope, rightType, commonType);

        scope.Method.Body.Instructions.Add(GetInstruction());

        Instruction GetInstruction() => Operator switch
        {
            BinaryOperator.GreaterThan => Instruction.Create(OpCodes.Cgt),
            BinaryOperator.LessThan => Instruction.Create(OpCodes.Clt),
            BinaryOperator.EqualTo => Instruction.Create(OpCodes.Ceq),
            _ => throw new AssertException($"Unsupported binary operator: {Operator}.")
        };
    }

    public override IType GetExpressionType(IDeclarationScope scope) => scope.CTypeSystem.Bool;
}
