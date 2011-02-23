﻿// 
//  Copyright 2010  Ekon Benefits
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

namespace ImpromptuInterface
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.CompilerServices;
    using Microsoft.CSharp.RuntimeBinder;
    using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

    ///<summary>
    /// Extension Methods that make emiting code easier and easier to read
    ///</summary>
    public static class EmitExtensions
    {
        ///<summary>
        /// Used to automatically create label on dispose
        ///</summary>
        public class BranchTrueOverBlock : IDisposable
        {
            private readonly ILGenerator _generator;
            private readonly Label _label;

            ///<summary>
            /// Constructor
            ///</summary>
            ///<param name="generator"></param>
            public BranchTrueOverBlock(ILGenerator generator)
            {
                _generator = generator;
                _label = generator.DefineLabel();
                _generator.Emit(OpCodes.Brtrue, _label);
            }

            /// <summary>
            /// Finishes block
            /// </summary>
            public void Dispose()
            {
                //_generator.Emit(OpCodes.Br_S, _label);
                _generator.MarkLabel(_label);
            }
        }

        /// <summary>
        /// Gets the field info even if generic type parameter.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns></returns>
        public static FieldInfo GetFieldEvenIfGeneric(this Type type, string fieldName)
        {
            if (type is TypeBuilder || type.GetType().Name.Contains("TypeBuilder"))
            {
                var tGenDef = type.GetGenericTypeDefinition();
                var tField = tGenDef.GetField(fieldName);
                return TypeBuilder.GetField(type, tField);
            }
            return type.GetField(fieldName);
        }



        /// <summary>
        /// Gets the method info even if generic type parameter.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="argTypes">The arg types.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodEvenIfGeneric(this Type type, string methodName, Type[] argTypes)
        {
            if (type is TypeBuilder || type.GetType().Name.Contains("TypeBuilder"))
            {
                var tGenDef = type.GetGenericTypeDefinition();
                var tMethodInfo = tGenDef.GetMethod(methodName, argTypes);
                return TypeBuilder.GetMethod(type, tMethodInfo);
            }
            return type.GetMethod(methodName, argTypes);
        }



        /// <summary>
        /// Gets the method info even if generic type parameter.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodEvenIfGeneric(this Type type, string methodName)
        {
            if (type is TypeBuilder || type.GetType().Name.Contains("TypeBuilder")) 
            {
                var tGenDef = type.GetGenericTypeDefinition();
                var tMethodInfo = tGenDef.GetMethod(methodName);
                return TypeBuilder.GetMethod(type, tMethodInfo);
            }
            return type.GetMethod(methodName);
        }



        /// <summary>
        /// Emits branch true. expects using keyword.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <returns></returns>
        /// <example>
        /// Using keyword allows you to set the emit code you are branching over and then automatically emits label when disposing
        /// <code>
        /// <![CDATA[
        ///       tIlGen.Emit(OpCodes.Ldsfld, tConvertField);
        ///        using (tIlGen.EmitBranchTrue())
        ///       {
        ///            tIlGen.EmitDynamicConvertBinder(CSharpBinderFlags.None, returnType, contextType);
        ///            tIlGen.EmitCallsiteCreate(convertFuncType);
        ///            tIlGen.Emit(OpCodes.Stsfld, tConvertField);
        ///        }
        /// ]]>
        /// </code>
        /// </example>
        public static BranchTrueOverBlock EmitBranchTrue(this ILGenerator generator)
        {
            return new BranchTrueOverBlock(generator);
        }



        /// <summary>
        /// Emits creating the callsite.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="funcType">Type of the func.</param>
        public static void EmitCallsiteCreate(this ILGenerator generator, Type funcType)
        {
            generator.Emit(OpCodes.Call, typeof(CallSite<>).MakeGenericType(funcType).GetMethodEvenIfGeneric("Create", new[] { typeof(CallSiteBinder) }));
        }



        /// <summary>
        /// Emits the call invoke delegate.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="funcType">Type of the func.</param>
        /// <param name="isAction">if set to <c>true</c> [is action].</param>
        public static void EmitCallInvokeFunc(this ILGenerator generator, Type funcType, bool isAction = false)
        {
            generator.Emit(OpCodes.Callvirt, funcType.GetMethodEvenIfGeneric("Invoke"));
        }

        /// <summary>
        /// Emits an array.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="arrayType">Type of the array.</param>
        /// <param name="emitElements">The emit elements.</param>
        public static void EmitArray(this ILGenerator generator, Type arrayType, IList<Action<ILGenerator>> emitElements)
        {
            var tLocal = generator.DeclareLocal(arrayType.MakeArrayType());
            generator.Emit(OpCodes.Ldc_I4, emitElements.Count);
            generator.Emit(OpCodes.Newarr, arrayType);
            generator.EmitStoreLocation(tLocal.LocalIndex);

            for (var i = 0; i < emitElements.Count; i++)
            {
                generator.EmitLoadLocation(tLocal.LocalIndex);
                generator.Emit(OpCodes.Ldc_I4, i);
                emitElements[i](generator);
                generator.Emit(OpCodes.Stelem_Ref);
            }
            generator.EmitLoadLocation(tLocal.LocalIndex);
        }

        /// <summary>
        /// Emits the store location.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="location">The location.</param>
        public static void EmitStoreLocation(this ILGenerator generator, int location)
        {
            switch (location)
            {
                case 0:
                    generator.Emit(OpCodes.Stloc_0);
                    return;
                case 1:
                    generator.Emit(OpCodes.Stloc_1);
                    return;
                case 2:
                    generator.Emit(OpCodes.Stloc_2);
                    return;
                case 3:
                    generator.Emit(OpCodes.Stloc_3);
                    return;
                default:
                    generator.Emit(OpCodes.Stloc, location);
                    return;
            }
        }


        /// <summary>
        /// Emits the load argument.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="location">The location.</param>
        public static void EmitLoadArgument(this ILGenerator generator, int location)
        {
            switch (location)
            {
                case 0:
                    generator.Emit(OpCodes.Ldarg_0);
                    return;
                case 1:
                    generator.Emit(OpCodes.Ldarg_1);
                    return;
                case 2:
                    generator.Emit(OpCodes.Ldarg_2);
                    return;
                case 3:
                    generator.Emit(OpCodes.Ldarg_3);
                    return;
                default:
                    generator.Emit(OpCodes.Ldarg, location);
                    return;
            }
        }

        /// <summary>
        /// Emits the load location.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="location">The location.</param>
        public static void EmitLoadLocation(this ILGenerator generator, int location)
        {
            switch (location)
            {
                case 0:
                    generator.Emit(OpCodes.Ldloc_0);
                    return;
                case 1:
                    generator.Emit(OpCodes.Ldloc_1);
                    return;
                case 2:
                    generator.Emit(OpCodes.Ldloc_2);
                    return;
                case 3:
                    generator.Emit(OpCodes.Ldloc_3);
                    return;
                default:
                    generator.Emit(OpCodes.Ldloc, location);
                    return;
            }
        }


        /// <summary>
        /// Emits the dynamic method invoke binder.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="flag">The binding flags.</param>
        /// <param name="name">The name.</param>
        /// <param name="context">The context.</param>
        /// <param name="argInfo">The arg info.</param>
        public static void EmitDynamicMethodInvokeBinder(this ILGenerator generator, CSharpBinderFlags flag, string name, Type context, ParameterInfo[] argInfo)
        {
            generator.Emit(OpCodes.Ldc_I4, (int)flag);
            generator.Emit(OpCodes.Ldstr, name);
            generator.Emit(OpCodes.Ldnull);
            generator.EmitTypeOf(context);
            var tList = new List<Action<ILGenerator>> { gen => gen.EmitCreateCSharpArgumentInfo(CSharpArgumentInfoFlags.None) };

           

            tList.AddRange(argInfo.Select(arg => (Action<ILGenerator>)(gen =>
                                                                            {
                                                                                var tStart = CSharpArgumentInfoFlags.
                                                                                    UseCompileTimeType;

                                                                                if (arg.IsOut)
                                                                                {
                                                                                    tStart |=
                                                                                        CSharpArgumentInfoFlags.IsOut;
                                                                                }
                                                                                else if(arg.ParameterType.IsByRef)
                                                                                {
                                                                                    tStart |=
                                                                                       CSharpArgumentInfoFlags.IsRef;
                                                                                }

                                                                                gen.EmitCreateCSharpArgumentInfo(tStart);
                                                                                return;
                                                                            })));
            generator.EmitArray(typeof(CSharpArgumentInfo), tList);
            generator.Emit(OpCodes.Call, typeof(Binder).GetMethod("InvokeMember", new[] { typeof(CSharpBinderFlags), typeof(string), typeof(IEnumerable<Type>), typeof(Type), typeof(CSharpArgumentInfo[]) }));
        }



        /// <summary>
        /// Emits the dynamic set binder.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="flag">The binding flags.</param>
        /// <param name="name">The name.</param>
        /// <param name="context">The context.</param>
        /// <param name="argTypes">The arg types.</param>
        public static void EmitDynamicSetBinder(this ILGenerator generator, CSharpBinderFlags flag, string name, Type context, params Type[] argTypes)
        {
            generator.Emit(OpCodes.Ldc_I4, (int)flag);
            if (argTypes.Length == 1)
                generator.Emit(OpCodes.Ldstr, name);
            generator.EmitTypeOf(context);
            var tList = new List<Action<ILGenerator>> { gen => gen.EmitCreateCSharpArgumentInfo(CSharpArgumentInfoFlags.None) };
            tList.AddRange(argTypes.Select(tArg => (Action<ILGenerator>)(gen => gen.EmitCreateCSharpArgumentInfo(CSharpArgumentInfoFlags.UseCompileTimeType))));
            generator.EmitArray(typeof(CSharpArgumentInfo), tList);

            if (argTypes.Length == 1)
                generator.Emit(OpCodes.Call, typeof(Binder).GetMethod("SetMember", new[] { typeof(CSharpBinderFlags), typeof(string), typeof(Type), typeof(CSharpArgumentInfo[]) }));
            else
                generator.Emit(OpCodes.Call, typeof(Binder).GetMethod("SetIndex", new[] { typeof(CSharpBinderFlags), typeof(Type), typeof(CSharpArgumentInfo[]) }));


        }


        /// <summary>
        /// Emits the dynamic get binder.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="flag">The binding flags.</param>
        /// <param name="name">The name.</param>
        /// <param name="context">The context.</param>
        /// <param name="argTypes">The arg types.</param>
        public static void EmitDynamicGetBinder(this ILGenerator generator, CSharpBinderFlags flag, string name, Type context, params Type[] argTypes)
        {
            generator.Emit(OpCodes.Ldc_I4, (int)flag);
            if (!argTypes.Any())
                generator.Emit(OpCodes.Ldstr, name);
            generator.EmitTypeOf(context);
            var tList = new List<Action<ILGenerator>> { gen => gen.EmitCreateCSharpArgumentInfo(CSharpArgumentInfoFlags.None) };
            tList.AddRange(argTypes.Select(tArg => (Action<ILGenerator>)(gen => gen.EmitCreateCSharpArgumentInfo(CSharpArgumentInfoFlags.UseCompileTimeType))));
            generator.EmitArray(typeof(CSharpArgumentInfo), tList);
            if (!argTypes.Any())
                generator.Emit(OpCodes.Call, typeof(Binder).GetMethod("GetMember", new[] { typeof(CSharpBinderFlags), typeof(string), typeof(Type), typeof(CSharpArgumentInfo[]) }));
            else
                generator.Emit(OpCodes.Call, typeof(Binder).GetMethod("GetIndex", new[] { typeof(CSharpBinderFlags), typeof(Type), typeof(CSharpArgumentInfo[]) }));
        }



        /// <summary>
        /// Emits creating the <see cref="CSharpArgumentInfo"></see>
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="flag">The flag.</param>
        /// <param name="name">The name.</param>
        public static void EmitCreateCSharpArgumentInfo(this ILGenerator generator, CSharpArgumentInfoFlags flag, string name = null)
        {
            generator.Emit(OpCodes.Ldc_I4, (int)flag);
            if (String.IsNullOrEmpty(name))
                generator.Emit(OpCodes.Ldnull);
            else
                generator.Emit(OpCodes.Ldstr, name);
            generator.Emit(OpCodes.Call, typeof(CSharpArgumentInfo).GetMethod("Create", new[] { typeof(CSharpArgumentInfoFlags), typeof(string) }));
        }


        /// <summary>
        /// Emits the dynamic convert binder.
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="flag">The binding flag.</param>
        /// <param name="returnType">Type of the return.</param>
        /// <param name="context">The context.</param>
        public static void EmitDynamicConvertBinder(this ILGenerator generator, CSharpBinderFlags flag, Type returnType, Type context)
        {
            generator.Emit(OpCodes.Ldc_I4, (int)flag);
            generator.EmitTypeOf(returnType);
            generator.EmitTypeOf(context);
            generator.Emit(OpCodes.Call, typeof(Binder).GetMethod("Convert", new[] { typeof(CSharpBinderFlags), typeof(Type), typeof(Type) }));
        }


        /// <summary>
        /// Emits the typeof(Type)
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="type">The type.</param>
        public static void EmitTypeOf(this ILGenerator generator, Type type)
        {

            generator.Emit(OpCodes.Ldtoken, type);
            var tTypeMeth = typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) });
            generator.Emit(OpCodes.Call, tTypeMeth);
        }


        /// <summary>
        /// Emits the typeof(Type)
        /// </summary>
        /// <param name="generator">The generator.</param>
        /// <param name="type">The type.</param>
        public static void EmitTypeOf(this ILGenerator generator, TypeToken type)
        {

            generator.Emit(OpCodes.Ldtoken, type.Token);
            var tTypeMeth = typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) });
            generator.Emit(OpCodes.Call, tTypeMeth);
        }
    }
}
