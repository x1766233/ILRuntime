﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Cecil;

using ILRuntime.CLR.TypeSystem;
using ILRuntime.CLR.Method;
using ILRuntime.Runtime.Stack;
using ILRuntime.Runtime.Intepreter.OpCodes;
using ILRuntime.Runtime.Enviorment;
using ILRuntime.CLR.Utils;

namespace ILRuntime.Runtime.Intepreter
{
    unsafe struct RegisterFrameInfo
    {
        public ILIntepreter Intepreter;
        public int LocalManagedBase;
        public int ParameterCount;
        public int LocalCount;
        public StackObject* RegisterStart;
        public IList<object> ManagedStack;
    }
    public unsafe partial class ILIntepreter
    {   
        internal StackObject* ExecuteR(ILMethod method, StackObject* esp, out bool unhandledException)
        {
            if (method == null)
                throw new NullReferenceException();
#if UNITY_EDITOR
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == AppDomain.UnityMainThreadID)

#if UNITY_5_5_OR_NEWER
                UnityEngine.Profiling.Profiler.BeginSample(method.ToString());
#else
                UnityEngine.Profiler.BeginSample(method.ToString());
#endif

#endif
            OpCodeR[] body = method.BodyRegister;
            StackFrame frame;
            stack.InitializeFrame(method, esp, out frame); 
            StackObject* v1 = frame.LocalVarPointer;
            int finallyEndAddress = 0;

            var stackRegStart = frame.BasePointer;
            esp = Add(frame.BasePointer, method.StackRegisterCount);
            StackObject* r = Minus(frame.LocalVarPointer, method.ParameterCount);
            IList<object> mStack = stack.ManagedStack;
            int paramCnt = method.ParameterCount;
            if (method.HasThis)//this parameter is always object reference
            {
                r--;
                paramCnt++;
            }
            unhandledException = false;
            var hasReturn = method.ReturnType != AppDomain.VoidType;

            //Managed Stack reserved for arguments(In case of starg)
            for (int i = 0; i < paramCnt; i++)
            {
                var a = Add(r, i);
                switch (a->ObjectType)
                {
                    case ObjectTypes.Null:
                        //Need to reserve place for null, in case of starg
                        a->ObjectType = ObjectTypes.Object;
                        a->Value = mStack.Count;
                        mStack.Add(null);
                        break;
                    case ObjectTypes.ValueTypeObjectReference:
                        CloneStackValueType(a, a, mStack);
                        break;
                    case ObjectTypes.Object:
                    case ObjectTypes.FieldReference:
                    case ObjectTypes.ArrayReference:
                        frame.ManagedStackBase--;
                        break;
                }
            }

            stack.PushFrame(ref frame);

            int locBase = mStack.Count;
            int locCnt = method.LocalVariableCount;
            int stackRegCnt = method.StackRegisterCount;
            RegisterFrameInfo info;
            info.Intepreter = this;
            info.LocalCount = locCnt;
            info.LocalManagedBase = locBase;
            info.ParameterCount = paramCnt;
            info.RegisterStart = r;
            info.ManagedStack = mStack;
            //Managed Stack reserved for local variable
            for (int i = 0; i < locCnt; i++)
            {
                mStack.Add(null);
            }

            for (int i = 0; i < locCnt; i++)
            {
                var v = method.Variables[i];
                if (v.VariableType.IsValueType && !v.VariableType.IsPrimitive)
                {
                    var t = AppDomain.GetType(v.VariableType, method.DeclearingType, method);
                    if (t is ILType)
                    {
                        //var obj = ((ILType)t).Instantiate(false);
                        var loc = Add(v1, i);
                        stack.AllocValueType(loc, t);

                        /*loc->ObjectType = ObjectTypes.Object;
                        loc->Value = mStack.Count;
                        mStack.Add(obj);*/

                    }
                    else
                    {
                        CLRType cT = (CLRType)t;
                        var loc = Add(v1, i);
                        if (cT.ValueTypeBinder != null)
                        {
                            stack.AllocValueType(loc, t);
                        }
                        else
                        {
                            var obj = ((CLRType)t).CreateDefaultInstance();
                            loc->ObjectType = ObjectTypes.Object;
                            loc->Value = locBase + i;
                            mStack[locBase + i] = obj;
                        }
                    }
                }
                else
                {
                    if (v.VariableType.IsPrimitive)
                    {
                        var t = AppDomain.GetType(v.VariableType, method.DeclearingType, method);
                        var loc = Add(v1, i);
                        StackObject.Initialized(loc, t);
                    }
                    else
                    {
                        var loc = Add(v1, i);
                        loc->ObjectType = ObjectTypes.Object;
                        loc->Value = locBase + i;
                    }
                }
            }
            for (int i = 0; i < stackRegCnt; i++)
            {
                var loc = Add(stackRegStart, i);
                *loc = StackObject.Null;
                mStack.Add(null);
            }
            var bp = stack.ValueTypeStackPointer;
            ValueTypeBasePointer = bp;

            StackObject* reg1, reg2, reg3, objRef;

            fixed (OpCodeR* ptr = body)
            {
                OpCodeR* ip = ptr;
                OpCodeREnum code = ip->Code;
                bool returned = false;
                while (!returned)
                {
                    try
                    {
#if DEBUG && !DISABLE_ILRUNTIME_DEBUG
                        if (ShouldBreak)
                            Break();
                        var insOffset = (int)(ip - ptr);
                        frame.Address.Value = insOffset;
                        AppDomain.DebugService.CheckShouldBreak(method, this, insOffset);
#endif
                        code = ip->Code;
                        switch (code)
                        {
                            #region Load Constants
                            case OpCodeREnum.Ldc_I4_0:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 0;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_1:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 1;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_2:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 2;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_3:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 3;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_4:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 4;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_5:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 5;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_6:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 6;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_7:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 7;
                                }
                                break;
                            case OpCodeREnum.Ldc_I4_8:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = 8;
                                }
                                break;

                            case OpCodeREnum.Ldc_I4:
                            case OpCodeREnum.Ldc_I4_S:
                                reg1 = Add(r, ip->Register1);
                                reg1->ObjectType = ObjectTypes.Integer;
                                reg1->Value = ip->Operand;
                                break;
                            case OpCodeREnum.Ldstr:
                                AssignToRegister(ref info, ip->Register1, AppDomain.GetString(ip->OperandLong));
                                break;
                            #endregion

                            #region Althemetics
                            case OpCodeREnum.Add:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register3);
                                    reg3 = Add(r, ip->Register1);
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Long:
                                            reg3->ObjectType = ObjectTypes.Long;
                                            *((long*)&reg3->Value) = *((long*)&reg1->Value) + *((long*)&reg2->Value);
                                            break;
                                        case ObjectTypes.Integer:
                                            reg3->ObjectType = ObjectTypes.Integer;
                                            reg3->Value = reg1->Value + reg2->Value;
                                            break;
                                        case ObjectTypes.Float:
                                            reg3->ObjectType = ObjectTypes.Float;
                                            *((float*)&reg3->Value) = *((float*)&reg1->Value) + *((float*)&reg2->Value);
                                            break;
                                        case ObjectTypes.Double:
                                            reg3->ObjectType = ObjectTypes.Double;
                                            *((double*)&reg3->Value) = *((double*)&reg1->Value) + *((double*)&reg2->Value);
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                }
                                break;
                            case OpCodeREnum.Div:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register3);
                                    reg3 = Add(r, ip->Register1);
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Long:
                                            reg3->ObjectType = ObjectTypes.Long;
                                            *((long*)&reg3->Value) = *((long*)&reg1->Value) / *((long*)&reg2->Value);
                                            break;
                                        case ObjectTypes.Integer:
                                            reg3->ObjectType = ObjectTypes.Integer;
                                            reg3->Value = reg1->Value / reg2->Value;
                                            break;
                                        case ObjectTypes.Float:
                                            reg3->ObjectType = ObjectTypes.Float;
                                            *((float*)&reg3->Value) = *((float*)&reg1->Value) / *((float*)&reg2->Value);
                                            break;
                                        case ObjectTypes.Double:
                                            reg3->ObjectType = ObjectTypes.Double;
                                            *((double*)&reg3->Value) = *((double*)&reg1->Value) / *((double*)&reg2->Value);
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                }
                                break;
                            case OpCodeREnum.Div_Un:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register3);
                                    reg3 = Add(r, ip->Register1);
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Long:
                                            reg3->ObjectType = ObjectTypes.Long;
                                            *((ulong*)&reg3->Value) = *((ulong*)&reg1->Value) / *((ulong*)&reg2->Value);
                                            break;
                                        case ObjectTypes.Integer:
                                            reg3->ObjectType = ObjectTypes.Integer;
                                            reg3->Value = (int)((uint)reg1->Value / (uint)reg2->Value);
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                }
                                break;
                            #endregion

                            #region Conversion
                            case OpCodeREnum.Conv_I4:
                            case OpCodeREnum.Conv_I:
                            case OpCodeREnum.Conv_Ovf_I:
                            case OpCodeREnum.Conv_Ovf_I_Un:
                            case OpCodeREnum.Conv_Ovf_I4:
                            case OpCodeREnum.Conv_Ovf_I4_Un:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register1);
                                    int val;
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Long:
                                            val = (int)*(long*)&reg1->Value;
                                            break;
                                        case ObjectTypes.Float:
                                            val = (int)*(float*)&reg1->Value;
                                            break;
                                        case ObjectTypes.Double:
                                            val = (int)*(double*)&reg1->Value;
                                            break;
                                        case ObjectTypes.Integer:
                                            val = reg1->Value;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                    reg2->ObjectType = ObjectTypes.Integer;
                                    reg2->Value = val;
                                }
                                break;
                            case OpCodeREnum.Conv_I8:
                            case OpCodeREnum.Conv_Ovf_I8:
                            case OpCodeREnum.Conv_Ovf_I8_Un:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register1);
                                    long val;
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Integer:
                                            val = reg1->Value;
                                            break;
                                        case ObjectTypes.Long:
                                            ip++;
                                            continue;
                                        case ObjectTypes.Float:
                                            val = (long)*(float*)&reg1->Value;
                                            break;
                                        case ObjectTypes.Double:
                                            val = (long)*(double*)&reg1->Value;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                    reg2->ObjectType = ObjectTypes.Long;
                                    *(long*)(&reg2->Value) = val;
                                }
                                break;
                            #endregion

                            #region Load Store
                            case OpCodeREnum.Move:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    CopyToRegister(ref info, ip->Register1, reg1);
                                }
                                break;

                            case OpCodeREnum.Push:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    CopyToStack(esp, reg1, mStack);
                                    esp++;
                                }
                                break;
                            case OpCodeREnum.Ldloca:
                            case OpCodeREnum.Ldloca_S:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register1);

                                    reg2->ObjectType = ObjectTypes.StackObjectReference;
                                    *(StackObject**)&reg2->Value = reg1;
                                }
                                break;
                            case OpCodeREnum.Ldind_I:
                            case OpCodeREnum.Ldind_I1:
                            case OpCodeREnum.Ldind_I2:
                            case OpCodeREnum.Ldind_I4:
                            case OpCodeREnum.Ldind_U1:
                            case OpCodeREnum.Ldind_U2:
                            case OpCodeREnum.Ldind_U4:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register1);
                                    var val = GetObjectAndResolveReference(reg1);
                                    switch (val->ObjectType)
                                    {
                                        case ObjectTypes.FieldReference:
                                            {
                                                var instance = mStack[val->Value];
                                                var idx = val->ValueLow;
                                                //Free(dst);
                                                //LoadFromFieldReference(instance, idx, reg2, mStack);
                                                throw new NotImplementedException();
                                            }
                                            break;
                                        case ObjectTypes.ArrayReference:
                                            {
                                                var instance = mStack[val->Value];
                                                var idx = val->ValueLow;
                                                //Free(dst);
                                                //LoadFromArrayReference(instance, idx, dst, instance.GetType().GetElementType(), mStack);

                                                throw new NotImplementedException();
                                            }
                                            break;
                                        default:
                                            {
                                                reg2->ObjectType = ObjectTypes.Integer;
                                                reg2->Value = val->Value;
                                                reg2->ValueLow = 0;
                                            }
                                            break;
                                    }
                                }
                                break;
                            case OpCodeREnum.Stind_I:
                            case OpCodeREnum.Stind_I1:
                            case OpCodeREnum.Stind_I2:
                            case OpCodeREnum.Stind_I4:
                            case OpCodeREnum.Stind_R4:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register1);
                                    var dst = GetObjectAndResolveReference(reg2);
                                    switch (dst->ObjectType)
                                    {
                                        case ObjectTypes.FieldReference:
                                            {
                                                //StoreValueToFieldReference(mStack[dst->Value], dst->ValueLow, val, mStack);
                                                throw new NotImplementedException();
                                            }
                                            break;
                                        case ObjectTypes.ArrayReference:
                                            {
                                                //StoreValueToArrayReference(dst, val, mStack[dst->Value].GetType().GetElementType(), mStack);
                                                throw new NotImplementedException();
                                            }
                                            break;
                                        default:
                                            {
                                                dst->Value = reg1->Value;
                                            }
                                            break;
                                    }
                                    /*Free(esp - 1);
                                    Free(esp - 1 - 1);
                                    esp = esp - 1 - 1;*/
                                }
                                break;
                            #endregion

                            #region Control Flow
                            case OpCodeREnum.Ret:
                                if (hasReturn)
                                {
                                    reg1 = Add(r, ip->Register1);
                                    CopyToStack(esp, reg1, mStack);
                                    esp++;
                                }
                                returned = true;
                                break;
                            case OpCodeREnum.Br_S:
                            case OpCodeREnum.Br:
                                ip = ptr + ip->Operand;
                                continue;
                            case OpCodeREnum.Brtrue:
                            case OpCodeREnum.Brtrue_S:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    bool res = false;
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Integer:
                                            res = reg1->Value != 0;
                                            break;
                                        case ObjectTypes.Long:
                                            res = *(long*)&reg1->Value != 0;
                                            break;
                                        case ObjectTypes.Object:
                                            res = mStack[reg1->Value] != null;
                                            break;
                                    }
                                    if (res)
                                    {
                                        ip = ptr + ip->Operand;
                                        continue;
                                    }
                                }
                                break;
                            case OpCodeREnum.Brfalse:
                            case OpCodeREnum.Brfalse_S:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    bool res = false;
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Null:
                                            res = true;
                                            break;
                                        case ObjectTypes.Integer:
                                            res = reg1->Value == 0;
                                            break;
                                        case ObjectTypes.Long:
                                            res = *(long*)&reg1->Value == 0;
                                            break;
                                        case ObjectTypes.Object:
                                            res = mStack[reg1->Value] == null;
                                            break;
                                    }
                                    if (res)
                                    {
                                        ip = ptr + ip->Operand;
                                        continue;
                                    }
                                }
                                break;
                            case OpCodeREnum.Blt:
                            case OpCodeREnum.Blt_S:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg2 = Add(r, ip->Register2);
                                    bool transfer = false;
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Integer:
                                            transfer = reg1->Value < reg2->Value;
                                            break;
                                        case ObjectTypes.Long:
                                            transfer = *(long*)&reg1->Value < *(long*)&reg2->Value;
                                            break;
                                        case ObjectTypes.Float:
                                            transfer = *(float*)&reg1->Value < *(float*)&reg2->Value;
                                            break;
                                        case ObjectTypes.Double:
                                            transfer = *(double*)&reg1->Value < *(double*)&reg2->Value;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }

                                    if (transfer)
                                    {
                                        ip = ptr + ip->Operand;
                                        continue;
                                    }

                                }
                                break;
                            case OpCodeREnum.Bge:
                            case OpCodeREnum.Bge_S:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg2 = Add(r, ip->Register2);
                                    bool transfer = false;
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Integer:
                                            transfer = reg1->Value >= reg2->Value;
                                            break;
                                        case ObjectTypes.Long:
                                            transfer = *(long*)&reg1->Value >= *(long*)&reg2->Value;
                                            break;
                                        case ObjectTypes.Float:
                                            transfer = *(float*)&reg1->Value >= *(float*)&reg2->Value;
                                            break;
                                        case ObjectTypes.Double:
                                            transfer = *(double*)&reg1->Value >= *(double*)&reg2->Value;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }

                                    if (transfer)
                                    {
                                        ip = ptr + ip->Operand;
                                        continue;
                                    }

                                }
                                break;
                            case OpCodeREnum.Call:
                            case OpCodeREnum.Callvirt:
                                {
                                    IMethod m = domain.GetMethod(ip->Operand);
                                    if (m == null)
                                    {
                                        //Irrelevant method
                                        int cnt = (int)ip->OperandLong;
                                        //Balance the stack
                                        for (int i = 0; i < cnt; i++)
                                        {
                                            Free(esp - 1);
                                            esp--;
                                        }
                                    }
                                    else
                                    {
                                        if (m is ILMethod)
                                        {
                                            ILMethod ilm = (ILMethod)m;
                                            bool processed = false;
                                            if (m.IsDelegateInvoke)
                                            {
                                                var instance = StackObject.ToObject((Minus(esp, m.ParameterCount + 1)), domain, mStack);
                                                if (instance is IDelegateAdapter)
                                                {
                                                    esp = ((IDelegateAdapter)instance).ILInvoke(this, esp, mStack);
                                                    processed = true;
                                                }
                                            }
                                            if (!processed)
                                            {
                                                if (code == OpCodeREnum.Callvirt)
                                                {
                                                    objRef = GetObjectAndResolveReference(Minus(esp, ilm.ParameterCount + 1));
                                                    if (objRef->ObjectType == ObjectTypes.Null)
                                                        throw new NullReferenceException();
                                                    if (objRef->ObjectType == ObjectTypes.ValueTypeObjectReference)
                                                    {
                                                        StackObject* dst = *(StackObject**)&objRef->Value;
                                                        var ft = domain.GetType(dst->Value) as ILType;
                                                        ilm = ft.GetVirtualMethod(ilm) as ILMethod;
                                                    }
                                                    else
                                                    {
                                                        var obj = mStack[objRef->Value];
                                                        if (obj == null)
                                                            throw new NullReferenceException();
                                                        ilm = ((ILTypeInstance)obj).Type.GetVirtualMethod(ilm) as ILMethod;
                                                    }
                                                }
                                                esp = ExecuteR(ilm, esp, out unhandledException);
                                                ValueTypeBasePointer = bp;
                                                if (unhandledException)
                                                    returned = true;
                                            }
                                        }
                                        else
                                        {
                                            CLRMethod cm = (CLRMethod)m;
                                            bool processed = false;
                                            if (cm.IsDelegateInvoke)
                                            {
                                                var instance = StackObject.ToObject((Minus(esp, cm.ParameterCount + 1)), domain, mStack);
                                                if (instance is IDelegateAdapter)
                                                {
                                                    esp = ((IDelegateAdapter)instance).ILInvoke(this, esp, mStack);
                                                    processed = true;
                                                }
                                            }

                                            if (!processed)
                                            {
                                                var redirect = cm.Redirection;
                                                if (redirect != null)
                                                    esp = redirect(this, esp, mStack, cm, false);
                                                else
                                                {
#if DEBUG && !DISABLE_ILRUNTIME_DEBUG
                                                    if (!allowUnboundCLRMethod)
                                                        throw new NotSupportedException(cm.ToString() + " is not bound!");
#endif
#if UNITY_EDITOR
                                                    if (System.Threading.Thread.CurrentThread.ManagedThreadId == AppDomain.UnityMainThreadID)

#if UNITY_5_5_OR_NEWER
                                                        UnityEngine.Profiling.Profiler.BeginSample(cm.ToString());
#else
                                                        UnityEngine.Profiler.BeginSample(cm.ToString());
#endif
#endif
                                                    object result = cm.Invoke(this, esp, mStack);
#if UNITY_EDITOR
                                                    if (System.Threading.Thread.CurrentThread.ManagedThreadId == AppDomain.UnityMainThreadID)
#if UNITY_5_5_OR_NEWER
                                                        UnityEngine.Profiling.Profiler.EndSample();
#else
                                                        UnityEngine.Profiler.EndSample();
#endif

#endif
                                                    if (result is CrossBindingAdaptorType)
                                                        result = ((CrossBindingAdaptorType)result).ILInstance;
                                                    int paramCount = cm.ParameterCount;
                                                    for (int i = 1; i <= paramCount; i++)
                                                    {
                                                        Free(Minus(esp, i));
                                                    }
                                                    esp = Minus(esp, paramCount);
                                                    if (cm.HasThis)
                                                    {
                                                        Free(esp - 1);
                                                        esp--;
                                                    }
                                                    if (cm.ReturnType != AppDomain.VoidType && !cm.IsConstructor)
                                                    {
                                                        esp = PushObject(esp, mStack, result, cm.ReturnType.TypeForCLR == typeof(object));
                                                    }
                                                }
                                            }
                                        }

                                        if (m.ReturnType != AppDomain.VoidType)
                                        {
                                            esp = PopToRegister(ref info, ip->Register1, esp);
                                        }
                                    }
                                }
                                break;
                            #endregion

                            #region FieldOperation
                            case OpCodeREnum.Stfld:
                                {
                                    reg2 = Add(r, ip->Register2); 
                                    objRef = GetObjectAndResolveReference(Add(r, ip->Register1));
                                    if (objRef->ObjectType == ObjectTypes.ValueTypeObjectReference)
                                    {
                                        StackObject* dst = *(StackObject**)&objRef->Value;
                                        var ft = domain.GetType(dst->Value);
                                        if (ft is ILType)
                                            CopyToValueTypeField(dst, (int)ip->OperandLong, reg2, mStack);
                                        else
                                            CopyToValueTypeField(dst, ((CLRType)ft).FieldIndexMapping[(int)ip->OperandLong], reg2, mStack);
                                    }
                                    else
                                    {
                                        object obj = RetriveObject(objRef, mStack);

                                        if (obj != null)
                                        {
                                            if (obj is ILTypeInstance)
                                            {
                                                ILTypeInstance instance = obj as ILTypeInstance;
                                                instance.AssignFromStack((int)ip->OperandLong, reg2, AppDomain, mStack);
                                            }
                                            else
                                            {
                                                var t = obj.GetType();
                                                var type = AppDomain.GetType((int)(ip->OperandLong >> 32));
                                                if (type != null)
                                                {
                                                    var fieldToken = (int)ip->OperandLong;
                                                    var f = ((CLRType)type).GetField(fieldToken);
                                                    ((CLRType)type).SetFieldValue(fieldToken, ref obj, f.FieldType.CheckCLRTypes(CheckAndCloneValueType(StackObject.ToObject(reg2, domain, mStack), domain)));
                                                    //Writeback
                                                    if (t.IsValueType)
                                                    {
                                                        switch (objRef->ObjectType)
                                                        {
                                                            case ObjectTypes.Object:
                                                                break;
                                                            case ObjectTypes.FieldReference:
                                                                {
                                                                    var oldObj = mStack[objRef->Value];
                                                                    int idx = objRef->ValueLow;
                                                                    if (oldObj is ILTypeInstance)
                                                                    {
                                                                        ((ILTypeInstance)oldObj)[idx] = obj;
                                                                    }
                                                                    else
                                                                    {
                                                                        var it = AppDomain.GetType(oldObj.GetType());
                                                                        ((CLRType)it).SetFieldValue(idx, ref oldObj, obj);
                                                                    }
                                                                }
                                                                break;
                                                            case ObjectTypes.StaticFieldReference:
                                                                {
                                                                    var it = AppDomain.GetType(objRef->Value);
                                                                    int idx = objRef->ValueLow;
                                                                    if (it is ILType)
                                                                    {
                                                                        ((ILType)it).StaticInstance[idx] = obj;
                                                                    }
                                                                    else
                                                                    {
                                                                        ((CLRType)it).SetStaticFieldValue(idx, obj);
                                                                    }
                                                                }
                                                                break;
                                                            case ObjectTypes.ValueTypeObjectReference:
                                                                {
                                                                    var dst = *(StackObject**)&objRef->Value;
                                                                    var ct = domain.GetType(dst->Value) as CLRType;
                                                                    var binder = ct.ValueTypeBinder;
                                                                    binder.CopyValueTypeToStack(obj, dst, mStack);
                                                                }
                                                                break;
                                                            default:
                                                                throw new NotImplementedException();
                                                        }
                                                    }
                                                }
                                                else
                                                    throw new TypeLoadException();
                                            }
                                        }
                                        else
                                            throw new NullReferenceException();
                                    }
                                }
                                break;
                            case OpCodeREnum.Ldfld:
                                {
                                    reg2 = Add(r, ip->Register2);
                                    objRef = GetObjectAndResolveReference(reg2);
                                    if (objRef->ObjectType == ObjectTypes.ValueTypeObjectReference)
                                    {
                                        var dst = *(StackObject**)&objRef->Value;
                                        var ft = domain.GetType(dst->Value);
                                        if (ft is ILType)
                                            dst = Minus(dst, (int)ip->OperandLong + 1);
                                        else
                                            dst = Minus(dst, ((CLRType)ft).FieldIndexMapping[(int)ip->OperandLong] + 1);

                                        CopyToRegister(ref info, ip->Register1, dst);
                                    }
                                    else
                                    {
                                        object obj = RetriveObject(objRef, mStack);
                                        if (obj != null)
                                        {
                                            if (obj is ILTypeInstance)
                                            {
                                                ILTypeInstance instance = obj as ILTypeInstance;
                                                instance.CopyToRegister((int)ip->OperandLong, ref info, ip->Register1);
                                            }
                                            else
                                            {
                                                //var t = obj.GetType();
                                                var type = AppDomain.GetType((int)(ip->OperandLong >> 32));
                                                if (type != null)
                                                {
                                                    var token = (int)ip->OperandLong;
                                                    var ft = ((CLRType)type).GetField(token);
                                                    var val = ((CLRType)type).GetFieldValue(token, obj);
                                                    if (val is CrossBindingAdaptorType)
                                                        val = ((CrossBindingAdaptorType)val).ILInstance;
                                                    AssignToRegister(ref info, ip->Register1, val, ft.FieldType == typeof(object));
                                                }
                                                else
                                                    throw new TypeLoadException();
                                            }
                                        }
                                        else
                                            throw new NullReferenceException();
                                    }
                                }
                                break;
                            case OpCodeREnum.Ldflda:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg2 = Add(r, ip->Register2);
                                    objRef = GetObjectAndResolveReference(reg2);
                                    if (objRef->ObjectType == ObjectTypes.ValueTypeObjectReference)
                                    {
                                        var ft = domain.GetType((int)(ip->OperandLong >> 32));
                                        StackObject* fieldAddr;
                                        if (ft is ILType)
                                        {
                                            fieldAddr = Minus(*(StackObject**)&objRef->Value, (int)ip->OperandLong + 1);
                                        }
                                        else
                                        {
                                            fieldAddr = Minus(*(StackObject**)&objRef->Value, ((CLRType)ft).FieldIndexMapping[(int)ip->OperandLong] + 1);
                                        }
                                        reg1->ObjectType = ObjectTypes.StackObjectReference;
                                        *(StackObject**)&reg1->Value = fieldAddr;
                                    }
                                    else
                                    {
                                        object obj = RetriveObject(objRef, mStack);
                                        if (obj != null)
                                        {
                                            AssignToRegister(ref info, ip->Register1, obj);
                                            reg1->ObjectType = ObjectTypes.FieldReference;
                                            reg1->ValueLow = (int)ip->OperandLong;
                                        }
                                        else
                                            throw new NullReferenceException();
                                    }
                                }
                                break;
                            #endregion

                            #region Initialization & Instantiation
                            case OpCodeREnum.Newobj:
                                {
                                    IMethod m = domain.GetMethod(ip->Operand);
                                    if (m is ILMethod)
                                    {
                                        ILType type = m.DeclearingType as ILType;
                                        if (type.IsDelegate)
                                        {
                                            objRef = GetObjectAndResolveReference(esp - 1 - 1);
                                            var mi = (IMethod)mStack[(esp - 1)->Value];
                                            object ins;
                                            if (objRef->ObjectType == ObjectTypes.Null)
                                                ins = null;
                                            else
                                                ins = mStack[objRef->Value];
                                            Free(esp - 1);
                                            Free(esp - 1 - 1);
                                            esp = esp - 1 - 1;
                                            object dele;
                                            if (mi is ILMethod)
                                            {
                                                if (ins != null)
                                                {
                                                    dele = ((ILTypeInstance)ins).GetDelegateAdapter((ILMethod)mi);
                                                    if (dele == null)
                                                        dele = domain.DelegateManager.FindDelegateAdapter((ILTypeInstance)ins, (ILMethod)mi);
                                                }
                                                else
                                                {
                                                    if (((ILMethod)mi).DelegateAdapter == null)
                                                    {
                                                        ((ILMethod)mi).DelegateAdapter = domain.DelegateManager.FindDelegateAdapter(null, (ILMethod)mi);
                                                    }
                                                    dele = ((ILMethod)mi).DelegateAdapter;
                                                }
                                            }

                                            else
                                            {
                                                throw new NotImplementedException();
                                            }
                                            esp = PushObject(esp, mStack, dele);
                                        }
                                        else
                                        {
                                            var a = esp - m.ParameterCount;
                                            ILTypeInstance obj = null;
                                            bool isValueType = type.IsValueType;
                                            if (isValueType)
                                            {
                                                throw new NotImplementedException();
                                                stack.AllocValueType(esp, type);
                                                objRef = esp + 1;
                                                objRef->ObjectType = ObjectTypes.StackObjectReference;
                                                *(StackObject**)&objRef->Value = esp;
                                                objRef++;
                                            }
                                            else
                                            {
                                                obj = type.Instantiate(false);
                                                objRef = PushObject(esp, mStack, obj);//this parameter for constructor
                                            }
                                            esp = objRef;
                                            for (int i = 0; i < m.ParameterCount; i++)
                                            {
                                                CopyToStack(esp, a + i, mStack);
                                                esp++;
                                            }
                                            esp = ExecuteR((ILMethod)m, esp, out unhandledException);
                                            ValueTypeBasePointer = bp;
                                            if (isValueType)
                                            {
                                                var ins = objRef - 1 - 1;
                                                *a = *ins;
                                                esp = a + 1;
                                            }
                                            else
                                            {
                                                //PushToRegister(ref info, ip->Register1, obj);
                                                esp = PushObject(a, mStack, obj);//new constructedObj
                                            }
                                        }
                                        if (unhandledException)
                                            returned = true;
                                    }
                                    else
                                    {
                                        CLRMethod cm = (CLRMethod)m;
                                        //Means new object();
                                        if (cm == null)
                                        {
                                            esp = PushObject(esp, mStack, new object());
                                        }
                                        else
                                        {
                                            if (cm.DeclearingType.IsDelegate)
                                            {
                                                throw new NotImplementedException();
                                                objRef = GetObjectAndResolveReference(esp - 1 - 1);
                                                var mi = (IMethod)mStack[(esp - 1)->Value];
                                                object ins;
                                                if (objRef->ObjectType == ObjectTypes.Null)
                                                    ins = null;
                                                else
                                                    ins = mStack[objRef->Value];
                                                Free(esp - 1);
                                                Free(esp - 1 - 1);
                                                esp = esp - 1 - 1;
                                                object dele;
                                                if (mi is ILMethod)
                                                {
                                                    if (ins != null)
                                                    {
                                                        dele = ((ILTypeInstance)ins).GetDelegateAdapter((ILMethod)mi);
                                                        if (dele == null)
                                                            dele = domain.DelegateManager.FindDelegateAdapter((ILTypeInstance)ins, (ILMethod)mi);
                                                    }
                                                    else
                                                    {
                                                        if (((ILMethod)mi).DelegateAdapter == null)
                                                        {
                                                            ((ILMethod)mi).DelegateAdapter = domain.DelegateManager.FindDelegateAdapter(null, (ILMethod)mi);
                                                        }
                                                        dele = ((ILMethod)mi).DelegateAdapter;
                                                    }
                                                }
                                                else
                                                {
                                                    if (ins is ILTypeInstance)
                                                        ins = ((ILTypeInstance)ins).CLRInstance;
                                                    dele = Delegate.CreateDelegate(cm.DeclearingType.TypeForCLR, ins, ((CLRMethod)mi).MethodInfo);
                                                }
                                                esp = PushObject(esp, mStack, dele);
                                            }
                                            else
                                            {
                                                var redirect = cm.Redirection;
                                                if (redirect != null)
                                                    esp = redirect(this, esp, mStack, cm, true);
                                                else
                                                {
                                                    object result = cm.Invoke(this, esp, mStack, true);
                                                    int paramCount = cm.ParameterCount;
                                                    for (int i = 1; i <= paramCount; i++)
                                                    {
                                                        Free(esp - i);
                                                    }
                                                    esp = Minus(esp, paramCount);
                                                    esp = PushObject(esp, mStack, result);//new constructedObj
                                                }
                                            }
                                        }
                                    }
                                    esp = PopToRegister(ref info, ip->Register1, esp);
                                }
                                break;
                            case OpCodeREnum.Box:
                                {
                                    var obj = Add(r, ip->Register2);
                                    var type = domain.GetType(ip->Operand);
                                    if (type != null)
                                    {
                                        if (type is ILType)
                                        {
                                            if (((ILType)type).IsEnum)
                                            {
                                                ILEnumTypeInstance ins = new Intepreter.ILEnumTypeInstance((ILType)type);
                                                ins.AssignFromStack(0, obj, AppDomain, mStack);
                                                ins.Boxed = true;
                                                esp = PushObject(esp, mStack, ins, true);
                                            }
                                            else
                                            {
                                                switch (obj->ObjectType)
                                                {
                                                    case ObjectTypes.Null:
                                                        break;
                                                    case ObjectTypes.ValueTypeObjectReference:
                                                        {
                                                            ILTypeInstance ins = ((ILType)type).Instantiate(false);
                                                            ins.AssignFromStack(obj, domain, mStack);
                                                            //FreeStackValueType(obj);
                                                            esp = PushObject(esp, mStack, ins, true);
                                                        }
                                                        break;
                                                    default:
                                                        {
                                                            var val = mStack[obj->Value];
                                                            //Free(obj);
                                                            if (type.IsArray)
                                                            {
                                                                esp = PushObject(esp, mStack, val, true);
                                                            }
                                                            else
                                                            {
                                                                ILTypeInstance ins = (ILTypeInstance)val;
                                                                if (ins != null)
                                                                {
                                                                    if (ins.IsValueType)
                                                                    {
                                                                        ins.Boxed = true;
                                                                    }
                                                                    esp = PushObject(esp, mStack, ins, true);
                                                                }
                                                                else
                                                                {
                                                                    esp = PushNull(esp);
                                                                }
                                                            }
                                                        }
                                                        break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (type.TypeForCLR.IsPrimitive)
                                            {
                                                var t = type.TypeForCLR;
                                                if (t == typeof(int))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(esp, mStack, obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, 0, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(bool))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(esp, mStack, (obj->Value == 1), true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, false, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(byte))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(esp, mStack, (byte)obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, 0L, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(short))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(obj, mStack, (short)obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(obj, mStack, (short)0, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(long))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Long:
                                                            esp = PushObject(esp, mStack, *(long*)&obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, 0L, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(float))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Float:
                                                            esp = PushObject(esp, mStack, *(float*)&obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, 0f, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(double))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Double:
                                                            esp = PushObject(esp, mStack, *(double*)&obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, 0.0, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(char))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(esp, mStack, (char)obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(uint))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(esp, mStack, (uint)obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, (uint)0, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(ushort))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(esp, mStack, (ushort)obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, (ushort)0, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(ulong))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Long:
                                                            esp = PushObject(esp, mStack, *(ulong*)&obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, (ulong)0, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else if (t == typeof(sbyte))
                                                {
                                                    switch (obj->ObjectType)
                                                    {
                                                        case ObjectTypes.Integer:
                                                            esp = PushObject(esp, mStack, (sbyte)obj->Value, true);
                                                            break;
                                                        case ObjectTypes.Null:
                                                            esp = PushObject(esp, mStack, (sbyte)0, true);
                                                            break;
                                                        case ObjectTypes.Object:
                                                            break;
                                                        default:
                                                            throw new NotImplementedException();
                                                    }
                                                }
                                                else
                                                    throw new NotImplementedException();
                                            }
                                            else if (type.TypeForCLR.IsEnum)
                                            {
                                                esp = PushObject(esp, mStack, Enum.ToObject(type.TypeForCLR, StackObject.ToObject(obj, AppDomain, mStack)), true);
                                            }
                                            else
                                            {
                                                if (obj->ObjectType == ObjectTypes.ValueTypeObjectReference)
                                                {
                                                    var dst = *(StackObject**)&obj->Value;
                                                    var vt = domain.GetType(dst->Value);
                                                    if (vt != type)
                                                        throw new InvalidCastException();
                                                    object ins = ((CLRType)vt).ValueTypeBinder.ToObject(dst, mStack);
                                                    //FreeStackValueType(obj);
                                                    esp = PushObject(esp, mStack, ins, true);
                                                }
                                                //nothing to do for CLR type boxing
                                            }
                                        }
                                    }
                                    else
                                        throw new NullReferenceException();

                                    esp = PopToRegister(ref info, ip->Register1, esp);
                                }
                                break;
                            #endregion

                            #region Compare
                            case OpCodeREnum.Clt:
                                {
                                    reg1 = Add(r, ip->Register2);
                                    reg2 = Add(r, ip->Register3);
                                    reg3 = Add(r, ip->Register1);
                                    bool res = false;
                                    switch (reg1->ObjectType)
                                    {
                                        case ObjectTypes.Integer:
                                            res = reg1->Value < reg2->Value;
                                            break;
                                        case ObjectTypes.Long:
                                            res = *(long*)&reg1->Value < *(long*)&reg2->Value;
                                            break;
                                        case ObjectTypes.Float:
                                            res = *(float*)&reg1->Value < *(float*)&reg2->Value;
                                            break;
                                        case ObjectTypes.Double:
                                            res = *(double*)&reg1->Value < *(double*)&reg2->Value;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                    if (res)
                                        WriteOne(reg3);
                                    else
                                        WriteZero(reg3);
                                }
                                break;
                            #endregion

                            #region Array
                            case OpCodeREnum.Newarr:
                                {
                                    reg2 = Add(r, ip->Register2);
                                    var type = domain.GetType(ip->Operand);
                                    object arr = null;
                                    if (type != null)
                                    {
                                        if (type.TypeForCLR != typeof(ILTypeInstance))
                                        {
                                            if (type is CLRType)
                                            {
                                                arr = ((CLRType)type).CreateArrayInstance(reg2->Value);
                                            }
                                            else
                                            {
                                                arr = Array.CreateInstance(type.TypeForCLR, reg2->Value);
                                            }

                                            //Register Type
                                            AppDomain.GetType(arr.GetType());
                                        }
                                        else
                                        {
                                            arr = new ILTypeInstance[reg2->Value];
                                            ILTypeInstance[] ilArr = (ILTypeInstance[])arr;
                                            if (type.IsValueType)
                                            {
                                                for (int i = 0; i < reg2->Value; i++)
                                                {
                                                    ilArr[i] = ((ILType)type).Instantiate(true);
                                                }
                                            }
                                        }
                                    }
                                    AssignToRegister(ref info, ip->Register1, arr);
                                }
                                break;
                            case OpCodeREnum.Stelem_I4:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg2 = Add(r, ip->Register2);
                                    reg3 = Add(r, ip->Register3);

                                    int[] arr = mStack[reg1->Value] as int[];
                                    if (arr != null)
                                    {
                                        arr[reg2->Value] = reg3->Value;
                                    }
                                    else
                                    {
                                        uint[] arr2 = mStack[reg1->Value] as uint[];
                                        arr2[reg2->Value] = (uint)reg3->Value;
                                    }
                                }
                                break;
                            case OpCodeREnum.Ldlen:
                                {
                                    reg1 = Add(r, ip->Register1);
                                    reg2 = Add(r, ip->Register2);
                                    Array arr = mStack[reg2->Value] as Array;

                                    reg1->ObjectType = ObjectTypes.Integer;
                                    reg1->Value = arr.Length;
                                }
                                break;
                            #endregion

                            default:
                                throw new NotSupportedException("Not supported opcode " + code);
                        }
                        ip++;
                    }
                    catch (Exception ex)
                    {
                        if (method.ExceptionHandler != null)
                        {
                            int addr = (int)(ip - ptr);
                            var eh = GetCorrespondingExceptionHandler(method, ex, addr, ExceptionHandlerType.Catch, true);

                            if (eh == null)
                            {
                                eh = GetCorrespondingExceptionHandler(method, ex, addr, ExceptionHandlerType.Catch, false);
                            }
                            if (eh != null)
                            {
                                if (ex is ILRuntimeException)
                                {
                                    ILRuntimeException ire = (ILRuntimeException)ex;
                                    var inner = ire.InnerException;
                                    inner.Data["ThisInfo"] = ire.ThisInfo;
                                    inner.Data["StackTrace"] = ire.StackTrace;
                                    inner.Data["LocalInfo"] = ire.LocalInfo;
                                    ex = inner;
                                }
                                else
                                {
                                    var debugger = AppDomain.DebugService;
                                    if (method.HasThis)
                                        ex.Data["ThisInfo"] = debugger.GetThisInfo(this);
                                    else
                                        ex.Data["ThisInfo"] = "";
                                    ex.Data["StackTrace"] = debugger.GetStackTrance(this);
                                    ex.Data["LocalInfo"] = debugger.GetLocalVariableInfo(this);
                                }
                                //Clear call stack
                                while (stack.Frames.Peek().BasePointer != frame.BasePointer)
                                {
                                    var f = stack.Frames.Peek();
                                    esp = stack.PopFrame(ref f, esp);
                                    if (f.Method.ReturnType != AppDomain.VoidType)
                                    {
                                        Free(esp - 1);
                                        esp--;
                                    }
                                }
                                esp = PushObject(esp, mStack, ex);
                                unhandledException = false;
                                ip = ptr + eh.HandlerStart;
                                continue;
                            }
                        }
                        if (unhandledException)
                        {
                            throw ex;
                        }

                        unhandledException = true;
                        returned = true;
#if DEBUG && !DISABLE_ILRUNTIME_DEBUG
                        if (!AppDomain.DebugService.Break(this, ex))
#endif
                        {
                            var newEx = new ILRuntimeException(ex.Message, this, method, ex);
                            throw newEx;
                        }
                    }
                }
            }

#if UNITY_EDITOR
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == AppDomain.UnityMainThreadID)
#if UNITY_5_5_OR_NEWER
                UnityEngine.Profiling.Profiler.EndSample();
#else
                UnityEngine.Profiler.EndSample();
#endif
#endif
            //ClearStack
            return stack.PopFrame(ref frame, esp);
        }

        internal void CopyToRegister(ref RegisterFrameInfo info, short reg, StackObject* val, IList<object> mStackSrc = null)
        {
            var argCnt = info.ParameterCount;
            var mStack = info.ManagedStack;
            var locCnt = info.LocalCount;
            if (mStackSrc == null)
                mStackSrc = mStack;

            if (reg < argCnt)
                throw new NotImplementedException();
            var v = Add(info.RegisterStart, reg);
            var idx = info.LocalManagedBase + (reg - argCnt);
                
            switch (val->ObjectType)
            {
                case ObjectTypes.Null:
                    v->ObjectType = ObjectTypes.Object;
                    v->Value = idx;
                    mStack[idx] = null;
                    break;
                case ObjectTypes.Object:
                case ObjectTypes.FieldReference:
                case ObjectTypes.ArrayReference:
                    if (v->ObjectType == ObjectTypes.ValueTypeObjectReference)
                    {
                        var obj = mStackSrc[val->Value];
                        if (obj is ILTypeInstance)
                        {
                            var dst = *(StackObject**)&v->Value;
                            ((ILTypeInstance)obj).CopyValueTypeToStack(dst, mStack);
                        }
                        else
                        {
                            var dst = *(StackObject**)&v->Value;
                            var ct = domain.GetType(dst->Value) as CLRType;
                            var binder = ct.ValueTypeBinder;
                            binder.CopyValueTypeToStack(obj, dst, mStack);
                        }
                    }
                    else
                    {
                        *v = *val;
                        mStack[idx] = CheckAndCloneValueType(mStackSrc[v->Value], domain);
                        v->Value = idx;
                    }
                    break;
                case ObjectTypes.ValueTypeObjectReference:
                    if (v->ObjectType == ObjectTypes.ValueTypeObjectReference)
                    {
                        CopyStackValueType(val, v, mStack);
                    }
                    else
                        throw new NotImplementedException();
                    FreeStackValueType(val);
                    break;
                default:
                    *v = *val;
                    mStack[idx] = null;
                    break;
            }
        }

        internal static void AssignToRegister(ref RegisterFrameInfo info, short reg, object obj, bool isBox = false)
        {
            var argCnt = info.ParameterCount;
            var mStack = info.ManagedStack;
            var locCnt = info.LocalCount;

            if (reg < argCnt)
                throw new NotImplementedException();
            var dst = Add(info.RegisterStart, reg);
            if (obj != null)
            {
                var idx = info.LocalManagedBase + (reg - argCnt);

                if (!isBox)
                {
                    var typeFlags = obj.GetType().GetTypeFlags();

                    if ((typeFlags & CLR.Utils.Extensions.TypeFlags.IsPrimitive) != 0)
                    {
                        UnboxObject(dst, obj, mStack);
                    }
                    else if ((typeFlags & CLR.Utils.Extensions.TypeFlags.IsEnum) != 0)
                    {
                        dst->ObjectType = ObjectTypes.Integer;
                        dst->Value = Convert.ToInt32(obj);
                    }
                    else
                    {
                        dst->ObjectType = ObjectTypes.Object;
                        dst->Value = idx;
                        mStack[idx] = obj;
                    }
                }
                else
                {
                    dst->ObjectType = ObjectTypes.Object;
                    dst->Value = idx;
                    mStack[idx] = obj;
                }
            }
            else
            {
                PushNull(dst);
            }
        }

        StackObject* PopToRegister(ref RegisterFrameInfo info, short reg, StackObject* esp)
        {
            var val = esp - 1;
            CopyToRegister(ref info, reg, val);
            Free(val);
            return val;
        }

        public static void WriteOne(StackObject* esp)
        {
            esp->ObjectType = ObjectTypes.Integer;
            esp->Value = 1;
        }

        public static void WriteZero(StackObject* esp)
        {
            esp->ObjectType = ObjectTypes.Integer;
            esp->Value = 0;
        }

        public static void WriteNull(StackObject* esp)
        {
            esp->ObjectType = ObjectTypes.Null;
            esp->Value = -1;
            esp->ValueLow = 0;
        }
    }
}