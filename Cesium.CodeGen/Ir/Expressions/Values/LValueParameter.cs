using Cesium.CodeGen.Contexts;
using Cesium.CodeGen.Ir.Types;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Cesium.CodeGen.Ir.Expressions.Values;

internal class LValueParameter : ILValue
{
    private readonly ParameterDefinition _definition;
    private readonly ParameterInfo _parameterInfo;
    public LValueParameter(ParameterInfo parameterInfo, ParameterDefinition definition)
    {
        _parameterInfo = parameterInfo;
        _definition = definition;
    }

    public void EmitGetValue(IDeclarationScope scope)
    {
        scope.Method.Body.Instructions.Add(_definition.Index switch
        {
            0 => Instruction.Create(OpCodes.Ldarg_0),
            1 => Instruction.Create(OpCodes.Ldarg_1),
            2 => Instruction.Create(OpCodes.Ldarg_2),
            3 => Instruction.Create(OpCodes.Ldarg_3),
            <= byte.MaxValue => Instruction.Create(OpCodes.Ldarg_S, _definition),
            _ => Instruction.Create(OpCodes.Ldarg, _definition)
        });
    }

    public void EmitGetAddress(IDeclarationScope scope)
    {
        scope.Method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarga, _definition));
    }

    public void EmitSetValue(IDeclarationScope scope, IExpression value)
    {
        value.EmitTo(scope);

        scope.Method.Body.Instructions.Add(_definition.Index switch
        {
            <= byte.MaxValue => Instruction.Create(OpCodes.Starg_S, _definition),
            _ => Instruction.Create(OpCodes.Starg, _definition)
        });
    }

    public IType GetValueType() => _parameterInfo.Type;
}
