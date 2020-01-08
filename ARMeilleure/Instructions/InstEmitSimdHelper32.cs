﻿using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation;
using System;
using System.Collections.Generic;
using System.Text;

using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.Instructions.InstEmitSimdHelper;
using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    using Func1I = Func<Operand, Operand>;
    using Func2I = Func<Operand, Operand, Operand>;
    using Func3I = Func<Operand, Operand, Operand, Operand>;

    static class InstEmitSimdHelper32
    {
        public static (int, int) GetQuadwordAndSubindex(int index, RegisterSize size)
        {
            switch (size)
            {
                case RegisterSize.Simd128:
                    return (index >> 1, 0);
                case RegisterSize.Simd64:
                    return (index >> 1, index & 1);
                case RegisterSize.Simd32:
                    return (index >> 2, index & 3);
            }

            throw new NotImplementedException("Unrecognized Vector Register Size!");
        }

        public static Operand ExtractScalar(ArmEmitterContext context, OperandType type, int reg)
        {
            if (type == OperandType.FP64)
            {
                // from dreg
                return context.VectorExtract(type, GetVecA32(reg >> 1), reg & 1);
            } 
            else
            {
                // from sreg
                return context.VectorExtract(type, GetVecA32(reg >> 2), reg & 3);
            }
        }

        public static void InsertScalar(ArmEmitterContext context, int reg, Operand value)
        {
            Operand vec, insert;
            if (value.Type == OperandType.FP64)
            {
                // from dreg
                vec = GetVecA32(reg >> 1);
                insert = context.VectorInsert(vec, value, reg & 1);
                
            }
            else
            {
                // from sreg
                vec = GetVecA32(reg >> 2);
                insert = context.VectorInsert(vec, value, reg & 3);
            }
            context.Copy(vec, insert);
        }

        public static void EmitVectorImmUnaryOp32(ArmEmitterContext context, Func1I emit)
        {
            IOpCode32SimdImm op = (IOpCode32SimdImm)context.CurrOp;

            Operand imm = Const(op.Immediate);

            int elems = op.Elems;
            (int index, int subIndex) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand vec = GetVecA32(index);
            Operand res = vec;

            for (int item = 0; item < elems; item++)
            {
                res = EmitVectorInsert(context, res, emit(imm), item + subIndex * elems, op.Size);
            }

            context.Copy(vec, res);
        }

        public static void EmitScalarUnaryOpF32(ArmEmitterContext context, Func1I emit)
        {
            OpCode32SimdS op = (OpCode32SimdS)context.CurrOp;

            OperandType type = (op.Size & 1) != 0 ? OperandType.FP64 : OperandType.FP32;

            Operand m = ExtractScalar(context, type, op.Vm);

            InsertScalar(context, op.Vd, emit(m));
        }

        public static void EmitScalarBinaryOpF32(ArmEmitterContext context, Func2I emit)
        {
            OpCode32SimdRegS op = (OpCode32SimdRegS)context.CurrOp;

            OperandType type = (op.Size & 1) != 0 ? OperandType.FP64 : OperandType.FP32;

            Operand n = ExtractScalar(context, type, op.Vn);
            Operand m = ExtractScalar(context, type, op.Vm);

            InsertScalar(context, op.Vd, emit(n, m));
        }
        public static void EmitScalarTernaryOpF32(ArmEmitterContext context, Func3I emit)
        {
            OpCode32SimdRegS op = (OpCode32SimdRegS)context.CurrOp;

            OperandType type = (op.Size & 1) != 0 ? OperandType.FP64 : OperandType.FP32;

            Operand a = ExtractScalar(context, type, op.Vd);
            Operand n = ExtractScalar(context, type, op.Vn);
            Operand m = ExtractScalar(context, type, op.Vm);

            InsertScalar(context, op.Vd, emit(a, n, m));
        }

        public static void EmitVectorUnaryOpF32(ArmEmitterContext context, Func1I emit)
        {
            OpCode32Simd op = (OpCode32Simd)context.CurrOp;

            int sizeF = op.Size & 1;

            OperandType type = sizeF != 0 ? OperandType.FP64 : OperandType.FP32;

            int elems = op.GetBytesCount() >> sizeF + 2;

            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            for (int index = 0; index < elems; index++)
            {
                Operand ne = context.VectorExtract(type, GetVecA32(vm), index + em * elems);

                res = context.VectorInsert(res, emit(ne), index + ed * elems);
            }

            context.Copy(GetVecA32(vd), res);
        }

        public static void EmitVectorBinaryOpF32(ArmEmitterContext context, Func2I emit)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            int sizeF = op.Size & 1;

            OperandType type = sizeF != 0 ? OperandType.FP64 : OperandType.FP32;

            int elems = op.GetBytesCount() >> (sizeF + 2);

            (int vn, int en) = GetQuadwordAndSubindex(op.Vn, op.RegisterSize);
            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            for (int index = 0; index < elems; index++)
            {
                Operand ne = context.VectorExtract(type, GetVecA32(vn), index + en * elems);
                Operand me = context.VectorExtract(type, GetVecA32(vm), index + em * elems);

                res = context.VectorInsert(res, emit(ne, me), index + ed * elems);
            }

            context.Copy(GetVecA32(vd), res);
        }

        public static void EmitVectorTernaryOpF32(ArmEmitterContext context, Func3I emit)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            int sizeF = op.Size & 1;

            OperandType type = sizeF != 0 ? OperandType.FP64 : OperandType.FP32;

            int elems = op.GetBytesCount() >> sizeF + 2;

            (int vn, int en) = GetQuadwordAndSubindex(op.Vn, op.RegisterSize);
            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            for (int index = 0; index < elems; index++)
            {
                Operand de = context.VectorExtract(type, GetVecA32(vd), index + ed * elems);
                Operand ne = context.VectorExtract(type, GetVecA32(vn), index + en * elems);
                Operand me = context.VectorExtract(type, GetVecA32(vm), index + em * elems);

                res = context.VectorInsert(res, emit(de, ne, me), index);
            }

            context.Copy(GetVecA32(vd), res);
        }

        // INTEGER

        public static void EmitVectorUnaryOpSx32(ArmEmitterContext context, Func1I emit)
        {
            OpCode32Simd op = (OpCode32Simd)context.CurrOp;

            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand ne = EmitVectorExtractSx(context, vm, index + em * elems, op.Size);

                res = EmitVectorInsert(context, res, emit(ne), index + ed * elems, op.Size);
            }

            context.Copy(GetVecA32(vd), res);
        }

        public static void EmitVectorBinaryOpSx32(ArmEmitterContext context, Func2I emit)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vn, int en) = GetQuadwordAndSubindex(op.Vn, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand ne = EmitVectorExtractSx(context, vn, index + en * elems, op.Size);
                Operand me = EmitVectorExtractSx(context, vm, index + em * elems, op.Size);

                res = EmitVectorInsert(context, res, emit(ne, me), index + ed * elems, op.Size);
            }

            context.Copy(GetVecA32(vd), res);
        }

        public static void EmitVectorTernaryOpSx32(ArmEmitterContext context, Func3I emit)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vn, int en) = GetQuadwordAndSubindex(op.Vn, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand de = EmitVectorExtractSx(context, vd, index + ed * elems, op.Size);
                Operand ne = EmitVectorExtractSx(context, vn, index + en * elems, op.Size);
                Operand me = EmitVectorExtractSx(context, vm, index + em * elems, op.Size);

                res = EmitVectorInsert(context, res, emit(de, ne, me), index + ed * elems, op.Size);
            }

            context.Copy(GetVecA32(vd), res);
        }

        public static void EmitVectorUnaryOpZx32(ArmEmitterContext context, Func1I emit)
        {
            OpCode32Simd op = (OpCode32Simd)context.CurrOp;

            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand ne = EmitVectorExtractZx(context, vm, index + em * elems, op.Size);

                res = EmitVectorInsert(context, res, emit(ne), index + ed * elems, op.Size);
            }

            context.Copy(GetVecA32(vd), res);
        }

        public static void EmitVectorBinaryOpZx32(ArmEmitterContext context, Func2I emit)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vn, int en) = GetQuadwordAndSubindex(op.Vn, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand ne = EmitVectorExtractZx(context, vn, index + en * elems, op.Size);
                Operand me = EmitVectorExtractZx(context, vm, index + em * elems, op.Size);

                res = EmitVectorInsert(context, res, emit(ne, me), index + ed * elems, op.Size);
            }

            context.Copy(GetVecA32(vd), res);
        }

        public static void EmitVectorTernaryOpZx32(ArmEmitterContext context, Func3I emit)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            (int vm, int em) = GetQuadwordAndSubindex(op.Vm, op.RegisterSize);
            (int vn, int en) = GetQuadwordAndSubindex(op.Vn, op.RegisterSize);
            (int vd, int ed) = GetQuadwordAndSubindex(op.Vd, op.RegisterSize);

            Operand res = GetVecA32(vd);

            int elems = op.GetBytesCount() >> op.Size;

            for (int index = 0; index < elems; index++)
            {
                Operand de = EmitVectorExtractZx(context, vd, index + ed * elems, op.Size);
                Operand ne = EmitVectorExtractZx(context, vn, index + en * elems, op.Size);
                Operand me = EmitVectorExtractZx(context, vm, index + em * elems, op.Size);

                res = EmitVectorInsert(context, res, emit(de, ne, me), index, op.Size);
            }

            context.Copy(GetVecA32(vd), res);
        }
    }
}
