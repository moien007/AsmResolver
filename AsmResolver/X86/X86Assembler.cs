﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsmResolver.X86
{
    public class X86Assembler
    {
        private readonly IBinaryStreamWriter _writer;

        public X86Assembler(IBinaryStreamWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");
            _writer = writer;
        }

        public void Write(X86Instruction instruction)
        {
            var opcode = instruction.OpCode;
            WriteOpCode(opcode);

            var mnemonicIndex = Array.IndexOf(opcode.Mnemonics, instruction.Mnemonic);
            if (mnemonicIndex == -1)
                throw new ArgumentException("Instruction's mnemonic is not supported by its opcode.");

            if (opcode.HasRegisterToken)
            {
                var token = (byte)(ComputeRegisterTokenPart(opcode.AddressingMethods1[mnemonicIndex],
                                        opcode.OperandSizes1[mnemonicIndex], instruction.Operand1) |
                                   ComputeRegisterTokenPart(opcode.AddressingMethods2[mnemonicIndex],
                                       opcode.OperandSizes2[mnemonicIndex], instruction.Operand2));
                _writer.WriteByte(token);
            }

            if (instruction.Operand1 != null)
            {
                WriteOperand(opcode.AddressingMethods1[mnemonicIndex],
                    opcode.OperandSizes1[mnemonicIndex], instruction.Operand1);
                if (instruction.Operand2 != null)
                {
                    WriteOperand(opcode.AddressingMethods2[mnemonicIndex],
                        opcode.OperandSizes2[mnemonicIndex], instruction.Operand2);
                }
            }
        }

        private void WriteOperand(X86AddressingMethod method, X86OperandSize size, X86Operand operand)
        {
            switch (method)
            {
                case X86AddressingMethod.MemoryAddress:
                case X86AddressingMethod.DirectAddress:
                case X86AddressingMethod.ImmediateData:
                    WriteNumber(operand.Value, size);
                    break;
                case X86AddressingMethod.RegisterOrMemoryAddress:
                    if (!(operand.Value is X86Register))
                        WriteNumber(operand.Value, X86OperandSize.Dword);
                    break;
                case X86AddressingMethod.RelativeOffset:
                    break;
                default: 
                    return;
            }

            if (operand.Correction != null)
            {
                if (operand.Correction is sbyte)
                    _writer.WriteSByte((sbyte)operand.Correction);
                else if (operand.Correction is int)
                    _writer.WriteInt32((int)operand.Correction);
                else
                    throw new ArgumentException("Operand correction is invalid.");
            }
            
        }

        private void WriteNumber(object value, X86OperandSize size)
        {
            switch (size)
            {
                case X86OperandSize.Byte:
                    _writer.WriteByte(Convert.ToByte(value));
                    break;
                case X86OperandSize.Word:
                    _writer.WriteUInt16(Convert.ToUInt16(value));
                    break;
                case X86OperandSize.Dword:
                    _writer.WriteUInt32(Convert.ToUInt32(value));
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void WriteOpCode(X86OpCode opCode)
        {
            _writer.WriteByte(opCode.Op1);
            // todo: multibyte opcodes.
        }
        
        private static byte ComputeRegisterTokenPart(X86AddressingMethod method, X86OperandSize size, X86Operand operand)
        {
            switch (method)
            {
                case X86AddressingMethod.Register:
                {
                    return (byte)(ComputeRegisterToken((X86Register)operand.Value) << 3);
                }
                case X86AddressingMethod.RegisterOrMemoryAddress:
                {
                    return ComputeRegOrMemToken(operand);
                }
            }

            throw new ArgumentException(string.Format("Cannot compute a register token from a {0} addressing method.", method));
        }

        private static byte ComputeRegisterToken(X86Register register)
        {
            return (byte)((byte)(register) & 7);
        }

        private static byte ComputeRegOrMemToken(X86Operand operand)
        {
            var modifier = DetermineRegOrMemModifier(operand);
            var token = (byte)((byte)modifier << 6);

            if (operand.Value is X86Register)
                token |= ComputeRegisterToken((X86Register)operand.Value);
            else
                return ComputeRegisterToken(X86Register.Ebp);

            switch (modifier)
            {
                case X86RegOrMemModifier.RegisterPointer:
                    break;
                case X86RegOrMemModifier.RegisterDispShortPointer:
                    token |= 0x40;
                    break;
                case X86RegOrMemModifier.RegisterDispLongPointer:
                    token |= 0x80;
                    break;
                case X86RegOrMemModifier.RegisterOnly:
                    token |= 0xC0;
                    break;
            }

            return token;
        }

        private static X86RegOrMemModifier DetermineRegOrMemModifier(X86Operand operand)
        {
            if (operand.Value is X86Register)
            {
                if (operand.Correction == null)
                {
                    return operand.OperandType == X86OperandType.Normal
                        ? X86RegOrMemModifier.RegisterOnly
                        : X86RegOrMemModifier.RegisterPointer;
                }

                return operand.Correction is sbyte
                    ? X86RegOrMemModifier.RegisterDispShortPointer
                    : X86RegOrMemModifier.RegisterDispLongPointer;
            }

            if (operand.Value is uint)
                return X86RegOrMemModifier.RegisterPointer;

            throw new ArgumentException("Operand is not a valid RegOrMem operand.", "operand");
        }
    }
}